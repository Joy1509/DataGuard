using System.Collections.Concurrent;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;

namespace DataGuard.Core.Engine;

/// <summary>
/// Executes validation rules concurrently with bounded parallelism and per-rule timeout isolation.
/// </summary>
public sealed class ParallelRuleRunner : IRuleRunner
{
    public async Task<IReadOnlyList<RuleResult>> RunSuiteAsync(
        IReadOnlyList<IValidationRule> rules,
        RuleContext context,
        int maxDegreeOfParallelism = 4,
        Action<RuleResult>? onRuleCompleted = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(context);

        if (maxDegreeOfParallelism < 1) maxDegreeOfParallelism = 1;

        var results = new ConcurrentBag<RuleResult>();
        using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);

        var tasks = rules.Select(async rule =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, rule.Definition.TimeoutSeconds)));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                RuleResult result;
                try
                {
                    result = await rule.ExecuteAsync(context, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    var fp = FingerprintGenerator.Generate(rule.Definition.Id, rule.Definition.TargetTable, rule.Definition.TargetColumn);
                    result = RuleResult.Error(
                        rule.Definition,
                        fp,
                        (long)TimeSpan.FromSeconds(rule.Definition.TimeoutSeconds).TotalMilliseconds,
                        $"Rule timed out after {rule.Definition.TimeoutSeconds} seconds.");
                }
                catch (Exception ex)
                {
                    var fp = FingerprintGenerator.Generate(rule.Definition.Id, rule.Definition.TargetTable, rule.Definition.TargetColumn);
                    result = RuleResult.Error(
                        rule.Definition,
                        fp,
                        0,
                        $"Unhandled exception during rule execution: {ex.Message}");
                }

                results.Add(result);
                onRuleCompleted?.Invoke(result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return results.OrderBy(r => r.RuleId).ToList();
    }
}
