using DataGuard.Api.Services;
using DataGuard.Core.Enums;
using DataGuard.Core.Interfaces;
using DataGuard.Core.Models;
using DataGuard.Infrastructure.Reporting;
using Microsoft.AspNetCore.Mvc;

namespace DataGuard.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth() => Ok(new { status = "Healthy", timestamp = DateTime.UtcNow, version = "1.0.0" });
}

[ApiController]
[Route("api/v1/runs")]
public sealed class RunsController : ControllerBase
{
    private readonly ValidationJobQueue _queue;
    private readonly IRunRepository _runRepository;

    public RunsController(ValidationJobQueue queue, IRunRepository runRepository)
    {
        _queue = queue;
        _runRepository = runRepository;
    }

    [HttpPost]
    public async Task<IActionResult> TriggerRun([FromBody] TriggerRunRequest request, CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        var job = new ValidationJob(
            runId,
            request.RulesPath,
            request.InlineRules,
            request.SourceConnectionString,
            request.TargetConnectionString ?? "Server=localhost;Database=MigratedDB;TrustServerCertificate=True;",
            request.MetadataConnectionString,
            request.Parallelism <= 0 ? 4 : request.Parallelism);

        await _queue.EnqueueAsync(job, ct);

        return Accepted(new { runId, status = "Queued", message = "Validation run queued successfully." });
    }

    [HttpGet]
    public async Task<IActionResult> ListRuns([FromQuery] int top = 30, CancellationToken ct = default)
    {
        var runs = await _runRepository.GetRecentRunsAsync(top, ct);
        return Ok(runs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRun(Guid id, CancellationToken ct)
    {
        var summary = await _runRepository.GetSummaryAsync(id, ct);
        if (summary == null) return NotFound(new ProblemDetails { Title = "Run Not Found", Detail = $"Run with ID '{id}' was not found.", Status = 404 });
        return Ok(summary);
    }

    public record TriggerRunRequest(
        string? RulesPath,
        IReadOnlyList<RuleDefinition>? InlineRules,
        string? SourceConnectionString,
        string? TargetConnectionString,
        string? MetadataConnectionString,
        int Parallelism = 4);
}

[ApiController]
[Route("api/v1/rules")]
public sealed class RulesController : ControllerBase
{
    private readonly IRuleRepository _ruleRepository;

    public RulesController(IRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var rules = await _ruleRepository.GetAllAsync(ct);
        return Ok(rules);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var rule = await _ruleRepository.GetByIdAsync(id, ct);
        if (rule == null) return NotFound(new ProblemDetails { Title = "Rule Not Found", Detail = $"Rule '{id}' was not found.", Status = 404 });
        return Ok(rule);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] RuleDefinition rule, CancellationToken ct)
    {
        await _ruleRepository.SaveAsync(rule, ct);
        return Ok(new { success = true, ruleId = rule.Id });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _ruleRepository.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/defects")]
public sealed class DefectsController : ControllerBase
{
    private readonly IDefectRepository _defectRepository;

    public DefectsController(IDefectRepository defectRepository)
    {
        _defectRepository = defectRepository;
    }

    [HttpGet]
    public async Task<IActionResult> ListDefects([FromQuery] DefectStatus? status, [FromQuery] RuleSeverity? severity, CancellationToken ct)
    {
        var defects = await _defectRepository.GetAllDefectsAsync(status, severity, ct);
        return Ok(defects);
    }
}

[ApiController]
[Route("api/v1/metrics")]
public sealed class MetricsController : ControllerBase
{
    private readonly IRunRepository _runRepository;

    public MetricsController(IRunRepository runRepository)
    {
        _runRepository = runRepository;
    }

    [HttpGet("trend")]
    public async Task<IActionResult> GetTrend([FromQuery] int top = 30, CancellationToken ct = default)
    {
        var runs = await _runRepository.GetRecentRunsAsync(top, ct);
        var trend = runs.Select(r => new
        {
            r.RunId,
            r.StartedAtUtc,
            r.PassRatePercent,
            r.TotalRules,
            r.PassedRules,
            r.FailedRules,
            r.ErrorRules,
            DurationMs = r.DurationMs ?? 0
        });
        return Ok(trend);
    }
}

[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IRunRepository _runRepository;
    private readonly IPdfReportService _pdfReportService;

    public ReportsController(IRunRepository runRepository, IPdfReportService pdfReportService)
    {
        _runRepository = runRepository;
        _pdfReportService = pdfReportService;
    }

    [HttpGet("pdf/{runId:guid}")]
    public async Task<IActionResult> DownloadPdf(Guid runId, CancellationToken ct)
    {
        var summary = await _runRepository.GetSummaryAsync(runId, ct);
        if (summary == null) return NotFound(new ProblemDetails { Title = "Run Not Found", Detail = $"Run '{runId}' was not found.", Status = 404 });

        var bytes = _pdfReportService.GenerateRunSummaryReport(summary);
        return File(bytes, "application/pdf", $"DataGuard-Run-{runId}.pdf");
    }
}
