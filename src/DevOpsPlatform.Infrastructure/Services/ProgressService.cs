namespace DevOpsPlatform.Infrastructure.Services;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class ProgressService : IProgressService
{
    private readonly PlatformDbContext _db;
    private readonly ILogger<ProgressService> _logger;

    public ProgressService(PlatformDbContext db, ILogger<ProgressService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UserProgressDto> UpdateProgressAsync(Ulid userId, ProgressUpdateRequest req, CancellationToken ct = default)
    {
        var node = await _db.KnowledgeNodes.FindAsync([req.NodeId], ct)
            ?? throw new InvalidOperationException("Nodo no encontrado.");

        var progress = await _db.UserProgress
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NodeId == req.NodeId, ct);

        if (progress == null)
        {
            progress = new UserProgress
            {
                Id = Ulid.NewUlid(),
                UserId = userId,
                NodeId = req.NodeId,
                Status = req.Status,
                Score = req.Score,
                Attempts = 1,
                TimeSpentSeconds = req.TimeSpentSeconds,
                CompletedAt = req.Status == "completed" ? DateTimeOffset.UtcNow : null
            };
            _db.UserProgress.Add(progress);
        }
        else
        {
            progress.Status = req.Status;
            if (req.Score.HasValue && (!progress.Score.HasValue || req.Score > progress.Score))
                progress.Score = req.Score;
            progress.Attempts++;
            progress.TimeSpentSeconds += req.TimeSpentSeconds;
            if (req.Status == "completed")
                progress.CompletedAt ??= DateTimeOffset.UtcNow;
            progress.LastAccessedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await UpsertSkillAsync(node, progress, userId, ct);
        _logger.LogInformation("Progreso actualizado: usuario {UserId}, nodo {NodeId} -> {Status}", userId, req.NodeId, req.Status);

        return ToDto(progress, node.Title, node.Type);
    }

    public async Task<IReadOnlyList<UserProgressDto>> GetProgressAsync(Ulid userId, CancellationToken ct = default)
    {
        var items = await _db.UserProgress
            .Include(p => p.Node)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.LastAccessedAt)
            .ToListAsync(ct);

        return items.Select(p => ToDto(p, p.Node?.Title ?? string.Empty, p.Node?.Type ?? Core.Enums.NodeType.Lesson)).ToList();
    }

    public async Task<IReadOnlyList<SkillAssessmentDto>> GetSkillsAsync(Ulid userId, CancellationToken ct = default)
    {
        var skills = await _db.UserSkills.Where(s => s.UserId == userId).ToListAsync(ct);
        return skills.Select(s => new SkillAssessmentDto
        {
            Technology = s.Technology,
            Topic = s.Topic,
            Level = s.Level,
            Proficiency = s.Proficiency,
            Confidence = s.Confidence,
            EvidenceCount = s.EvidenceCount,
            LastAssessedAt = s.LastAssessedAt
        }).ToList();
    }

    private async Task UpsertSkillAsync(KnowledgeNode node, UserProgress progress, Ulid userId, CancellationToken ct)
    {
        // La tecnologia y el nivel viajan en el Metadata del nodo (ver DbSeeder)
        var technology = ReadMeta(node, "technology") ?? node.Slug;
        var topic = ReadMeta(node, "topic");
        var level = ReadMeta(node, "level") ?? "basic";

        // Sobrecarga usada internamente con userId explicito
        await UpsertSkillAsync(userId, technology, topic, level, progress.Score, ct);
    }

    private async Task UpsertSkillAsync(Ulid userId, string technology, string? topic, string level, decimal? score, CancellationToken ct)
    {
        var skill = await _db.UserSkills.FirstOrDefaultAsync(
            s => s.UserId == userId && s.Technology == technology && s.Topic == topic && s.Level == level, ct);

        if (skill == null)
        {
            skill = new UserSkill
            {
                Id = Ulid.NewUlid(),
                UserId = userId,
                Technology = technology,
                Topic = topic,
                Level = level,
                Proficiency = score ?? 0,
                Confidence = score.HasValue ? 50 : 0,
                EvidenceCount = 1,
                LastAssessedAt = DateTimeOffset.UtcNow
            };
            _db.UserSkills.Add(skill);
        }
        else
        {
            var updated = skill with
            {
                Proficiency = score.HasValue ? Math.Max(skill.Proficiency, score.Value) : skill.Proficiency,
                Confidence = Math.Min(100, skill.Confidence + 5),
                EvidenceCount = skill.EvidenceCount + 1,
                LastAssessedAt = DateTimeOffset.UtcNow
            };
            _db.Entry(skill).CurrentValues.SetValues(updated);
        }

        await _db.SaveChangesAsync(ct);
    }

    // Punto de entrada usado por ExamService para registrar evidencia de examen
    internal async Task AddExamEvidenceAsync(Ulid userId, string technology, string? topic, string level, decimal score, CancellationToken ct)
        => await UpsertSkillAsync(userId, technology, topic, level, score, ct);

    private static string? ReadMeta(KnowledgeNode node, string key)
    {
        if (node.Metadata.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            return null;
        return node.Metadata.RootElement.TryGetProperty(key, out var v) ? v.GetString() : null;
    }

    private static UserProgressDto ToDto(UserProgress p, string title, Core.Enums.NodeType type) => new()
    {
        NodeId = p.NodeId,
        NodeTitle = title,
        NodeType = type,
        Status = p.Status,
        Score = p.Score,
        Attempts = p.Attempts,
        TimeSpentSeconds = p.TimeSpentSeconds,
        CompletedAt = p.CompletedAt,
        LastAccessedAt = p.LastAccessedAt
    };
}
