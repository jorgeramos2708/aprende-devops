namespace DevOpsPlatform.Infrastructure.Watch;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class ImpactService : IImpactService
{
    private readonly PlatformDbContext _db;
    private readonly ILogger<ImpactService> _logger;

    public ImpactService(PlatformDbContext db, ILogger<ImpactService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ImpactAssessment>> ListAsync(CancellationToken ct = default)
        => await _db.ImpactAssessments
            .Include(a => a.Change)
            .OrderByDescending(a => a.AssessedAt)
            .Take(100)
            .ToListAsync(ct);

    public async Task<ImpactAssessment> EnsureForChangeAsync(Ulid changeId, CancellationToken ct = default)
    {
        var existing = await _db.ImpactAssessments.FirstOrDefaultAsync(a => a.ChangeId == changeId, ct);
        if (existing != null) return existing;

        var change = await _db.TechnologyChanges.FindAsync([changeId], ct)
            ?? throw new InvalidOperationException("Cambio no encontrado.");

        // Coincidencia por tecnologia declarada en el Metadata de nodos/labs o campo Technology
        var nodes = await _db.KnowledgeNodes.ToListAsync(ct);
        var affectedNodes = nodes
            .Where(n => MetaEquals(n.Metadata, "technology", change.Technology))
            .Select(n => new { nodeId = n.Id, nodeTitle = n.Title, nodeType = n.Type.ToString() })
            .ToList();

        var labs = await _db.LabEnvironments.ToListAsync(ct);
        var affectedLabs = labs
            .Where(l => MetaEquals(l.Metadata, "technology", change.Technology))
            .Select(l => new { labId = l.Id, labName = l.Name })
            .ToList();

        var questionRows = await _db.Questions
            .Where(q => q.Technology == change.Technology)
            .Select(q => new { questionId = q.Id, prompt = q.Prompt })
            .ToListAsync(ct);
        var questions = questionRows
            .Select(q => new { q.questionId, prompt = q.prompt.Length > 120 ? q.prompt[..120] : q.prompt })
            .ToList();

        var exams = await _db.Exams
            .Where(e => e.Technology == change.Technology)
            .Select(e => new { examId = e.Id, title = e.Title })
            .ToListAsync(ct);

        var total = affectedNodes.Count + affectedLabs.Count + questions.Count + exams.Count;
        var overall = change.Severity == "critical" || total > 10 ? "high"
            : total > 3 ? "medium" : "low";

        var assessment = new ImpactAssessment
        {
            Id = Ulid.NewUlid(),
            ChangeId = change.Id,
            AffectedNodes = JsonDocument.Parse(JsonSerializer.Serialize(affectedNodes)),
            AffectedLabs = JsonDocument.Parse(JsonSerializer.Serialize(affectedLabs)),
            AffectedQuestions = JsonDocument.Parse(JsonSerializer.Serialize(questions)),
            AffectedExams = JsonDocument.Parse(JsonSerializer.Serialize(exams)),
            AffectedCertMappings = JsonDocument.Parse("[]"),
            OverallImpact = overall,
            Recommendation = total == 0
                ? "Sin contenido afectado. Solo actualizar la version registrada."
                : $"Revisar {affectedNodes.Count} nodos, {affectedLabs.Count} laboratorios, {questions.Count} preguntas y {exams.Count} examenes."
        };

        _db.ImpactAssessments.Add(assessment);

        var withStatus = change with { Status = "assessed", ProcessedAt = DateTimeOffset.UtcNow };
        _db.Entry(change).CurrentValues.SetValues(withStatus);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Impacto evaluado para cambio {ChangeId}: {Impact} ({Total} items)", changeId, overall, total);
        return assessment;
    }

    private static bool MetaEquals(JsonDocument meta, string key, string value)
    {
        try
        {
            return meta.RootElement.ValueKind == JsonValueKind.Object
                && meta.RootElement.TryGetProperty(key, out var v)
                && string.Equals(v.GetString(), value, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

public class UpdateService : IUpdateService
{
    private readonly PlatformDbContext _db;

    public UpdateService(PlatformDbContext db) => _db = db;

    public async Task<IReadOnlyList<UpdateProposal>> ListAsync(CancellationToken ct = default)
        => await _db.UpdateProposals
            .Include(p => p.Change)
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

    public async Task<UpdateProposal> CreateAsync(Ulid changeId, CreateUpdateProposalRequest req, Ulid createdBy, CancellationToken ct = default)
    {
        _ = await _db.TechnologyChanges.FindAsync([changeId], ct)
            ?? throw new InvalidOperationException("Cambio no encontrado.");

        var proposal = new UpdateProposal
        {
            Id = Ulid.NewUlid(),
            ChangeId = req.ChangeId,
            Title = req.Title,
            Description = req.Description,
            ProposedChanges = JsonDocument.Parse("[]"),
            Status = "draft",
            CreatedBy = createdBy
        };

        _db.UpdateProposals.Add(proposal);
        await _db.SaveChangesAsync(ct);
        return proposal;
    }

    public async Task<UpdateProposal> ApproveAsync(Ulid id, Ulid reviewedBy, CancellationToken ct = default)
    {
        var proposal = await _db.UpdateProposals.FindAsync([id], ct)
            ?? throw new InvalidOperationException("Propuesta no encontrada.");

        var approved = proposal with
        {
            Status = "approved",
            ReviewedBy = reviewedBy,
            ReviewedAt = DateTimeOffset.UtcNow
        };
        _db.Entry(proposal).CurrentValues.SetValues(approved);
        await _db.SaveChangesAsync(ct);
        return approved;
    }
}

public class RegressionService : IRegressionService
{
    private readonly PlatformDbContext _db;
    private readonly IContainerRuntime _runtime;
    private readonly ILogger<RegressionService> _logger;

    public RegressionService(PlatformDbContext db, IContainerRuntime runtime, ILogger<RegressionService> logger)
    {
        _db = db;
        _runtime = runtime;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LabRegressionRun>> RunAsync(string? suiteId, string technologyVersion, CancellationToken ct = default)
    {
        var query = _db.LabRegressionSuites.Include(s => s.LabEnvironment).Where(s => s.IsActive);
        if (suiteId != null && Ulid.TryParse(suiteId, out var sid))
            query = query.Where(s => s.Id == sid);

        var suites = await query.ToListAsync(ct);
        var runs = new List<LabRegressionRun>();

        foreach (var suite in suites)
        {
            var run = new LabRegressionRun
            {
                Id = Ulid.NewUlid(),
                SuiteId = suite.Id,
                TechnologyVersion = technologyVersion,
                Status = "running",
                StartedAt = DateTimeOffset.UtcNow
            };
            _db.LabRegressionRuns.Add(run);
            await _db.SaveChangesAsync(ct);

            try
            {
                var containerId = await _runtime.CreateContainerAsync(suite.LabEnvironment, Ulid.NewUlid(), ct);
                await _runtime.StartContainerAsync(containerId, ct);
                var result = await _runtime.ExecAsync(containerId, ["bash", "-c", suite.TestScript], ct);
                await _runtime.StopContainerAsync(containerId, ct);
                await _runtime.RemoveContainerAsync(containerId, ct);

                var finished = run with
                {
                    Status = result.ExitCode == 0 ? "pass" : "fail",
                    Output = result.Stdout + "\n" + result.Stderr,
                    CompletedAt = DateTimeOffset.UtcNow
                };
                _db.Entry(run).CurrentValues.SetValues(finished);
                await _db.SaveChangesAsync(ct);
                runs.Add(finished);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Regresion fallo para suite {SuiteId}", suite.Id);
                var failed = run with { Status = "error", Output = ex.Message, CompletedAt = DateTimeOffset.UtcNow };
                _db.Entry(run).CurrentValues.SetValues(failed);
                await _db.SaveChangesAsync(ct);
                runs.Add(failed);
            }
        }

        return runs;
    }

    public async Task<IReadOnlyList<LabRegressionRun>> RecentRunsAsync(int take, CancellationToken ct = default)
        => await _db.LabRegressionRuns
            .Include(r => r.Suite)
            .OrderByDescending(r => r.CompletedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
}
