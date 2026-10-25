using GymFlow.Application.Auth;
using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Application.Notifications;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymFlow.Application.Invitations;

public class InvitationService(
    IApplicationDbContext db,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IEmailSender emailSender,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    TimeProvider timeProvider,
    IOptions<AppOptions> appOptions,
    IValidator<CreateInvitationRequest> createValidator,
    IValidator<AcceptInvitationRequest> newAccountValidator)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task<IReadOnlyList<InvitationDto>> GetAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await db.Invitations.AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Take(100)
            .Select(i => new { Invitation = i, InvitedBy = i.InvitedBy!.FullName })
            .ToListAsync(ct);

        return rows.Select(r => ToDto(r.Invitation, r.InvitedBy, now)).ToList();
    }

    public async Task<InvitationDto> CreateAsync(CreateInvitationRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var inviterId = currentUser.RequireUserId();
        var email = request.Email.Trim().ToLowerInvariant();
        var now = timeProvider.GetUtcNow();

        var currentRole = await db.TenantUsers
            .Where(tu => tu.User!.Email == email)
            .Select(tu => (TenantRole?)tu.Role)
            .SingleOrDefaultAsync(ct);
        if (currentRole is not null && currentRole >= request.Role)
            throw new ConflictException("already_staff", $"This person is already a {currentRole.ToString()!.ToLowerInvariant()} here.");

        var pending = await db.Invitations
            .Where(i => i.Email == email && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (var old in pending)
            old.Revoke(now);

        var (token, hash) = SecureTokens.Create();
        var invitation = new Invitation
        {
            Email = email,
            Role = request.Role,
            TokenHash = hash,
            ExpiresAt = now.Add(Lifetime),
            InvitedById = inviterId
        };

        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        var inviter = await SendAsync(invitation, token, ct);
        return ToDto(invitation, inviter, now);
    }

    public async Task<InvitationDto> ResendAsync(Guid id, CancellationToken ct)
    {
        var invitation = await FindAsync(id, ct);
        var now = timeProvider.GetUtcNow();

        var (token, hash) = SecureTokens.Create();
        invitation.Renew(hash, now.Add(Lifetime), now);
        await db.SaveChangesAsync(ct);

        var inviter = await SendAsync(invitation, token, ct);
        return ToDto(invitation, inviter, now);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var invitation = await FindAsync(id, ct);
        invitation.Revoke(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    public async Task<InvitationPreviewDto> PreviewAsync(string token, CancellationToken ct)
    {
        var invitation = await FindByTokenAsync(token, ct);
        var gym = invitation.Tenant!;
        var hasAccount = await db.Users.AnyAsync(u => u.Email == invitation.Email, ct);

        return new InvitationPreviewDto(
            gym.Name, gym.Slug, gym.BrandColor, invitation.Email, invitation.Role,
            invitation.GetStatus(timeProvider.GetUtcNow()), hasAccount);
    }

    public async Task<AcceptInvitationResponse> AcceptAsync(string token, AcceptInvitationRequest request, CancellationToken ct)
    {
        var invitation = await FindByTokenAsync(token, ct);
        var gym = invitation.Tenant!;
        var now = timeProvider.GetUtcNow();

        if (gym.IsSuspended)
            throw new ForbiddenException("gym_suspended", "This gym is currently suspended.");

        invitation.EnsurePending(now);

        User user;
        AuthResponse? auth = null;

        if (currentUser.UserId is { } userId)
        {
            user = await db.Users.SingleAsync(u => u.Id == userId, ct);
            if (user.Email != invitation.Email)
                throw new ForbiddenException(
                    "invitation_wrong_account", $"This invitation is for {invitation.Email}. Log in with that account to accept it.");
        }
        else
        {
            if (await db.Users.AnyAsync(u => u.Email == invitation.Email, ct))
                throw new UnauthorizedException("login_required", "You already have an account. Log in to accept the invitation.");

            await newAccountValidator.ValidateAndThrowAsync(request, ct);
            user = new User
            {
                FullName = request.FullName!.Trim(),
                Email = invitation.Email,
                PreferredLanguage = request.PreferredLanguage ?? "sq"
            };
            user.PasswordHash = passwordHasher.Hash(request.Password!);
            db.Users.Add(user);
        }

        var membership = await db.TenantUsers.IgnoreQueryFilters()
            .SingleOrDefaultAsync(tu => tu.TenantId == gym.Id && tu.UserId == user.Id, ct);

        if (membership is null)
        {
            membership = new TenantUser { TenantId = gym.Id, UserId = user.Id, Role = invitation.Role };
            db.TenantUsers.Add(membership);
        }
        else if (membership.Role < invitation.Role)
        {
            membership.Role = invitation.Role;
        }

        invitation.Accept(user.Id, now);
        await db.SaveChangesAsync(ct);

        if (currentUser.UserId is null)
        {
            var (jwt, expiresAt) = tokenGenerator.Generate(user);
            auth = new AuthResponse(jwt, expiresAt, UserDto.From(user));
        }

        return new AcceptInvitationResponse(
            new MyGymDto(gym.Id, gym.Name, gym.Slug, gym.BrandColor, membership.Role, gym.IsSuspended), auth);
    }

    private async Task<string> SendAsync(Invitation invitation, string token, CancellationToken ct)
    {
        var tenantId = tenantContext.RequireTenantId();
        var gym = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, ct);
        var inviter = await db.Users.AsNoTracking().SingleAsync(u => u.Id == invitation.InvitedById, ct);

        var url = $"{appOptions.Value.FrontendUrl.TrimEnd('/')}/invite/{token}";
        var (subject, html) = EmailTemplates.Invitation(
            inviter.PreferredLanguage,
            new InvitationEmailModel(gym.Name, gym.BrandColor, inviter.FullName, invitation.Role, url, invitation.ExpiresAt));

        await emailSender.QueueAsync(new EmailMessage(invitation.Email, subject, html), ct);
        return inviter.FullName;
    }

    private async Task<Invitation> FindAsync(Guid id, CancellationToken ct) =>
        await db.Invitations.SingleOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new NotFoundException("invitation_not_found", "Invitation not found.");

    private async Task<Invitation> FindByTokenAsync(string token, CancellationToken ct)
    {
        var hash = SecureTokens.Hash(token);
        return await db.Invitations.IgnoreQueryFilters()
                   .Include(i => i.Tenant)
                   .SingleOrDefaultAsync(i => i.TokenHash == hash, ct)
               ?? throw new NotFoundException("invitation_not_found", "This invitation link is not valid.");
    }

    private static InvitationDto ToDto(Invitation i, string invitedBy, DateTimeOffset now) =>
        new(i.Id, i.Email, i.Role, i.GetStatus(now), invitedBy, i.CreatedAt, i.ExpiresAt);
}
