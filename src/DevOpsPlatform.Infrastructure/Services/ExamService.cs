namespace DevOpsPlatform.Infrastructure.Services;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Enums;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Core.Models;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public static class ExamGrader
{
    public static (bool Correct, decimal Score, string? Explanation) Grade(Question q, JsonElement answer)
    {
        var max = MaxScore(q);
        return q.QuestionType switch
        {
            QuestionType.SingleChoice or QuestionType.TrueFalse => GradeSingle(q, answer, max),
            QuestionType.MultipleChoice => GradeMultiple(q, answer, max),
            _ => GradeExact(q, answer, max)
        };
    }

    public static decimal MaxScore(Question q)
    {
        if (q.Metadata.RootElement.ValueKind == JsonValueKind.Object
            && q.Metadata.RootElement.TryGetProperty("maxScore", out var m)
            && m.TryGetDecimal(out var v))
            return v;
        return 10;
    }

    private static (bool, decimal, string?) GradeSingle(Question q, JsonElement answer, decimal max)
    {
        var expected = Norm(GetCorrectText(q));
        var given = Norm(GetAnswerText(answer));
        var ok = !string.IsNullOrEmpty(expected) && expected == given;
        return (ok, ok ? max : 0, q.Explanation);
    }

    private static (bool, decimal, string?) GradeMultiple(Question q, JsonElement answer, decimal max)
    {
        var expected = GetCorrectSet(q);
        var given = GetAnswerSet(answer);
        if (expected.Count == 0)
            return (false, 0, q.Explanation);
        var ok = expected.SetEquals(given);
        if (ok) return (true, max, q.Explanation);
        // Credito parcial: interseccion menos penalizacion por extras
        var hits = expected.Intersect(given).Count();
        var extras = given.Except(expected).Count();
        var partial = Math.Max(0, (decimal)(hits - extras) / expected.Count) * max;
        return (false, Math.Round(partial, 2), q.Explanation);
    }

    private static (bool, decimal, string?) GradeExact(Question q, JsonElement answer, decimal max)
    {
        var expected = Norm(GetCorrectText(q));
        var given = Norm(GetAnswerText(answer));
        var ok = !string.IsNullOrEmpty(expected) && expected == given;
        return (ok, ok ? max : 0, q.Explanation ?? "Este tipo de pregunta requiere revision manual.");
    }

    private static string? GetCorrectText(Question q)
    {
        try
        {
            var root = q.CorrectAnswer.RootElement;
            return root.ValueKind switch
            {
                JsonValueKind.String => root.GetString(),
                JsonValueKind.Number => root.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => root.GetRawText()
            };
        }
        catch { return null; }
    }

    private static string? GetAnswerText(JsonElement answer) => answer.ValueKind switch
    {
        JsonValueKind.String => answer.GetString(),
        JsonValueKind.Number => answer.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Undefined => null,
        _ => answer.GetRawText()
    };

    private static HashSet<string> GetCorrectSet(Question q)
    {
        try
        {
            var root = q.CorrectAnswer.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
                return root.EnumerateArray().Select(e => Norm(GetAnswerText(e)) ?? "").Where(s => s != "").ToHashSet();
            var single = Norm(GetCorrectText(q));
            return single != null ? [single] : [];
        }
        catch { return []; }
    }

    private static HashSet<string> GetAnswerSet(JsonElement answer)
    {
        if (answer.ValueKind == JsonValueKind.Array)
            return answer.EnumerateArray().Select(e => Norm(GetAnswerText(e)) ?? "").Where(s => s != "").ToHashSet();
        var single = Norm(GetAnswerText(answer));
        return single != null ? [single] : [];
    }

    private static string? Norm(string? s) => s?.Trim().ToLowerInvariant();
}

public class ExamService : IExamService
{
    private readonly PlatformDbContext _db;
    private readonly ProgressService _progress;
    private readonly ILogger<ExamService> _logger;

    public ExamService(PlatformDbContext db, ProgressService progress, ILogger<ExamService> logger)
    {
        _db = db;
        _progress = progress;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Exam>> GetExamsAsync(CancellationToken ct = default)
        => await _db.Exams.Where(e => e.IsActive).OrderBy(e => e.Title).ToListAsync(ct);

    public async Task<Exam?> GetExamAsync(Guid id, CancellationToken ct = default)
        => await _db.Exams.FirstOrDefaultAsync(e => e.Id == id && e.IsActive, ct);

    public async Task<ExamStartResponse> StartExamAsync(Guid userId, Guid examId, CancellationToken ct = default)
    {
        var exam = await GetExamAsync(examId, ct)
            ?? throw new InvalidOperationException("Examen no encontrado.");

        var questions = await SelectQuestionsAsync(exam, ct);

        var attempt = new ExamAttempt
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ExamId = exam.Id,
            Status = "in_progress",
            MaxScore = questions.Sum(q => q.MaxScore)
        };
        _db.ExamAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Examen {ExamId} iniciado por {UserId}, intento {AttemptId}", examId, userId, attempt.Id);

        return new ExamStartResponse
        {
            AttemptId = attempt.Id,
            Questions = questions,
            ExpiresAt = exam.TimeLimitMinutes.HasValue
                ? DateTimeOffset.UtcNow.AddMinutes(exam.TimeLimitMinutes.Value)
                : DateTimeOffset.UtcNow.AddHours(4)
        };
    }

    public async Task<ExamResultDto> SubmitExamAsync(Guid userId, ExamSubmitRequest req, CancellationToken ct = default)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Exam)
            .FirstOrDefaultAsync(a => a.Id == req.AttemptId && a.UserId == userId, ct)
            ?? throw new InvalidOperationException("Intento no encontrado.");

        if (attempt.Status != "in_progress")
            throw new InvalidOperationException("El intento ya fue enviado.");

        var questions = await _db.Questions
            .Where(q => req.Answers.Select(a => a.QuestionId).Contains(q.Id) && q.IsActive)
            .ToListAsync(ct);

        var results = new List<QuestionResultDto>();
        decimal score = 0, max = 0;

        foreach (var q in questions)
        {
            var userAnswer = req.Answers.First(a => a.QuestionId == q.Id).Answer;
            var (correct, qScore, explanation) = ExamGrader.Grade(q, userAnswer);
            var qMax = ExamGrader.MaxScore(q);
            score += qScore;
            max += qMax;
            results.Add(new QuestionResultDto
            {
                QuestionId = q.Id,
                Correct = correct,
                Score = qScore,
                MaxScore = qMax,
                Explanation = explanation,
                UserAnswer = userAnswer
            });
        }

        var percentage = max > 0 ? Math.Round(score / max * 100, 2) : 0;
        var passed = percentage >= attempt.Exam.PassingScore;

        var updated = attempt with
        {
            Status = "graded",
            Score = score,
            MaxScore = max,
            SubmittedAt = DateTimeOffset.UtcNow,
            GradedAt = DateTimeOffset.UtcNow,
            Answers = JsonDocument.Parse(JsonSerializer.Serialize(req.Answers)),
            GradingDetails = JsonDocument.Parse(JsonSerializer.Serialize(results))
        };
        _db.Entry(attempt).CurrentValues.SetValues(updated);
        await _db.SaveChangesAsync(ct);

        // Evidencia para skills + insight si hay debilidad
        await _progress.AddExamEvidenceAsync(
            userId, attempt.Exam.Technology ?? "general", null,
            attempt.Exam.Level ?? "basic", percentage, ct);

        if (!passed)
        {
            _db.Insights.Add(new Insight
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Type = "weakness",
                Title = $"Repasar: {attempt.Exam.Title}",
                Description = $"Obtuviste {percentage:F0}% (mínimo {attempt.Exam.PassingScore:F0}%). Repasa la teoría e inténtalo de nuevo.",
                Priority = 8
            });
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Examen {AttemptId} calificado: {Score}/{Max} ({Pct}%)", attempt.Id, score, max, percentage);

        return new ExamResultDto
        {
            AttemptId = attempt.Id,
            Score = score,
            MaxScore = max,
            Percentage = percentage,
            Passed = passed,
            QuestionResults = results,
            GradedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<ExamResultDto?> GetResultAsync(Guid attemptId, Guid userId, CancellationToken ct = default)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Exam)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct);

        if (attempt == null || attempt.Status != "graded" || attempt.Score == null || attempt.MaxScore == null)
            return null;

        List<QuestionResultDto> details;
        try
        {
            details = JsonSerializer.Deserialize<List<QuestionResultDto>>(attempt.GradingDetails!.RootElement.GetRawText()) ?? [];
        }
        catch { details = []; }

        var percentage = attempt.MaxScore > 0 ? Math.Round(attempt.Score.Value / attempt.MaxScore.Value * 100, 2) : 0;

        return new ExamResultDto
        {
            AttemptId = attempt.Id,
            Score = attempt.Score.Value,
            MaxScore = attempt.MaxScore.Value,
            Percentage = percentage,
            Passed = percentage >= attempt.Exam.PassingScore,
            QuestionResults = details,
            GradedAt = attempt.GradedAt ?? DateTimeOffset.UtcNow
        };
    }

    private async Task<List<ExamQuestionDto>> SelectQuestionsAsync(Exam exam, CancellationToken ct)
    {
        // QuestionSelection admite { "questionIds": [...] } o { "technology": "x", "level": "y", "count": N }
        List<Question> picked = [];
        try
        {
            var sel = exam.QuestionSelection.RootElement;
            if (sel.ValueKind == JsonValueKind.Object && sel.TryGetProperty("questionIds", out var ids))
            {
                var idList = ids.EnumerateArray()
                    .Select(e => Guid.TryParse(e.GetString(), out var u) ? u : (Guid?)null)
                    .Where(u => u.HasValue).Select(u => u!.Value).ToList();
                picked = await _db.Questions.Where(q => idList.Contains(q.Id) && q.IsActive).ToListAsync(ct);
            }
            else
            {
                var tech = sel.TryGetProperty("technology", out var t) ? t.GetString() : exam.Technology;
                var level = sel.TryGetProperty("level", out var l) ? l.GetString() : exam.Level;
                var count = sel.TryGetProperty("count", out var c) && c.TryGetInt32(out var n) ? n : exam.QuestionCount;
                var query = _db.Questions.Where(q => q.IsActive);
                if (!string.IsNullOrEmpty(tech)) query = query.Where(q => q.Technology == tech);
                if (!string.IsNullOrEmpty(level)) query = query.Where(q => q.Level == level);
                picked = (await query.ToListAsync(ct)).OrderBy(_ => Guid.NewGuid()).Take(count > 0 ? count : 10).ToList();
            }
        }
        catch
        {
            picked = [];
        }

        var order = 0;
        return picked.Select(q => new ExamQuestionDto
        {
            QuestionId = q.Id,
            Order = ++order,
            Prompt = q.Prompt,
            QuestionType = q.QuestionType,
            Options = q.Options?.RootElement,
            MaxScore = ExamGrader.MaxScore(q)
        }).ToList();
    }
}
