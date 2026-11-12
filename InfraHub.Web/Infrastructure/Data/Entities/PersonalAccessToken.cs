namespace InfraHub.Infrastructure.Data.Entities;

/// <summary>
/// Tabel: personal_access_tokens (Laravel Sanctum)
/// </summary>
public class PersonalAccessToken
{
    public long Id { get; set; }
    public string TokenableType { get; set; } = string.Empty;
    public Guid TokenableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? Abilities { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
