namespace DevOpsPlatform.Infrastructure.Services;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Enums;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

/// <summary>
/// Contenido inicial minimo para que roadmap, labs y examenes devuelvan datos reales.
/// Solo inserta si la base esta vacia. El contenido curado vive en git (content/).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(PlatformDbContext db, IPasswordHasher hasher, ILogger logger, CancellationToken ct = default)
    {
        if (await db.KnowledgeNodes.AnyAsync(ct))
            return;

        logger.LogInformation("Base vacia: insertando contenido inicial.");

        var linux = TechNode("linux", "Linux", "Fundamentos de Linux para DevOps: terminal, permisos, procesos y systemd.", "1.0");
        var git = TechNode("git", "Git", "Control de versiones con Git: ramas, merges y flujo de trabajo.", "1.0");
        var docker = TechNode("docker", "Docker", "Contenedores con Docker: imagenes, volumenes y redes.", "1.0");
        db.KnowledgeNodes.AddRange([linux, git, docker]);

        var lessons = new[]
        {
            (Lesson(linux, "linux-basic-cli", "La terminal de Linux", "basic", "cli",
                "# La terminal de Linux\n\nAprende `ls`, `cd`, `pwd`, `cat` y redirecciones. Fuente oficial: `man coreutils`.\n\n```bash\nls -la /etc\n```\n")),
            (Lesson(linux, "linux-basic-permissions", "Permisos y usuarios", "basic", "permisos",
                "# Permisos y usuarios\n\n`chmod`, `chown`, `sudo` y el significado de `rwx`. Fuente oficial: `man chmod`.\n")),
            (Lesson(linux, "linux-intermediate-systemd", "systemd y servicios", "intermediate", "systemd",
                "# systemd\n\n`systemctl status`, `journalctl -u` y unit files. Fuente oficial: `man systemd.unit`.\n")),
            (Lesson(git, "git-basic-workflow", "Flujo basico con Git", "basic", "flujo",
                "# Flujo basico\n\n`git init`, `add`, `commit`, `branch`, `merge`. Fuente oficial: `git-scm.com/docs`.\n")),
            (Lesson(docker, "docker-basic-containers", "Contenedores basicos", "basic", "contenedores",
                "# Contenedores basicos\n\n`docker run`, `ps`, `exec`, `logs`. Fuente oficial: `docs.docker.com`.\n"))
        };
        db.KnowledgeNodes.AddRange(lessons);

        var linuxLab = new LabEnvironment
        {
            Id = Ulid.NewUlid(),
            Name = "Linux: terminal basica",
            Slug = "linux-terminal-basica",
            Description = "Practica comandos basicos en una terminal Ubuntu real.",
            LabType = LabType.Guided,
            BaseImage = "ubuntu:24.04",
            ValidationScript = "test -f /home/lab/.ejercicio && echo OK",
            Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { technology = "linux", topic = "cli", level = "basic", estimatedTimeMinutes = 30 })),
            IsActive = true
        };
        db.LabEnvironments.Add(linuxLab);

        var exam = new Exam
        {
            Id = Ulid.NewUlid(),
            Technology = "linux",
            Slug = "linux-basic",
            Title = "Linux basico: examen de nivel",
            Description = "20 preguntas de terminal, permisos y procesos.",
            Level = "basic",
            PassingScore = 70,
            TimeLimitMinutes = 30,
            QuestionCount = 3,
            QuestionSelection = JsonDocument.Parse("{}"),
            IsActive = true
        };
        db.Exams.Add(exam);

        var questions = new[]
        {
            MkQuestion("linux", "cli", "basic", Difficulty.Beginner, QuestionType.SingleChoice,
                "Que comando lista archivos incluyendo ocultos?",
                new[] { "ls", "ls -a", "list", "dir /a" }, "ls -a",
                "Fuente: man ls. La opcion -a muestra entradas que empiezan con punto."),
            MkQuestion("linux", "cli", "basic", Difficulty.Beginner, QuestionType.SingleChoice,
                "Que comando muestra el directorio actual?",
                new[] { "pwd", "cd", "whoami", "dir" }, "pwd",
                "Fuente: man pwd."),
            MkQuestion("linux", "permisos", "basic", Difficulty.Beginner, QuestionType.TrueFalse,
                "El permiso octal 755 en un directorio permite listar su contenido al resto de usuarios.",
                new[] { "true", "false" }, "true",
                "Fuente: man chmod. r-x para group/other permite listar y entrar.")
        };
        db.Questions.AddRange(questions);

        var cert = new Certification
        {
            Id = Ulid.NewUlid(),
            Code = "LINUX-ESS",
            Name = "Linux Essentials (simulacro)",
            Vendor = "Comunidad",
            Version = "1.0",
            Domains = JsonDocument.Parse(JsonSerializer.Serialize(new[] { new { name = "cli", weight = 60 }, new { name = "permisos", weight = 40 } })),
            IsActive = true
        };
        db.Certifications.Add(cert);

        // Mappings de dominios del simulacro a lecciones (base del readiness)
        var cliLessons = lessons.Where(l => l.Slug is "linux-basic-cli" or "linux-intermediate-systemd").ToList();
        var permLessons = lessons.Where(l => l.Slug == "linux-basic-permissions").ToList();
        foreach (var l in cliLessons)
            db.CertificationMappings.Add(MkMapping(cert, "cli", 60, l));
        foreach (var l in permLessons)
            db.CertificationMappings.Add(MkMapping(cert, "permisos", 40, l));

        db.TechnologySources.AddRange([
            new TechnologySource
            {
                Id = Ulid.NewUlid(),
                Technology = "docker",
                SourceType = "github_releases",
                SourceUrl = "moby/moby",
                IsActive = true
            },
            new TechnologySource
            {
                Id = Ulid.NewUlid(),
                Technology = "ubuntu",
                SourceType = "docker_tags",
                SourceUrl = "library/ubuntu",
                IsActive = true
            }
        ]);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Contenido inicial insertado.");
    }

    private static KnowledgeNode TechNode(string slug, string title, string desc, string version) => new()
    {
        Id = Ulid.NewUlid(),
        Type = NodeType.Technology,
        Slug = slug,
        Title = title,
        Version = version,
        Content = JsonDocument.Parse(JsonSerializer.Serialize(new { description = desc })),
        Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { technology = slug }))
    };

    private static KnowledgeNode Lesson(KnowledgeNode parent, string slug, string title, string level, string topic, string markdown) => new()
    {
        Id = Ulid.NewUlid(),
        Type = NodeType.Lesson,
        Slug = slug,
        Title = title,
        ParentId = parent.Id,
        Content = JsonDocument.Parse(JsonSerializer.Serialize(new { markdown })),
        Metadata = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            technology = parent.Slug,
            topic,
            level
        }))
    };

    private static CertificationMapping MkMapping(Certification cert, string domain, decimal domainWeight, KnowledgeNode node) => new()
    {
        Id = Ulid.NewUlid(),
        CertificationId = cert.Id,
        DomainName = domain,
        DomainWeight = domainWeight,
        NodeId = node.Id,
        CoverageWeight = 1
    };

    private static Question MkQuestion(
        string technology, string? topic, string level, Difficulty difficulty,
        QuestionType type, string prompt, string[] options, string correct, string? explanation) => new()
        {
            Id = Ulid.NewUlid(),
            Technology = technology,
            Topic = topic,
            Level = level,
            Difficulty = difficulty,
            QuestionType = type,
            Prompt = prompt,
            Options = JsonDocument.Parse(JsonSerializer.Serialize(options)),
            CorrectAnswer = JsonDocument.Parse(JsonSerializer.Serialize(correct)),
            Explanation = explanation,
            OfficialSource = "Documentacion oficial de la tecnologia",
            IsActive = true
        };
}
