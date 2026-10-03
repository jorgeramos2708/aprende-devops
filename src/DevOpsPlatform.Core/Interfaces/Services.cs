namespace DevOpsPlatform.Core.Interfaces;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Models;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public record AuthSession(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt,
    UserDto User);

public interface ITokenService
{
    Task<AuthSession> CreateSessionAsync(ApplicationUser user, CancellationToken ct = default);
    Task<AuthSession?> RefreshSessionAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeSessionAsync(string refreshToken, CancellationToken ct = default);
}

public interface IProgressService
{
    Task<UserProgressDto> UpdateProgressAsync(Ulid userId, ProgressUpdateRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<UserProgressDto>> GetProgressAsync(Ulid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillAssessmentDto>> GetSkillsAsync(Ulid userId, CancellationToken ct = default);
}

public interface IExamService
{
    Task<IReadOnlyList<Exam>> GetExamsAsync(CancellationToken ct = default);
    Task<Exam?> GetExamAsync(Ulid id, CancellationToken ct = default);
    Task<ExamStartResponse> StartExamAsync(Ulid userId, Ulid examId, CancellationToken ct = default);
    Task<ExamResultDto> SubmitExamAsync(Ulid userId, ExamSubmitRequest req, CancellationToken ct = default);
    Task<ExamResultDto?> GetResultAsync(Ulid attemptId, Ulid userId, CancellationToken ct = default);
}

public interface ICertificationService
{
    Task<IReadOnlyList<Certification>> GetCertificationsAsync(CancellationToken ct = default);
    Task<CertificationReadinessDto?> GetReadinessAsync(Ulid userId, Ulid certificationId, CancellationToken ct = default);
}

public interface IInsightService
{
    Task<IReadOnlyList<InsightDto>> GetInsightsAsync(Ulid userId, CancellationToken ct = default);
    Task MarkReadAsync(Ulid userId, Ulid id, CancellationToken ct = default);
    Task DismissAsync(Ulid userId, Ulid id, CancellationToken ct = default);
    Task<Insight> CreateAsync(Insight insight, CancellationToken ct = default);
}

public record WatchSummary(int SourcesChecked, int ChangesDetected);

public interface ITechnologyWatchService
{
    Task<WatchSummary> CheckAllAsync(string? technologyFilter, CancellationToken ct = default);
}

public interface IImpactService
{
    Task<IReadOnlyList<ImpactAssessment>> ListAsync(CancellationToken ct = default);
    Task<ImpactAssessment> EnsureForChangeAsync(Ulid changeId, CancellationToken ct = default);
}

public interface IUpdateService
{
    Task<IReadOnlyList<UpdateProposal>> ListAsync(CancellationToken ct = default);
    Task<UpdateProposal> CreateAsync(Ulid changeId, CreateUpdateProposalRequest req, Ulid createdBy, CancellationToken ct = default);
    Task<UpdateProposal> ApproveAsync(Ulid id, Ulid reviewedBy, CancellationToken ct = default);
}

public interface IRegressionService
{
    Task<IReadOnlyList<LabRegressionRun>> RunAsync(string? suiteId, string technologyVersion, CancellationToken ct = default);
    Task<IReadOnlyList<LabRegressionRun>> RecentRunsAsync(int take, CancellationToken ct = default);
}
