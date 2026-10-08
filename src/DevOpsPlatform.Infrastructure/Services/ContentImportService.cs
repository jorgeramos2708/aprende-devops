namespace DevOpsPlatform.Infrastructure.Services;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Enums;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Pipeline de contenido como codigo: importa content/ (markdown + YAML) a la DB.
/// - Idempotente: upsert por (type, slug, version) / (slug) / (technology, prompt)
/// - Fuente de verdad editorial = git; la DB es la cache de trabajo.
/// - No borra contenido que desaparece del repo (decision deliberada: deprecar, no eliminar).
/// Formato soportado:
///   content/<tech>/technology.yml                 -> nodo Technology
///   content/<tech>/<nivel>/<NN>-<slug>/lesson.md  -> nodos Lesson (front-matter YAML + markdown)
///   content/<tech>/<nivel>/questions.yml          -> Exam de nivel + banco de preguntas
/// </summary>
public static class ContentImportService
{
    private static readonly string[] Levels = ["basic", "intermediate", "advanced", "expert"];

    public static async Task ImportAsync(PlatformDbContext db, string contentDir, ILogger logger, CancellationToken ct = default)
    {
        if (!Directory.Exists(contentDir))
        {
            logger.LogInformation("Sin directorio de contenido {Dir}; se omite importacion", contentDir);
            return;
        }

        var imported = 0;
        var techDirs = Directory.GetDirectories(contentDir);
        foreach (var techDir in techDirs)
        {
            var techSlug = Path.GetFileName(techDir);
            var techFile = Path.Combine(techDir, "technology.yml");
            if (!File.Exists(techFile)) continue;

            var tech = await ImportTechnologyAsync(db, techSlug, techFile, logger, ct);
            if (tech == null) continue;
            imported++;

            foreach (var level in Levels)
            {
                var levelDir = Path.Combine(techDir, level);
                if (!Directory.Exists(levelDir)) continue;

                // Lecciones: <NN>-<slug>/lesson.md
                foreach (var lessonDir in Directory.GetDirectories(levelDir).OrderBy(d => d))
                {
                    var lessonFile = Path.Combine(lessonDir, "lesson.md");
                    if (!File.Exists(lessonFile)) continue;
                    await ImportLessonAsync(db, tech, techSlug, level, Path.GetFileName(lessonDir), lessonFile, logger, ct);
                }

                // Examen de nivel: questions.yml
                var questionsFile = Path.Combine(levelDir, "questions.yml");
                if (File.Exists(questionsFile))
                    await ImportLevelExamAsync(db, techSlug, level, questionsFile, logger, ct);
            }

            // Limpieza de lecciones del seed demo (slugs cortos del DbSeeder heredado)
            // cuando git ya aporta contenido real para esa tecnologia
            var demoSlugs = new[] { $"{techSlug}-basic-cli", $"{techSlug}-basic-permissions", $"{techSlug}-intermediate-systemd" };
            var demos = await db.KnowledgeNodes
                .Where(n => n.Type == NodeType.Lesson && demoSlugs.Contains(n.Slug))
                .ToListAsync(ct);
            if (demos.Count > 0)
            {
                db.KnowledgeNodes.RemoveRange(demos);
                logger.LogInformation("Retiradas {Count} lecciones demo de {Tech}", demos.Count, techSlug);
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Importacion de contenido completada desde {Dir} ({Count} tecnologias)", contentDir, imported);
    }

    // ---------------------------------------------------------------- tecnologia
    private static async Task<KnowledgeNode?> ImportTechnologyAsync(
        PlatformDbContext db, string slug, string file, ILogger logger, CancellationToken ct)
    {
        TechnologyYaml? def;
        try
        {
            def = Yaml().Deserialize<TechnologyYaml>(await File.ReadAllTextAsync(file, ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, " technology.yml invalido en {File}", file);
            return null;
        }
        if (def == null) return null;

        var existing = await db.KnowledgeNodes
            .FirstOrDefaultAsync(n => n.Type == NodeType.Technology && n.Slug == slug && n.Version == def.Version, ct);

        var content = JsonDocument.Parse(JsonSerializer.Serialize(new { description = def.Description }));
        var metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            technology = slug,
            icon = def.Icon,
            color = def.Color,
            official_docs = def.OfficialDocs,
            tech_version = def.TechVersion,
            order = def.Order
        }));

        if (existing == null)
        {
            db.KnowledgeNodes.Add(new KnowledgeNode
            {
                Id = Guid.CreateVersion7(),
                Type = NodeType.Technology,
                Slug = slug,
                Title = def.Title,
                Version = def.Version,
                Content = content,
                Metadata = metadata
            });
        }
        else
        {
            var updated = existing with { Title = def.Title, Content = content, Metadata = metadata };
            db.Entry(existing).CurrentValues.SetValues(updated);
        }
        await db.SaveChangesAsync(ct);

        // devolver la entidad persistida (necesitamos su Id para enlazar lecciones)
        return await db.KnowledgeNodes.FirstAsync(
            n => n.Type == NodeType.Technology && n.Slug == slug && n.Version == def.Version, ct);
    }

    // ---------------------------------------------------------------- leccion
    private static async Task ImportLessonAsync(
        PlatformDbContext db, KnowledgeNode tech, string techSlug, string level, string dirName, string file,
        ILogger logger, CancellationToken ct)
    {
        var raw = await File.ReadAllTextAsync(file, ct);
        var (fm, body) = SplitFrontMatter(raw, file, logger);
        if (fm == null) return;

        LessonYaml? def;
        try
        {
            def = Yaml().Deserialize<LessonYaml>(fm);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Front-matter invalido en {File}", file);
            return;
        }
        if (def == null) return;

        var slug = $"{techSlug}-{level}-{dirName}";
        var existing = await db.KnowledgeNodes
            .FirstOrDefaultAsync(n => n.Type == NodeType.Lesson && n.Slug == slug && n.Version == tech.Version, ct);

        var content = JsonDocument.Parse(JsonSerializer.Serialize(new { markdown = body }));
        var metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            technology = techSlug,
            level,
            topic = def.Topic ?? "",
            module = def.Module ?? "",
            order = def.Order,
            estimated_minutes = def.EstimatedMinutes,
            sources = def.Sources ?? [],
            objective = def.Objective ?? "",
            tech_version = def.TechVersion ?? ""
        }));

        if (existing == null)
        {
            db.KnowledgeNodes.Add(new KnowledgeNode
            {
                Id = Guid.CreateVersion7(),
                Type = NodeType.Lesson,
                Slug = slug,
                Title = def.Title,
                Version = tech.Version,
                Content = content,
                Metadata = metadata,
                ParentId = tech.Id
            });
        }
        else
        {
            var updated = existing with
            {
                Title = def.Title,
                Content = content,
                Metadata = metadata,
                ParentId = tech.Id
            };
            db.Entry(existing).CurrentValues.SetValues(updated);
        }
        logger.LogDebug("Leccion importada: {Slug}", slug);
    }

    // ---------------------------------------------------------------- examen de nivel
    private static async Task ImportLevelExamAsync(
        PlatformDbContext db, string techSlug, string level, string file, ILogger logger, CancellationToken ct)
    {
        ExamYaml? def;
        try
        {
            def = Yaml().Deserialize<ExamYaml>(await File.ReadAllTextAsync(file, ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "questions.yml invalido en {File}", file);
            return;
        }
        if (def?.Exam == null || def.Questions == null || def.Questions.Count == 0) return;

        var slug = $"{techSlug}-{level}";
        var selection = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            technology = techSlug,
            level,
            count = def.Exam.Count
        }));

        var exam = await db.Exams.FirstOrDefaultAsync(e => e.Slug == slug, ct);
        if (exam == null)
        {
            exam = new Exam
            {
                Id = Guid.CreateVersion7(),
                Technology = techSlug,
                Slug = slug,
                Title = def.Exam.Title,
                Description = def.Exam.Description,
                Level = level,
                PassingScore = def.Exam.PassingScore,
                TimeLimitMinutes = def.Exam.TimeLimitMinutes,
                QuestionCount = def.Exam.Count,
                QuestionSelection = selection,
                IsActive = true
            };
            db.Exams.Add(exam);
        }
        else
        {
            var updated = exam with
            {
                Title = def.Exam.Title,
                Description = def.Exam.Description,
                PassingScore = def.Exam.PassingScore,
                TimeLimitMinutes = def.Exam.TimeLimitMinutes,
                QuestionCount = def.Exam.Count,
                QuestionSelection = selection
            };
            db.Entry(exam).CurrentValues.SetValues(updated);
        }

        foreach (var q in def.Questions)
            await UpsertQuestionAsync(db, techSlug, q, ct);

        logger.LogDebug("Examen de nivel importado: {Slug} ({Count} preguntas en banco)", slug, def.Questions.Count);
    }

    private static async Task UpsertQuestionAsync(PlatformDbContext db, string techSlug, QuestionYaml q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Prompt)) return;

        var existing = await db.Questions
            .FirstOrDefaultAsync(x => x.Technology == techSlug && x.Prompt == q.Prompt, ct);

        var options = q.Options != null ? JsonDocument.Parse(JsonSerializer.Serialize(q.Options)) : null;
        var correct = JsonDocument.Parse(JsonSerializer.Serialize(q.Answer));

        if (existing == null)
        {
            db.Questions.Add(new Question
            {
                Id = Guid.CreateVersion7(),
                Technology = techSlug,
                Topic = q.Topic,
                Subtopic = q.Subtopic,
                Level = q.Level ?? "basic",
                Difficulty = ParseDifficulty(q.Difficulty),
                QuestionType = ParseQuestionType(q.Type),
                Prompt = q.Prompt,
                Options = options,
                CorrectAnswer = correct,
                Explanation = q.Explanation,
                OfficialSource = q.Source,
                TechnologyVersion = q.TechVersion,
                Tags = q.Tags ?? [],
                IsActive = true
            });
        }
        else
        {
            var updated = existing with
            {
                Topic = q.Topic,
                Subtopic = q.Subtopic,
                Level = q.Level ?? existing.Level,
                Difficulty = ParseDifficulty(q.Difficulty),
                QuestionType = ParseQuestionType(q.Type ?? existing.QuestionType.ToString()),
                Options = options ?? existing.Options,
                CorrectAnswer = correct,
                Explanation = q.Explanation ?? existing.Explanation,
                OfficialSource = q.Source ?? existing.OfficialSource,
                TechnologyVersion = q.TechVersion ?? existing.TechnologyVersion,
                Tags = q.Tags ?? existing.Tags,
                IsActive = true
            };
            db.Entry(existing).CurrentValues.SetValues(updated);
        }
    }

    // ---------------------------------------------------------------- utilidades
    // Convencion del contenido: YAML siempre en snake_case
    private static IDeserializer Yaml() => new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();
    private static (string? frontMatter, string body) SplitFrontMatter(string raw, string file, ILogger logger)
    {
        // Formato: primera linea "---", luego YAML, luego "---" y el cuerpo markdown
        var lines = raw.Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 3 || lines[0].Trim() != "---")
        {
            logger.LogWarning("Leccion sin front-matter: {File}", file);
            return (null, raw);
        }
        var end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
        if (end < 0)
        {
            logger.LogWarning("Front-matter sin cierre: {File}", file);
            return (null, raw);
        }
        return (string.Join('\n', lines[1..end]), string.Join('\n', lines[(end + 1)..]).Trim());
    }

    private static Difficulty ParseDifficulty(string? d) => (d ?? "").Trim().ToLowerInvariant() switch
    {
        "intermediate" or "intermedio" => Difficulty.Intermediate,
        "advanced" or "avanzado" => Difficulty.Advanced,
        "expert" or "experto" => Difficulty.Expert,
        _ => Difficulty.Beginner
    };

    private static QuestionType ParseQuestionType(string? t) => (t ?? "").Trim().ToLowerInvariant() switch
    {
        "multiple_choice" or "multiplechoice" => QuestionType.MultipleChoice,
        "true_false" or "truefalse" or "verdadero_falso" => QuestionType.TrueFalse,
        "ordering" => QuestionType.Ordering,
        _ => QuestionType.SingleChoice
    };

    // ---------------------------------------------------------------- modelos YAML
    private sealed class TechnologyYaml
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Version { get; set; } = "1.0";
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public int Order { get; set; }
        public List<string> OfficialDocs { get; set; } = [];
        public string? TechVersion { get; set; }
    }

    private sealed class LessonYaml
    {
        public string Title { get; set; } = "";
        public int Order { get; set; }
        public string? Topic { get; set; }
        public string? Module { get; set; }
        public int EstimatedMinutes { get; set; } = 15;
        public List<string>? Sources { get; set; }
        public string? Objective { get; set; }
        public string? TechVersion { get; set; }
    }

    private sealed class ExamYaml
    {
        public ExamDef? Exam { get; set; }
        public List<QuestionYaml>? Questions { get; set; }
    }

    private sealed class ExamDef
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public decimal PassingScore { get; set; } = 80;
        public int TimeLimitMinutes { get; set; } = 30;
        public int Count { get; set; } = 10;
    }

    private sealed class QuestionYaml
    {
        public string Prompt { get; set; } = "";
        public string? Topic { get; set; }
        public string? Subtopic { get; set; }
        public string? Level { get; set; }
        public string? Difficulty { get; set; }
        public string? Type { get; set; }
        public List<string>? Options { get; set; }
        public object? Answer { get; set; }
        public string? Explanation { get; set; }
        public string? Source { get; set; }
        public string? TechVersion { get; set; }
        public string[]? Tags { get; set; }
    }
}
