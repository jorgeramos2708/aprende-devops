namespace DevOpsPlatform.Core.Interfaces;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Enums;
using DevOpsPlatform.Core.Models;
using System.Text.Json;

public interface IKnowledgeGraphRepository
{
    Task<KnowledgeNode?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeNode?> GetBySlugAsync(string type, string slug, string? version = null, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeNode>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeNode>> GetByTypeAsync(NodeType type, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeEdge>> GetOutgoingEdgesAsync(Guid nodeId, EdgeType? edgeType = null, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeEdge>> GetIncomingEdgesAsync(Guid nodeId, EdgeType? edgeType = null, CancellationToken ct = default);
    Task<KnowledgeNode> AddAsync(KnowledgeNode node, CancellationToken ct = default);
    Task<KnowledgeNode> UpdateAsync(KnowledgeNode node, CancellationToken ct = default);
    Task AddEdgeAsync(KnowledgeEdge edge, CancellationToken ct = default);
    Task RemoveEdgeAsync(Guid fromId, Guid toId, EdgeType edgeType, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetPrerequisitesAsync(Guid nodeId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetDependentsAsync(Guid nodeId, CancellationToken ct = default);
}

public interface ILabOrchestrator
{
    Task<LabSession> StartLabAsync(Guid labEnvironmentId, Guid userId, CancellationToken ct = default);
    Task<LabSession?> GetSessionAsync(Guid attemptId, CancellationToken ct = default);
    /// <summary>Token one-shot de terminal. Exige que el intento pertenezca al usuario.</summary>
    Task<TerminalConnection> ConnectTerminalAsync(Guid attemptId, Guid userId, int cols, int rows, CancellationToken ct = default);
    /// <summary>Solo uso interno (WS ya autenticado por token Redis): asegura sesion sin emitir token.</summary>
    Task EnsureTerminalSessionAsync(Guid attemptId, int cols, int rows, CancellationToken ct = default);
    Task SendTerminalInputAsync(Guid attemptId, string input, CancellationToken ct = default);
    Task ResizeTerminalAsync(Guid attemptId, int cols, int rows, CancellationToken ct = default);
    Task<LabValidationResult> ValidateLabAsync(Guid attemptId, CancellationToken ct = default);
    Task StopLabAsync(Guid attemptId, CancellationToken ct = default);
    Task CleanupExpiredLabsAsync(CancellationToken ct = default);
}

public record LabSession
{
    public Guid AttemptId { get; init; }
    public string ContainerId { get; init; } = string.Empty;
    public string ContainerIp { get; init; } = string.Empty;
    public int TerminalPort { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public LabStatus Status { get; init; }
}

public record TerminalConnection
{
    public string WebSocketUrl { get; init; } = string.Empty;
    public string SessionToken { get; init; } = string.Empty;
    public int Cols { get; init; }
    public int Rows { get; init; }
}

public record LabValidationResult
{
    public bool Passed { get; init; }
    public decimal Score { get; init; }
    public JsonDocument Evidence { get; init; } = JsonDocument.Parse("{}");
    public JsonDocument Details { get; init; } = JsonDocument.Parse("{}");
    public string[] Warnings { get; init; } = [];
    public string[] Errors { get; init; } = [];
}

public interface IContainerRuntime
{
    Task<string> CreateContainerAsync(LabEnvironment lab, Guid attemptId, CancellationToken ct = default);
    Task<bool> StartContainerAsync(string containerId, CancellationToken ct = default);
    Task<bool> StopContainerAsync(string containerId, CancellationToken ct = default);
    Task<bool> RemoveContainerAsync(string containerId, CancellationToken ct = default);
    Task<ContainerExecResult> ExecAsync(string containerId, string[] command, CancellationToken ct = default);
    Task<Stream> AttachTerminalAsync(string containerId, int cols, int rows, CancellationToken ct = default);
    /// <summary>Sesion de shell interactiva persistente (exec con TTY). Devuelve stream + execId para resize.</summary>
    Task<TerminalExecSession> AttachExecShellAsync(string containerId, int cols, int rows, CancellationToken ct = default);
    Task<bool> ResizeExecAsync(string execId, int cols, int rows, CancellationToken ct = default);
    Task<ContainerInfo?> GetContainerInfoAsync(string containerId, CancellationToken ct = default);
    Task<IReadOnlyList<ContainerInfo>> ListContainersAsync(string? labelFilter = null, CancellationToken ct = default);
}

public record TerminalExecSession
{
    public string ExecId { get; init; } = string.Empty;
    public Stream Stream { get; init; } = Stream.Null;
}

public record ContainerExecResult
{
    public int ExitCode { get; init; }
    public string Stdout { get; init; } = string.Empty;
    public string Stderr { get; init; } = string.Empty;
}

public record ContainerInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Image { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public Dictionary<string, string> Labels { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; }
}

public interface ITechnologyWatcher
{
    Task<IReadOnlyList<TechnologyChange>> CheckTechnologyAsync(string technology, CancellationToken ct = default);
    Task<IReadOnlyList<TechnologyChange>> CheckAllAsync(CancellationToken ct = default);
}

public interface ISourceConnector
{
    string SourceType { get; }
    Task<SourceCheckResult> CheckAsync(TechnologySource source, CancellationToken ct = default);
}

public record SourceCheckResult
{
    public bool HasChanges { get; init; }
    public string? CurrentVersion { get; init; }
    public string? ContentHash { get; init; }
    public JsonDocument? RawData { get; init; }
    public string? Error { get; init; }
}

public interface IContentPipeline
{
    Task<ContentImportResult> ImportFromMarkdownAsync(string technologyPath, CancellationToken ct = default);
    Task<ContentImportResult> ImportLabAsync(string labPath, CancellationToken ct = default);
}

public record ContentImportResult
{
    public bool Success { get; init; }
    public Guid? NodeId { get; init; }
    public string[] Errors { get; init; } = [];
    public string[] Warnings { get; init; } = [];
}
