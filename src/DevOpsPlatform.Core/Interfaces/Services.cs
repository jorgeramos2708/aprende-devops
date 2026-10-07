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
    Task<UserProgressDto> UpdateProgressAsync(Guid userId, ProgressUpdateRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<UserProgressDto>> GetProgressAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillAssessmentDto>> GetSkillsAsync(Guid userId, CancellationToken ct = default);
}

public interface IExamService
{
    Task<IReadOnlyList<Exam>> GetExamsAsync(CancellationToken ct = default);
    Task<Exam?> GetExamAsync(Guid id, CancellationToken ct = default);
    Task<ExamStartResponse> StartExamAsync(Guid userId, Guid examId, CancellationToken ct = default);
    Task<ExamResultDto> SubmitExamAsync(Guid userId, ExamSubmitRequest req, CancellationToken ct = default);
    Task<ExamResultDto?> GetResultAsync(Guid attemptId, Guid userId, CancellationToken ct = default);
}

public interface ICertificationService
{
    Task<IReadOnlyList<Certification>> GetCertificationsAsync(CancellationToken ct = default);
    Task<CertificationReadinessDto?> GetReadinessAsync(Guid userId, Guid certificationId, CancellationToken ct = default);
}

public interface IInsightService
{
    Task<IReadOnlyList<InsightDto>> GetInsightsAsync(Guid userId, CancellationToken ct = default);
    Task MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task DismissAsync(Guid userId, Guid id, CancellationToken ct = default);
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
    Task<ImpactAssessment> EnsureForChangeAsync(Guid changeId, CancellationToken ct = default);
}

public interface IUpdateService
{
    Task<IReadOnlyList<UpdateProposal>> ListAsync(CancellationToken ct = default);
    Task<UpdateProposal> CreateAsync(Guid changeId, CreateUpdateProposalRequest req, Guid createdBy, CancellationToken ct = default);
    Task<UpdateProposal> ApproveAsync(Guid id, Guid reviewedBy, CancellationToken ct = default);
}

public interface IRegressionService
{
    Task<IReadOnlyList<LabRegressionRun>> RunAsync(string? suiteId, string technologyVersion, CancellationToken ct = default);
    Task<IReadOnlyList<LabRegressionRun>> RecentRunsAsync(int take, CancellationToken ct = default);
}
