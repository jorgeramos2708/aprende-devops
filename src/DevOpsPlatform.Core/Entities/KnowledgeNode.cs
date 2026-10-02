namespace DevOpsPlatform.Core.Entities;

using System.Text.Json;
using System.Text.Json.Serialization;
using DevOpsPlatform.Core.Enums;

public abstract record Entity<TId> where TId : notnull
{
    public TId Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public record KnowledgeNode : Entity<Ulid>
{
    public NodeType Type { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public JsonDocument? Content { get; init; }
    public string? Version { get; init; }
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
    public Ulid? ParentId { get; init; }
    public KnowledgeNode? Parent { get; init; }
    public ICollection<KnowledgeNode> Children { get; init; } = new List<KnowledgeNode>();
    public ICollection<KnowledgeEdge> OutgoingEdges { get; init; } = new List<KnowledgeEdge>();
    public ICollection<KnowledgeEdge> IncomingEdges { get; init; } = new List<KnowledgeEdge>();
}

public record KnowledgeEdge
{
    public Ulid FromId { get; init; }
    public KnowledgeNode FromNode { get; init; } = null!;
    public Ulid ToId { get; init; }
    public KnowledgeNode ToNode { get; init; } = null!;
    public EdgeType EdgeType { get; init; }
    public int Weight { get; init; } = 1;
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public record LabEnvironment : Entity<Ulid>
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; set; }
    public LabType LabType { get; set; }
    public string BaseImage { get; set; } = string.Empty;
    public string? DockerCompose { get; set; }
    public JsonDocument ResourceLimits { get; set; } = JsonDocument.Parse("{}");
    public string? ValidationScript { get; set; }
    public string? SetupScript { get; set; }
    public string? CleanupScript { get; set; }
    public JsonDocument Metadata { get; set; } = JsonDocument.Parse("{}");
    public bool IsActive { get; set; } = true;
    public ICollection<LabAttempt> Attempts { get; init; } = new List<LabAttempt>();
}

public record LabAttempt : Entity<Ulid>
{
    public Ulid UserId { get; init; }
    public Ulid LabEnvironmentId { get; init; }
    public LabStatus Status { get; set; } = LabStatus.Pending;
    public string? ContainerId { get; set; }
    public string? ContainerIp { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public decimal? Score { get; set; }
    public JsonDocument? Evidence { get; set; }
    public JsonDocument? ValidationResult { get; set; }
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
    public LabEnvironment LabEnvironment { get; init; } = null!;
}

public record Question : Entity<Ulid>
{
    public string Technology { get; init; } = string.Empty;
    public string? Topic { get; init; }
    public string? Subtopic { get; init; }
    public string Level { get; init; } = string.Empty; // basic, intermediate, advanced
    public Difficulty Difficulty { get; init; }
    public string? Competency { get; init; }
    public QuestionType QuestionType { get; init; }
    public string Prompt { get; init; } = string.Empty; // markdown
    public JsonDocument? Options { get; init; }
    public JsonDocument CorrectAnswer { get; init; } = null!;
    public string? Explanation { get; init; }
    public string? OfficialSource { get; init; }
    public string? TechnologyVersion { get; init; }
    public string[] Tags { get; init; } = [];
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
    public bool IsActive { get; init; } = true;
    public ICollection<QuestionVersion> Versions { get; init; } = new List<QuestionVersion>();
}

public record QuestionVersion : Entity<Ulid>
{
    public Ulid QuestionId { get; init; }
    public Question Question { get; init; } = null!;
    public int Version { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public JsonDocument? Options { get; init; }
    public JsonDocument CorrectAnswer { get; init; } = null!;
    public string? Explanation { get; init; }
    public string? TechnologyVersion { get; init; }
    public Ulid? ChangedBy { get; init; }
    public string? ChangeReason { get; init; }
}

public record Exam : Entity<Ulid>
{
    public string? Technology { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Level { get; init; } // basic, intermediate, advanced, final, super
    public decimal PassingScore { get; init; } = 70;
    public int? TimeLimitMinutes { get; init; }
    public int QuestionCount { get; init; }
    public JsonDocument QuestionSelection { get; init; } = JsonDocument.Parse("{}");
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
    public bool IsActive { get; init; } = true;
    public ICollection<ExamAttempt> Attempts { get; init; } = new List<ExamAttempt>();
}

public record ExamAttempt : Entity<Ulid>
{
    public Ulid UserId { get; init; }
    public Ulid ExamId { get; init; }
    public string Status { get; init; } = "in_progress"; // in_progress, submitted, graded, reviewed
    public decimal? Score { get; init; }
    public decimal? MaxScore { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? GradedAt { get; init; }
    public JsonDocument? Answers { get; init; }
    public JsonDocument? GradingDetails { get; init; }
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
    public Exam Exam { get; init; } = null!;
}

public record Certification : Entity<Ulid>
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Vendor { get; init; } = string.Empty;
    public string? Url { get; init; }
    public string? Version { get; init; }
    public JsonDocument Domains { get; init; } = JsonDocument.Parse("{}");
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
    public bool IsActive { get; init; } = true;
    public ICollection<CertificationMapping> Mappings { get; init; } = new List<CertificationMapping>();
}

public record CertificationMapping : Entity<Ulid>
{
    public Ulid CertificationId { get; init; }
    public Certification Certification { get; init; } = null!;
    public string DomainName { get; init; } = string.Empty;
    public decimal DomainWeight { get; init; }
    public Ulid NodeId { get; init; }
    public KnowledgeNode Node { get; init; } = null!;
    public decimal CoverageWeight { get; init; } = 1;
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
}

public record TechnologySource : Entity<Ulid>
{
    public string Technology { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public JsonDocument? Selectors { get; init; }
    public JsonDocument? Headers { get; init; }
    public string ScheduleCron { get; init; } = "0 */6 * * *";
    public DateTimeOffset? LastCheckedAt { get; init; }
    public string? LastVersion { get; init; }
    public string? LastContentHash { get; init; }
    public bool IsActive { get; init; } = true;
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
}

public record TechnologyChange : Entity<Ulid>
{
    public string Technology { get; init; } = string.Empty;
    public Ulid? SourceId { get; init; }
    public string ChangeType { get; init; } = string.Empty;
    public string? PreviousVersion { get; init; }
    public string? NewVersion { get; init; }
    public string Summary { get; init; } = string.Empty;
    public JsonDocument Details { get; init; } = JsonDocument.Parse("{}");
    public string Severity { get; init; } = "medium";
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; init; }
    public string Status { get; init; } = "pending";
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
}

public record ImpactAssessment : Entity<Ulid>
{
    public Ulid ChangeId { get; init; }
    public TechnologyChange Change { get; init; } = null!;
    public JsonDocument AffectedNodes { get; init; } = JsonDocument.Parse("[]");
    public JsonDocument AffectedLabs { get; init; } = JsonDocument.Parse("[]");
    public JsonDocument AffectedQuestions { get; init; } = JsonDocument.Parse("[]");
    public JsonDocument AffectedExams { get; init; } = JsonDocument.Parse("[]");
    public JsonDocument AffectedCertMappings { get; init; } = JsonDocument.Parse("[]");
    public string OverallImpact { get; init; } = "low";
    public string? Recommendation { get; init; }
    public DateTimeOffset AssessedAt { get; init; } = DateTimeOffset.UtcNow;
    public Ulid? AssessedBy { get; init; }
}

public record UpdateProposal : Entity<Ulid>
{
    public Ulid ChangeId { get; init; }
    public TechnologyChange Change { get; init; } = null!;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public JsonDocument ProposedChanges { get; init; } = JsonDocument.Parse("[]");
    public string Status { get; init; } = "draft";
    public Ulid? CreatedBy { get; init; }
    public Ulid? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public DateTimeOffset? AppliedAt { get; init; }
}

public record LabRegressionSuite : Entity<Ulid>
{
    public Ulid LabEnvironmentId { get; init; }
    public LabEnvironment LabEnvironment { get; init; } = null!;
    public string Name { get; init; } = string.Empty;
    public string TestScript { get; init; } = string.Empty;
    public JsonDocument ExpectedResult { get; init; } = JsonDocument.Parse("{}");
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
    public bool IsActive { get; init; } = true;
    public ICollection<LabRegressionRun> Runs { get; init; } = new List<LabRegressionRun>();
}

public record LabRegressionRun : Entity<Ulid>
{
    public Ulid SuiteId { get; init; }
    public LabRegressionSuite Suite { get; init; } = null!;
    public string TechnologyVersion { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty; // pass, warning, fail, error
    public string? Output { get; init; }
    public JsonDocument? Artifacts { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
}

public record UserSkill : Entity<Ulid>
{
    public Ulid UserId { get; init; }
    public string Technology { get; init; } = string.Empty;
    public string? Topic { get; init; }
    public string Level { get; init; } = string.Empty;
    public decimal Proficiency { get; init; } = 0;
    public decimal Confidence { get; init; } = 0;
    public int EvidenceCount { get; init; } = 0;
    public DateTimeOffset? LastAssessedAt { get; init; }
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
}

public record Insight : Entity<Ulid>
{
    public Ulid UserId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Ulid[] RelatedNodeIds { get; init; } = [];
    public int Priority { get; init; } = 5;
    public bool IsRead { get; init; } = false;
    public bool IsDismissed { get; init; } = false;
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
    public DateTimeOffset? ExpiresAt { get; init; }
}

public record AuditLog : Entity<Ulid>
{
    public Ulid? UserId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? EntityType { get; init; }
    public Ulid? EntityId { get; init; }
    public JsonDocument? OldValue { get; init; }
    public JsonDocument? NewValue { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
}

public record Notification : Entity<Ulid>
{
    public Ulid UserId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Message { get; init; }
    public string? ActionUrl { get; init; }
    public bool IsRead { get; init; } = false;
    public JsonDocument Metadata { get; init; } = JsonDocument.Parse("{}");
}
