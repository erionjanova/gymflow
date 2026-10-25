using FluentValidation;
using GymFlow.Application.Auth;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;
using GymFlow.Infrastructure.Identity;
using GymFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymFlow.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class AuthTests(PostgresFixture fixture)
{
    private static AuthService Service(ApplicationDbContext db, Guid? userId = null)
    {
        var jwt = new JwtTokenGenerator(
            Options.Create(new JwtOptions { Secret = new string('x', 40) }), TimeProvider.System);

        return new AuthService(
            db, new PasswordHasher(), jwt, new FakeCurrentUser(userId),
            new RegisterRequestValidator(), new LoginRequestValidator(),
            new UpdateProfileRequestValidator(), new ChangePasswordRequestValidator());
    }

    private async Task<(Guid UserId, string Email)> RegisterAsync(string password)
    {
        var email = $"user{Guid.NewGuid():N}@test.local";
        await using var db = fixture.CreateContext();
        var response = await Service(db).RegisterAsync(new RegisterRequest("Test User", email, password, null, "sq"), default);
        return (response.User.Id, email);
    }

    [Fact]
    public async Task Changed_password_replaces_the_old_one_for_login()
    {
        var (userId, email) = await RegisterAsync("OldPassword1");

        await using (var db = fixture.CreateContext())
            await Service(db, userId).ChangePasswordAsync(new ChangePasswordRequest("OldPassword1", "NewPassword2"), default);

        await using (var db = fixture.CreateContext())
        {
            var response = await Service(db).LoginAsync(new LoginRequest(email, "NewPassword2"), default);
            Assert.Equal(userId, response.User.Id);

            var old = await Assert.ThrowsAsync<UnauthorizedException>(() =>
                Service(db).LoginAsync(new LoginRequest(email, "OldPassword1"), default));
            Assert.Equal("invalid_credentials", old.Code);
        }
    }

    [Fact]
    public async Task Wrong_current_password_is_rejected()
    {
        var (userId, email) = await RegisterAsync("OldPassword1");

        await using (var db = fixture.CreateContext())
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                Service(db, userId).ChangePasswordAsync(new ChangePasswordRequest("NotMyPassword1", "NewPassword2"), default));
            Assert.Equal("wrong_password", error.Code);
        }

        await using (var db = fixture.CreateContext())
            await Service(db).LoginAsync(new LoginRequest(email, "OldPassword1"), default);
    }

    [Fact]
    public async Task New_password_must_differ_from_the_current_one()
    {
        var (userId, _) = await RegisterAsync("OldPassword1");

        await using var db = fixture.CreateContext();
        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(db, userId).ChangePasswordAsync(new ChangePasswordRequest("OldPassword1", "OldPassword1"), default));
    }

    [Fact]
    public async Task My_gyms_keeps_suspended_gyms_and_flags_them()
    {
        var active = await fixture.SeedGymAsync("Zeta Active");
        var suspended = await fixture.SeedGymAsync("Alpha Suspended");

        await using (var db = fixture.CreateContext())
        {
            db.TenantUsers.Add(new TenantUser { TenantId = suspended.TenantId, UserId = active.OwnerId, Role = TenantRole.Member });
            (await db.Tenants.SingleAsync(t => t.Id == suspended.TenantId)).IsSuspended = true;
            await db.SaveChangesAsync();
        }

        await using var check = fixture.CreateContext();
        var gyms = await Service(check, active.OwnerId).GetMyGymsAsync(default);

        Assert.Collection(gyms,
            g => Assert.Equal(("Zeta Active", false), (g.Name, g.IsSuspended)),
            g => Assert.Equal(("Alpha Suspended", true), (g.Name, g.IsSuspended)));
    }
}
