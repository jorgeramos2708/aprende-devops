namespace DevOpsPlatform.Workers.Jobs;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using DevOpsPlatform.Infrastructure.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public interface IBackgroundJobService
{
    Task ScheduleLabCleanupAsync(string attemptId, TimeSpan delay);
    Task ScheduleInsightGenerationAsync(string userId);
    Task ScheduleCertificationReadinessAsync(string userId, string certificationId);
    Task ScheduleLabRegressionAsync(string technology, string version);
}

public class BackgroundJobService : IBackgroundJobService
{
    public Task ScheduleLabCleanupAsync(string attemptId, TimeSpan delay)
    {
        BackgroundJob.Schedule<LabCleanupJob>(j => j.ExecuteAsync(attemptId), delay);
        return Task.CompletedTask;
    }

    public Task ScheduleInsightGenerationAsync(string userId)
    {
        BackgroundJob.Enqueue<InsightGenerationJob>(j => j.ExecuteAsync(userId));
        return Task.CompletedTask;
    }

    public Task ScheduleCertificationReadinessAsync(string userId, string certificationId)
    {
        BackgroundJob.Enqueue<CertificationReadinessJob>(j => j.ExecuteAsync(userId, certificationId));
        return Task.CompletedTask;
    }

    public Task ScheduleLabRegressionAsync(string technology, string version)
    {
        BackgroundJob.Enqueue<LabRegressionJob>(j => j.ExecuteAsync(technology, version));
        return Task.CompletedTask;
    }
}

public class LabCleanupJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LabCleanupJob> _logger;

    public LabCleanupJob(IServiceScopeFactory scopeFactory, ILogger<LabCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ILabOrchestrator>();
        await orchestrator.CleanupExpiredLabsAsync(ct);
        _logger.LogInformation("Limpieza periodica de laboratorios expirados completada");
    }

    public async Task ExecuteAsync(string attemptId)
    {
        if (!Guid.TryParse(attemptId, out var id))
        {
            _logger.LogWarning("AttemptId invalido para limpieza: {AttemptId}", attemptId);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ILabOrchestrator>();

        await orchestrator.StopLabAsync(id);
        _logger.LogInformation("Cleaned up lab attempt {AttemptId}", id);
    }
}

public class InsightGenerationJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InsightGenerationJob> _logger;

    public InsightGenerationJob(IServiceScopeFactory scopeFactory, ILogger<InsightGenerationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var uid))
        {
            _logger.LogWarning("UserId invalido para insights: {UserId}", userId);
            return;
        }

        var ct = CancellationToken.None;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // Analyze user progress and generate insights
        var progress = await db.UserProgress
            .Where(p => p.UserId == uid)
            .ToListAsync(ct);

        var insights = new List<Insight>();

        // Weakness detection: topics with low scores
        var weakTopics = progress
            .Where(p => p.Status == "completed" && p.Score.HasValue && p.Score < 70)
            .GroupBy(p => p.NodeId)
            .Select(g => new { NodeId = g.Key, AvgScore = g.Average(p => p.Score!.Value), Count = g.Count() })
            .Where(x => x.AvgScore < 70 && x.Count >= 2)
            .OrderBy(x => x.AvgScore)
            .Take(5);

        foreach (var weak in weakTopics)
        {
            var node = await db.KnowledgeNodes.FindAsync([weak.NodeId], ct);
            if (node != null)
            {
                insights.Add(new Insight
                {
                    Id = Guid.CreateVersion7(),
                    UserId = uid,
                    Type = "weakness",
                    Title = $"Refuerza: {node.Title}",
                    Description = $"Tu puntuación promedio en este tema es {weak.AvgScore:F0}%. Revisa la teoría y repite los laboratorios.",
                    RelatedNodeIds = [weak.NodeId],
                    Priority = 8,
                    Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { avgScore = weak.AvgScore, attempts = weak.Count }))
                });
            }
        }

        // Strength detection: topics with high scores
        var strongTopics = progress
            .Where(p => p.Status == "completed" && p.Score.HasValue && p.Score >= 90)
            .GroupBy(p => p.NodeId)
            .Select(g => new { NodeId = g.Key, AvgScore = g.Average(p => p.Score!.Value), Count = g.Count() })
            .Where(x => x.AvgScore >= 90 && x.Count >= 2)
            .OrderByDescending(x => x.AvgScore)
            .Take(3);

        foreach (var strong in strongTopics)
        {
            var node = await db.KnowledgeNodes.FindAsync([strong.NodeId], ct);
            if (node != null)
            {
                insights.Add(new Insight
                {
                    Id = Guid.CreateVersion7(),
                    UserId = uid,
                    Type = "strength",
                    Title = $"Fortaleza: {node.Title}",
                    Description = $"Dominas este tema con {strong.AvgScore:F0}% de puntuación promedio.",
                    RelatedNodeIds = [strong.NodeId],
                    Priority = 3,
                    Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { avgScore = strong.AvgScore }))
                });
            }
        }

        // Certification readiness
        var certMappings = await db.CertificationMappings
            .Include(m => m.Certification)
            .Include(m => m.Node)
            .ToListAsync(ct);

        var certGroups = certMappings.GroupBy(m => m.CertificationId);
        foreach (var group in certGroups)
        {
            var cert = group.First().Certification;
            var coveredNodes = group.Select(m => m.NodeId).ToHashSet();
            var userCompletedNodes = progress
                .Where(p => p.Status == "completed" && coveredNodes.Contains(p.NodeId))
                .Select(p => p.NodeId)
                .ToHashSet();

            var readiness = coveredNodes.Count > 0
                ? (decimal)userCompletedNodes.Count / coveredNodes.Count * 100
                : 0;

            if (readiness > 50 && readiness < 90)
            {
                insights.Add(new Insight
                {
                    Id = Guid.CreateVersion7(),
                    UserId = uid,
                    Type = "certification_readiness",
                    Title = $"Preparación para {cert.Code}: {readiness:F0}%",
                    Description = $"Estás a {(100 - readiness):F0}% de cubrir los temas para {cert.Name}.",
                    RelatedNodeIds = coveredNodes.Except(userCompletedNodes).Take(10).ToArray(),
                    Priority = readiness > 80 ? 7 : 5,
                    Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
                    {
                        certificationId = cert.Id.ToString(),
                        readiness,
                        missingCount = coveredNodes.Count - userCompletedNodes.Count
                    }))
                });
            }
        }

        // Save insights
        db.Insights.AddRange(insights);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Generated {Count} insights for user {UserId}", insights.Count, uid);
    }
}

public class CertificationReadinessJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CertificationReadinessJob> _logger;

    public CertificationReadinessJob(IServiceScopeFactory scopeFactory, ILogger<CertificationReadinessJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(string userId, string certificationId)
    {
        if (!Guid.TryParse(userId, out var uid) || !Guid.TryParse(certificationId, out var cid))
        {
            _logger.LogWarning("Ids invalidos para readiness: {UserId}/{CertId}", userId, certificationId);
            return;
        }

        var ct = CancellationToken.None;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var mappings = await db.CertificationMappings
            .Include(m => m.Certification)
            .Where(m => m.CertificationId == cid)
            .ToListAsync(ct);

        if (mappings.Count == 0) return;

        var cert = mappings[0].Certification;
        var completed = (await db.UserProgress
            .Where(p => p.UserId == uid && p.Status == "completed")
            .Select(p => p.NodeId)
            .ToListAsync(ct)).ToHashSet();

        var total = mappings.Sum(m => m.CoverageWeight);
        var covered = mappings.Where(m => completed.Contains(m.NodeId)).Sum(m => m.CoverageWeight);
        var readiness = total > 0 ? Math.Round(covered / total * 100, 2) : 0;

        db.Insights.Add(new Insight
        {
            Id = Guid.CreateVersion7(),
            UserId = uid,
            Type = "certification_readiness",
            Title = $"Preparación para {cert.Code}: {readiness:F0}%",
            Description = $"Reporte detallado: {covered:F0} de {total:F0} puntos de cobertura.",
            RelatedNodeIds = mappings.Where(m => !completed.Contains(m.NodeId)).Select(m => m.NodeId).Take(10).ToArray(),
            Priority = 6,
            Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { certificationId = cert.Id.ToString(), readiness }))
        });
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Readiness {Cert} usuario {UserId}: {Pct}%", cert.Code, uid, readiness);
    }
}

public class LabRegressionJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LabRegressionJob> _logger;

    public LabRegressionJob(IServiceScopeFactory scopeFactory, ILogger<LabRegressionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task ExecuteAsync(string technology, string version)
        => ExecuteAsync(technology, version, CancellationToken.None);

    public async Task ExecuteAsync(string technology, string version, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var containerRuntime = scope.ServiceProvider.GetRequiredService<IContainerRuntime>();

        var suites = (await db.LabRegressionSuites
            .Include(s => s.LabEnvironment)
            .Where(s => s.IsActive)
            .ToListAsync(ct))
            .Where(s => LabMatchesTechnology(s.LabEnvironment, technology))
            .ToList();

        _logger.LogInformation("Running regression for {Technology} {Version}: {Count} suites", technology, version, suites.Count);

        foreach (var suite in suites)
        {
            var run = new LabRegressionRun
            {
                Id = Guid.CreateVersion7(),
                SuiteId = suite.Id,
                TechnologyVersion = version,
                Status = "running",
                StartedAt = DateTimeOffset.UtcNow
            };

            db.LabRegressionRuns.Add(run);
            await db.SaveChangesAsync(ct);

            try
            {
                // Create temporary container for regression test
                var containerId = await containerRuntime.CreateContainerAsync(suite.LabEnvironment, Guid.CreateVersion7(), ct);
                await containerRuntime.StartContainerAsync(containerId, ct);

                // Run test script
                var result = await containerRuntime.ExecAsync(containerId, ["bash", "-c", suite.TestScript], ct);

                var finished = run with
                {
                    Status = result.ExitCode == 0 ? "pass" : "fail",
                    Output = result.Stdout + "\n" + result.Stderr,
                    CompletedAt = DateTimeOffset.UtcNow
                };
                db.Entry(run).CurrentValues.SetValues(finished);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                var failed = run with { Status = "error", Output = ex.Message, CompletedAt = DateTimeOffset.UtcNow };
                db.Entry(run).CurrentValues.SetValues(failed);
                await db.SaveChangesAsync(ct);
                _logger.LogError(ex, "Regression failed for suite {SuiteId}", suite.Id);
            }
        }

        _logger.LogInformation("Regression completed for {Technology} {Version}", technology, version);
    }

    private static bool LabMatchesTechnology(LabEnvironment lab, string technology)
    {
        try
        {
            return lab.Metadata.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && lab.Metadata.RootElement.TryGetProperty("technology", out var v)
                && string.Equals(v.GetString(), technology, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

// Hangfire recurring jobs setup
public static class RecurringJobs
{
    public static void Configure()
    {
        var manager = new RecurringJobManager();

        // Limpieza diaria de laboratorios expirados a las 3 AM
        manager.AddOrUpdate<LabCleanupJob>("daily-lab-cleanup",
            j => j.ExecuteAsync(CancellationToken.None),
            "0 3 * * *");
    }
}
