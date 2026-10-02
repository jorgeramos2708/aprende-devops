namespace DevOpsPlatform.Core.Models;

using DevOpsPlatform.Core.Enums;
using Ulid;

public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record LabStartRequest
{
    public Ulid LabEnvironmentId { get; init; }
}

public record LabStartResponse
{
    public Ulid AttemptId { get; init; }
    public string ContainerId { get; init; } = string.Empty;
    public string WebSocketUrl { get; init; } = string.Empty;
    public string SessionToken { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public int Cols { get; init; } = 120;
    public int Rows { get; init; } = 30;
}

public record TerminalResizeRequest
{
    public int Cols { get; init; }
    public int Rows { get; init; }
}

public record TerminalInput
{
    public string Data { get; init; } = string.Empty;
}

public record TerminalOutput
{
    public string Data { get; init; } = string.Empty;
    public bool IsError { get; init; } = false;
}

public record ExamStartRequest
{
    public Ulid ExamId { get; init; }
}

public record ExamStartResponse
{
    public Ulid AttemptId { get; init; }
    public IReadOnlyList<ExamQuestionDto> Questions { get; init; } = Array.Empty<ExamQuestionDto>();
    public DateTimeOffset ExpiresAt { get; init; }
}

public record ExamQuestionDto
{
    public Ulid QuestionId { get; init; }
    public int Order { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public QuestionType QuestionType { get; init; }
    public JsonElement? Options { get; init; }
    public decimal MaxScore { get; init; }
}

public record ExamSubmitRequest
{
    public Ulid AttemptId { get; init; }
    public IReadOnlyList<ExamAnswerDto> Answers { get; init; } = Array.Empty<ExamAnswerDto>();
}

public record ExamAnswerDto
{
    public Ulid QuestionId { get; init; }
    public JsonElement Answer { get; init; }
    public int TimeSpentSeconds { get; init; }
}

public record ExamResultDto
{
    public Ulid AttemptId { get; init; }
    public decimal Score { get; init; }
    public decimal MaxScore { get; init; }
    public decimal Percentage { get; init; }
    public bool Passed { get; init; }
    public IReadOnlyList<QuestionResultDto> QuestionResults { get; init; } = Array.Empty<QuestionResultDto>();
    public DateTimeOffset GradedAt { get; init; }
}

public record QuestionResultDto
{
    public Ulid QuestionId { get; init; }
    public bool Correct { get; init; }
    public decimal Score { get; init; }
    public decimal MaxScore { get; init; }
    public string? Explanation { get; init; }
    public JsonElement? UserAnswer { get; init; }
    public JsonElement? CorrectAnswer { get; init; }
}

public record ProgressUpdateRequest
{
    public Ulid NodeId { get; init; }
    public string Status { get; init; } = string.Empty; // not_started, in_progress, completed, mastered
    public decimal? Score { get; init; }
    public int TimeSpentSeconds { get; init; } = 0;
}

public record UserProgressDto
{
    public Ulid NodeId { get; init; }
    public string NodeTitle { get; init; } = string.Empty;
    public NodeType NodeType { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal? Score { get; init; }
    public int Attempts { get; init; }
    public long TimeSpentSeconds { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset LastAccessedAt { get; init; }
}

public record SkillAssessmentDto
{
    public string Technology { get; init; } = string.Empty;
    public string? Topic { get; init; }
    public string Level { get; init; } = string.Empty;
    public decimal Proficiency { get; init; }
    public decimal Confidence { get; init; }
    public int EvidenceCount { get; init; }
    public IReadOnlyList<Ulid> RelatedNodeIds { get; init; } = Array.Empty<Ulid>();
    public DateTimeOffset? LastAssessedAt { get; init; }
}

public record InsightDto
{
    public Ulid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Priority { get; init; }
    public bool IsRead { get; init; }
    public bool IsDismissed { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public record CertificationReadinessDto
{
    public Ulid CertificationId { get; init; }
    public string CertificationCode { get; init; } = string.Empty;
    public string CertificationName { get; init; } = string.Empty;
    public decimal OverallReadiness { get; init; }
    public IReadOnlyList<DomainReadinessDto> Domains { get; init; } = Array.Empty<DomainReadinessDto>();
    public IReadOnlyList<Ulid> RecommendedNodes { get; init; } = Array.Empty<Ulid>();
}

public record DomainReadinessDto
{
    public string DomainName { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public decimal Readiness { get; init; }
    public IReadOnlyList<Ulid> CoveredNodes { get; init; } = Array.Empty<Ulid>();
    public IReadOnlyList<Ulid> MissingNodes { get; init; } = Array.Empty<Ulid>();
}

public record TechChangeDto
{
    public Ulid Id { get; init; }
    public string Technology { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
    public string? PreviousVersion { get; init; }
    public string? NewVersion { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset DetectedAt { get; init; }
    public IReadOnlyList<AffectedItemDto> AffectedItems { get; init; } = Array.Empty<AffectedItemDto>();
}

public record AffectedItemDto
{
    public Ulid NodeId { get; init; }
    public string NodeTitle { get; init; } = string.Empty;
    public NodeType NodeType { get; init; }
    public string ImpactLevel { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public record UpdateProposalDto
{
    public Ulid Id { get; init; }
    public Ulid ChangeId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<ProposedChangeDto> ProposedChanges { get; init; } = Array.Empty<ProposedChangeDto>();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public DateTimeOffset? AppliedAt { get; init; }
}

public record ProposedChangeDto
{
    public Ulid NodeId { get; init; }
    public string NodeTitle { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty; // update, replace, deprecate
    public JsonElement? NewContent { get; init; }
}
