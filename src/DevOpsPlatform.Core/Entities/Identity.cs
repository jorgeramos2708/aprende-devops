namespace DevOpsPlatform.Core.Entities;

public record ApplicationUser : Entity<Ulid>
{
    public string Email { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string[] Roles { get; set; } = ["student"];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
}

public record RefreshToken : Entity<Ulid>
{
    public Ulid UserId { get; init; }
    public string TokenHash { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? ReplacedAt { get; set; }
}

public record UserProgress : Entity<Ulid>
{
    public Ulid UserId { get; init; }
    public Ulid NodeId { get; init; }
    public KnowledgeNode Node { get; init; } = null!;
    public string Status { get; set; } = "not_started"; // not_started, in_progress, completed, mastered
    public decimal? Score { get; set; }
    public int Attempts { get; set; }
    public long TimeSpentSeconds { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;
}
