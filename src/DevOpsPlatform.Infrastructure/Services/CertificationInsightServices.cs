namespace DevOpsPlatform.Infrastructure.Services;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class CertificationService : ICertificationService
{
    private readonly PlatformDbContext _db;
    private readonly ILogger<CertificationService> _logger;

    public CertificationService(PlatformDbContext db, ILogger<CertificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Certification>> GetCertificationsAsync(CancellationToken ct = default)
        => await _db.Certifications.Where(c => c.IsActive).OrderBy(c => c.Code).ToListAsync(ct);

    public async Task<CertificationReadinessDto?> GetReadinessAsync(Ulid userId, Ulid certificationId, CancellationToken ct = default)
    {
        var cert = await _db.Certifications
            .Include(c => c.Mappings)
            .FirstOrDefaultAsync(c => c.Id == certificationId && c.IsActive, ct);

        if (cert == null) return null;

        var completed = (await _db.UserProgress
            .Where(p => p.UserId == userId && p.Status == "completed")
            .Select(p => p.NodeId)
            .ToListAsync(ct)).ToHashSet();

        var domains = cert.Mappings
            .GroupBy(m => m.DomainName)
            .Select(g =>
            {
                var total = g.Sum(m => m.CoverageWeight);
                var covered = g.Where(m => completed.Contains(m.NodeId)).Sum(m => m.CoverageWeight);
                var missing = g.Where(m => !completed.Contains(m.NodeId)).Select(m => m.NodeId).ToList();
                return new DomainReadinessDto
                {
                    DomainName = g.Key,
                    Weight = g.Sum(m => m.DomainWeight),
                    Readiness = total > 0 ? Math.Round(covered / total * 100, 2) : 0,
                    CoveredNodes = g.Where(m => completed.Contains(m.NodeId)).Select(m => m.NodeId).ToList(),
                    MissingNodes = missing
                };
            }).ToList();

        var weightSum = domains.Sum(d => d.Weight);
        var overall = weightSum > 0 ? Math.Round(domains.Sum(d => d.Readiness * d.Weight) / weightSum, 2) : 0;

        _logger.LogInformation("Readiness {Cert} usuario {UserId}: {Pct}%", cert.Code, userId, overall);

        return new CertificationReadinessDto
        {
            CertificationId = cert.Id,
            CertificationCode = cert.Code,
            CertificationName = cert.Name,
            OverallReadiness = overall,
            Domains = domains,
            RecommendedNodes = domains.SelectMany(d => d.MissingNodes).Take(10).ToList()
        };
    }
}

public class InsightService : IInsightService
{
    private readonly PlatformDbContext _db;

    public InsightService(PlatformDbContext db) => _db = db;

    public async Task<IReadOnlyList<InsightDto>> GetInsightsAsync(Ulid userId, CancellationToken ct = default)
    {
        var items = await _db.Insights
            .Where(i => i.UserId == userId && !i.IsDismissed)
            .OrderByDescending(i => i.Priority)
            .ThenByDescending(i => i.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return items.Select(i => new InsightDto
        {
            Id = i.Id,
            Type = i.Type,
            Title = i.Title,
            Description = i.Description,
            Priority = i.Priority,
            IsRead = i.IsRead,
            IsDismissed = i.IsDismissed,
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt
        }).ToList();
    }

    public async Task MarkReadAsync(Ulid userId, Ulid id, CancellationToken ct = default)
    {
        var insight = await _db.Insights.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, ct);
        if (insight == null) return;
        _db.Entry(insight).CurrentValues.SetValues(insight with { IsRead = true });
        await _db.SaveChangesAsync(ct);
    }

    public async Task DismissAsync(Ulid userId, Ulid id, CancellationToken ct = default)
    {
        var insight = await _db.Insights.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, ct);
        if (insight == null) return;
        _db.Entry(insight).CurrentValues.SetValues(insight with { IsDismissed = true });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Insight> CreateAsync(Insight insight, CancellationToken ct = default)
    {
        _db.Insights.Add(insight);
        await _db.SaveChangesAsync(ct);
        return insight;
    }
}
