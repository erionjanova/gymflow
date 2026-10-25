using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Invitations;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;
using GymFlow.Infrastructure.Identity;
using GymFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymFlow.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class InvitationTests(PostgresFixture fixture)
{
    private readonly CapturingEmailSender _email = new();

    private InvitationService Service(
        ApplicationDbContext db, Guid? tenantId, Guid? userId, TimeProvider? time = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id, "test");
            tenantContext.SetRole(TenantRole.Owner);
        }

        var jwt = new JwtTokenGenerator(
            Options.Create(new JwtOptions { Secret = new string('x', 40) }), TimeProvider.System);

        return new InvitationService(
            db, tenantContext, new FakeCurrentUser(userId), _email, new PasswordHasher(), jwt,
            time ?? TimeProvider.System, Options.Create(new AppOptions { FrontendUrl = "http://app.test" }),
            new CreateInvitationRequestValidator(), new NewAccountForInvitationValidator());
    }

    private async Task<string> InviteAsync(SeededGym gym, string email, TenantRole role = TenantRole.Trainer)
    {
        await using var db = fixture.CreateContext(gym.TenantId, TenantRole.Owner);
        await Service(db, gym.TenantId, gym.OwnerId).CreateAsync(new CreateInvitationRequest(email, role), default);
        return _email.LastInvitationToken();
    }

    private async Task<string> EmailOfAsync(Guid userId)
    {
        await using var db = fixture.CreateContext();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.Email).SingleAsync();
    }

    private async Task<TenantRole?> RoleInGymAsync(Guid tenantId, string email)
    {
        await using var db = fixture.CreateContext(tenantId);
        return await db.TenantUsers.Where(tu => tu.User!.Email == email).Select(tu => (TenantRole?)tu.Role).SingleOrDefaultAsync();
    }

    [Fact]
    public async Task New_person_accepts_with_a_new_account_and_becomes_trainer()
    {
        var gym = await fixture.SeedGymAsync("Invite Gym");
        var email = $"ana-{Guid.NewGuid():N}@test.local";

        var token = await InviteAsync(gym, email);

        var message = Assert.Single(_email.Sent);
        Assert.Equal(email, message.To);
        Assert.Contains("http://app.test/invite/", message.HtmlBody);

        await using (var db = fixture.CreateContext())
        {
            var stored = await db.Invitations.IgnoreQueryFilters().SingleAsync(i => i.Email == email);
            Assert.NotEqual(token, stored.TokenHash);
            Assert.Equal(SecureTokens.Hash(token), stored.TokenHash);

            var preview = await Service(db, null, null).PreviewAsync(token, default);
            Assert.Equal("Invite Gym", preview.GymName);
            Assert.False(preview.HasAccount);

            var result = await Service(db, null, null)
                .AcceptAsync(token, new AcceptInvitationRequest("Ana Gashi", "Trainer123", "sq"), default);

            Assert.NotNull(result.Auth);
            Assert.Equal(TenantRole.Trainer, result.Gym.Role);
        }

        Assert.Equal(TenantRole.Trainer, await RoleInGymAsync(gym.TenantId, email));

        await using var again = fixture.CreateContext();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Service(again, null, null).AcceptAsync(token, new AcceptInvitationRequest("X", "Password123", null), default));
        Assert.Equal("invitation_accepted", ex.Code);
    }

    [Fact]
    public async Task Existing_member_logged_in_is_promoted()
    {
        var gym = await fixture.SeedGymAsync("Promote Invite Gym");
        var memberEmail = await EmailOfAsync(gym.MemberId);
        var token = await InviteAsync(gym, memberEmail.ToUpperInvariant());

        await using (var db = fixture.CreateContext())
        {
            var result = await Service(db, null, gym.MemberId).AcceptAsync(token, new AcceptInvitationRequest(null, null, null), default);
            Assert.Null(result.Auth);
            Assert.Equal(TenantRole.Trainer, result.Gym.Role);
        }

        Assert.Equal(TenantRole.Trainer, await RoleInGymAsync(gym.TenantId, memberEmail));
    }

    [Fact]
    public async Task Wrong_account_or_logged_out_owner_of_email_cannot_accept()
    {
        var gym = await fixture.SeedGymAsync("Wrong Account Gym");
        var other = await fixture.SeedGymAsync("Other Gym");
        var memberEmail = await EmailOfAsync(gym.MemberId);
        var token = await InviteAsync(gym, memberEmail);

        await using var db = fixture.CreateContext();
        var empty = new AcceptInvitationRequest(null, null, null);

        var wrong = await Assert.ThrowsAsync<ForbiddenException>(() =>
            Service(db, null, other.OwnerId).AcceptAsync(token, empty, default));
        Assert.Equal("invitation_wrong_account", wrong.Code);

        var loggedOut = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Service(db, null, null).AcceptAsync(token, new AcceptInvitationRequest("Hacker", "Password123", null), default));
        Assert.Equal("login_required", loggedOut.Code);

        Assert.Equal(TenantRole.Member, await RoleInGymAsync(gym.TenantId, memberEmail));
    }

    [Fact]
    public async Task Inviting_again_revokes_the_old_link()
    {
        var gym = await fixture.SeedGymAsync("Reinvite Gym");
        var email = $"reinvite-{Guid.NewGuid():N}@test.local";

        var first = await InviteAsync(gym, email);
        var second = await InviteAsync(gym, email);

        await using var db = fixture.CreateContext();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Service(db, null, null).AcceptAsync(first, new AcceptInvitationRequest("A", "Password123", null), default));
        Assert.Equal("invitation_revoked", ex.Code);

        var result = await Service(db, null, null).AcceptAsync(second, new AcceptInvitationRequest("A", "Password123", null), default);
        Assert.Equal(TenantRole.Trainer, result.Gym.Role);
    }

    [Fact]
    public async Task Expired_links_are_rejected()
    {
        var gym = await fixture.SeedGymAsync("Expired Gym");
        var token = await InviteAsync(gym, $"late-{Guid.NewGuid():N}@test.local");

        await using var db = fixture.CreateContext();
        var later = new ShiftedTimeProvider(InvitationService.Lifetime + TimeSpan.FromMinutes(1));
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Service(db, null, null, later).AcceptAsync(token, new AcceptInvitationRequest("A", "Password123", null), default));

        Assert.Equal("invitation_expired", ex.Code);
    }

    [Fact]
    public async Task Cannot_invite_someone_who_is_already_staff()
    {
        var gym = await fixture.SeedGymAsync("Staff Gym");
        var ownerEmail = await EmailOfAsync(gym.OwnerId);

        await using var db = fixture.CreateContext(gym.TenantId, TenantRole.Owner);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Service(db, gym.TenantId, gym.OwnerId).CreateAsync(new CreateInvitationRequest(ownerEmail, TenantRole.Trainer), default));

        Assert.Equal("already_staff", ex.Code);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Invitations_are_invisible_to_other_gyms()
    {
        var gymA = await fixture.SeedGymAsync("Invite A");
        var gymB = await fixture.SeedGymAsync("Invite B");
        await InviteAsync(gymA, $"a-{Guid.NewGuid():N}@test.local");

        await using var db = fixture.CreateContext(gymB.TenantId, TenantRole.Owner);
        Assert.Empty(await Service(db, gymB.TenantId, gymB.OwnerId).GetAsync(default));
    }
}
