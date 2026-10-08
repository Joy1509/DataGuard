using System.Threading.Channels;
using DataGuard.Core.Engine;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using DataGuard.Core.Rules;

namespace DataGuard.Api.Services;

public record ValidationJob(
    Guid RunId,
    string? RulesPath,
    IReadOnlyList<RuleDefinition>? InlineRules,
    string? SourceConnectionString,
    string TargetConnectionString,
    string? MetadataConnectionString,
    int Parallelism = 4);

public sealed class ValidationJobQueue
{
    private readonly Channel<ValidationJob> _channel = Channel.CreateBounded<ValidationJob>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait
    });

    public async ValueTask EnqueueAsync(ValidationJob job, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(job, ct).ConfigureAwait(false);
    }

    public IAsyncEnumerable<ValidationJob> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}

public sealed class ValidationBackgroundWorker : BackgroundService
{
    private readonly ValidationJobQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ValidationBackgroundWorker> _logger;

    public ValidationBackgroundWorker(
        ValidationJobQueue queue,
        IServiceProvider serviceProvider,
        ILogger<ValidationBackgroundWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataGuard Validation Background Worker started.");

        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Starting execution of queued validation run {RunId}...", job.RunId);

                using var scope = _serviceProvider.CreateScope();
                var sqlExecutor = scope.ServiceProvider.GetRequiredService<ISqlExecutor>();
                var ruleFactory = new RuleFactory(sqlExecutor);
                var runner = new ParallelRuleRunner();

                var ruleDefs = job.InlineRules ?? Array.Empty<RuleDefinition>();

                var executableRules = ruleDefs.Where(r => r.Enabled).Select(ruleFactory.Create).ToList();
                var context = new RuleContext
                {
                    RunId = job.RunId,
                    SourceConnectionString = job.SourceConnectionString,
                    TargetConnectionString = job.TargetConnectionString
                };

                var results = await runner.RunSuiteAsync(executableRules, context, job.Parallelism, ct: stoppingToken).ConfigureAwait(false);

                _logger.LogInformation("Completed validation run {RunId}. Total rules: {Count}, Passed: {Passed}", 
                    job.RunId, results.Count, results.Count(r => r.Status == Core.Enums.RuleStatus.Passed));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing validation job for run {RunId}", job.RunId);
            }
        }
    }
}
