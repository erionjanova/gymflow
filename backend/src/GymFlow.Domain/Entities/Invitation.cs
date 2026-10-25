using GymFlow.Domain.Common;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;

namespace GymFlow.Domain.Entities;

public class Invitation : TenantEntity
{
    public string Email { get; set; } = string.Empty;
    public TenantRole Role { get; set; } = TenantRole.Trainer;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid InvitedById { get; set; }
    public User? InvitedBy { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedById { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public InvitationStatus GetStatus(DateTimeOffset now) =>
        AcceptedAt is not null ? InvitationStatus.Accepted
        : RevokedAt is not null ? InvitationStatus.Revoked
        : now >= ExpiresAt ? InvitationStatus.Expired
        : InvitationStatus.Pending;

    public void Accept(Guid userId, DateTimeOffset now)
    {
        EnsurePending(now);
        AcceptedAt = now;
        AcceptedById = userId;
    }

    public void Revoke(DateTimeOffset now)
    {
        EnsurePending(now);
        RevokedAt = now;
    }

    public void Renew(string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (GetStatus(now) is InvitationStatus.Accepted or InvitationStatus.Revoked)
            throw new DomainException("invitation_closed", "This invitation can no longer be sent.");

        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public void EnsurePending(DateTimeOffset now)
    {
        switch (GetStatus(now))
        {
            case InvitationStatus.Accepted:
                throw new DomainException("invitation_accepted", "This invitation was already used.");
            case InvitationStatus.Revoked:
                throw new DomainException("invitation_revoked", "This invitation was withdrawn.");
            case InvitationStatus.Expired:
                throw new DomainException("invitation_expired", "This invitation has expired. Ask the gym for a new one.");
        }
    }
}
