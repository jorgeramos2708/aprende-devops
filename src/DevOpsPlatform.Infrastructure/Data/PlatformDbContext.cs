namespace DevOpsPlatform.Infrastructure.Data;

using DevOpsPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Contexto principal de la plataforma. Fuente de verdad del esquema:
/// las migraciones EF Core (carpeta Migrations) definen y versionan la base.
/// Convenciones: ids Guid v7 generados en app (Guid.CreateVersion7),
/// enums como text, propiedades JsonDocument como jsonb (Npgsql).
/// </summary>
public class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options) : base(options) { }

    // Identidad
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Grafo de conocimiento
    public DbSet<KnowledgeNode> KnowledgeNodes => Set<KnowledgeNode>();
    public DbSet<KnowledgeEdge> KnowledgeEdges => Set<KnowledgeEdge>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();

    // Labs
    public DbSet<LabEnvironment> LabEnvironments => Set<LabEnvironment>();
    public DbSet<LabAttempt> LabAttempts => Set<LabAttempt>();

    // Evaluacion
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();

    // Certificaciones oficiales
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<CertificationMapping> CertificationMappings => Set<CertificationMapping>();

    // Vigilancia de versiones (TechWatcher)
    public DbSet<TechnologySource> TechnologySources => Set<TechnologySource>();
    public DbSet<TechnologyChange> TechnologyChanges => Set<TechnologyChange>();
    public DbSet<ImpactAssessment> ImpactAssessments => Set<ImpactAssessment>();
    public DbSet<UpdateProposal> UpdateProposals => Set<UpdateProposal>();

    // Regresion de labs
    public DbSet<LabRegressionSuite> LabRegressionSuites => Set<LabRegressionSuite>();
    public DbSet<LabRegressionRun> LabRegressionRuns => Set<LabRegressionRun>();

    // Habilidades e insights
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<Insight> Insights => Set<Insight>();

    // Auditoria y notificaciones
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("pg_trgm");

        // ---------- Identidad ----------
        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
        });

        // ---------- Grafo de conocimiento ----------
        modelBuilder.Entity<KnowledgeNode>(e =>
        {
            e.Property(n => n.Type).HasConversion<string>();
            e.Property(n => n.Content).HasColumnType("jsonb");
            e.Property(n => n.Metadata).HasColumnType("jsonb");
            e.HasOne(n => n.Parent)
                .WithMany(n => n.Children)
                .HasForeignKey(n => n.ParentId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(n => n.ParentId);
            e.HasIndex(n => new { n.Type, n.Slug, n.Version }).IsUnique();
        });
        modelBuilder.Entity<KnowledgeEdge>(e =>
        {
            e.HasKey(x => new { x.FromId, x.ToId, x.EdgeType });
            e.Property(x => x.EdgeType).HasConversion<string>();
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasOne(x => x.FromNode)
                .WithMany(n => n.OutgoingEdges)
                .HasForeignKey(x => x.FromId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ToNode)
                .WithMany(n => n.IncomingEdges)
                .HasForeignKey(x => x.ToId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserProgress>(e =>
        {
            e.HasOne(p => p.Node)
                .WithMany()
                .HasForeignKey(p => p.NodeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.UserId, p.NodeId }).IsUnique();
        });

        // ---------- Labs ----------
        modelBuilder.Entity<LabEnvironment>(e =>
        {
            e.Property(l => l.LabType).HasConversion<string>();
            e.Property(l => l.ResourceLimits).HasColumnType("jsonb");
            e.Property(l => l.Metadata).HasColumnType("jsonb");
            e.HasIndex(l => l.Slug).IsUnique();
        });
        modelBuilder.Entity<LabAttempt>(e =>
        {
            e.Property(a => a.Status).HasConversion<string>();
            e.Property(a => a.Evidence).HasColumnType("jsonb");
            e.Property(a => a.ValidationResult).HasColumnType("jsonb");
            e.Property(a => a.Metadata).HasColumnType("jsonb");
            // Sin cascade: conservar historial de intentos aunque el lab se desactive
            e.HasOne(a => a.LabEnvironment)
                .WithMany(l => l.Attempts)
                .HasForeignKey(a => a.LabEnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => a.UserId);
        });

        // ---------- Evaluacion ----------
        modelBuilder.Entity<Question>(e =>
        {
            e.Property(q => q.Difficulty).HasConversion<string>();
            e.Property(q => q.QuestionType).HasConversion<string>();
            e.Property(q => q.Options).HasColumnType("jsonb");
            e.Property(q => q.CorrectAnswer).HasColumnType("jsonb");
            e.Property(q => q.Metadata).HasColumnType("jsonb");
            e.HasIndex(q => new { q.Technology, q.Level });
        });
        modelBuilder.Entity<QuestionVersion>(e =>
        {
            e.Property(q => q.Options).HasColumnType("jsonb");
            e.Property(q => q.CorrectAnswer).HasColumnType("jsonb");
            e.HasOne(q => q.Question)
                .WithMany(q => q.Versions)
                .HasForeignKey(q => q.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(q => new { q.QuestionId, q.Version }).IsUnique();
        });
        modelBuilder.Entity<Exam>(e =>
        {
            e.Property(x => x.QuestionSelection).HasColumnType("jsonb");
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasIndex(x => x.Slug).IsUnique();
        });
        modelBuilder.Entity<ExamAttempt>(e =>
        {
            e.Property(a => a.Answers).HasColumnType("jsonb");
            e.Property(a => a.GradingDetails).HasColumnType("jsonb");
            e.Property(a => a.Metadata).HasColumnType("jsonb");
            e.HasOne(a => a.Exam)
                .WithMany(x => x.Attempts)
                .HasForeignKey(a => a.ExamId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => a.UserId);
        });

        // ---------- Certificaciones ----------
        modelBuilder.Entity<Certification>(e =>
        {
            e.Property(c => c.Domains).HasColumnType("jsonb");
            e.Property(c => c.Metadata).HasColumnType("jsonb");
            e.HasIndex(c => c.Code).IsUnique();
        });
        modelBuilder.Entity<CertificationMapping>(e =>
        {
            e.Property(m => m.Metadata).HasColumnType("jsonb");
            e.HasOne(m => m.Certification)
                .WithMany(c => c.Mappings)
                .HasForeignKey(m => m.CertificationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Node)
                .WithMany()
                .HasForeignKey(m => m.NodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- TechWatcher ----------
        modelBuilder.Entity<TechnologySource>(e =>
        {
            e.Property(s => s.Selectors).HasColumnType("jsonb");
            e.Property(s => s.Headers).HasColumnType("jsonb");
            e.Property(s => s.Metadata).HasColumnType("jsonb");
        });
        modelBuilder.Entity<TechnologyChange>(e =>
        {
            e.Property(c => c.Details).HasColumnType("jsonb");
            e.Property(c => c.Metadata).HasColumnType("jsonb");
            e.HasIndex(c => c.Technology);
        });
        modelBuilder.Entity<ImpactAssessment>(e =>
        {
            e.Property(a => a.AffectedNodes).HasColumnType("jsonb");
            e.Property(a => a.AffectedLabs).HasColumnType("jsonb");
            e.Property(a => a.AffectedQuestions).HasColumnType("jsonb");
            e.Property(a => a.AffectedExams).HasColumnType("jsonb");
            e.Property(a => a.AffectedCertMappings).HasColumnType("jsonb");
            e.HasOne(a => a.Change)
                .WithMany()
                .HasForeignKey(a => a.ChangeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UpdateProposal>(e =>
        {
            e.Property(p => p.ProposedChanges).HasColumnType("jsonb");
            e.HasOne(p => p.Change)
                .WithMany()
                .HasForeignKey(p => p.ChangeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Regresion de labs ----------
        modelBuilder.Entity<LabRegressionSuite>(e =>
        {
            e.Property(s => s.ExpectedResult).HasColumnType("jsonb");
            e.Property(s => s.Metadata).HasColumnType("jsonb");
            e.HasOne(s => s.LabEnvironment)
                .WithMany()
                .HasForeignKey(s => s.LabEnvironmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<LabRegressionRun>(e =>
        {
            e.Property(r => r.Artifacts).HasColumnType("jsonb");
            e.Property(r => r.Metadata).HasColumnType("jsonb");
            e.HasOne(r => r.Suite)
                .WithMany(s => s.Runs)
                .HasForeignKey(r => r.SuiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Habilidades e insights ----------
        modelBuilder.Entity<UserSkill>(e =>
        {
            e.Property(s => s.Metadata).HasColumnType("jsonb");
            e.HasIndex(s => new { s.UserId, s.Technology, s.Topic, s.Level }).IsUnique();
        });
        modelBuilder.Entity<Insight>(e =>
        {
            e.Property(i => i.Metadata).HasColumnType("jsonb");
            e.HasIndex(i => i.UserId);
        });

        // ---------- Auditoria y notificaciones ----------
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.OldValue).HasColumnType("jsonb");
            e.Property(a => a.NewValue).HasColumnType("jsonb");
            e.Property(a => a.Metadata).HasColumnType("jsonb");
            e.HasIndex(a => a.CreatedAt);
        });
        modelBuilder.Entity<Notification>(e =>
        {
            e.Property(n => n.Metadata).HasColumnType("jsonb");
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });
    }
}
