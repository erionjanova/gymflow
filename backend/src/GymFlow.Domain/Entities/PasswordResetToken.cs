using GymFlow.Domain.Common;

namespace GymFlow.Domain.Entities;

public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;
}
