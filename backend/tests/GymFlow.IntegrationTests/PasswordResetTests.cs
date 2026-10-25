using GymFlow.Application.Auth;
using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Domain.Exceptions;
using GymFlow.Infrastructure.Identity;
using GymFlow.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace GymFlow.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class PasswordResetTests(PostgresFixture fixture)
{
    private readonly CapturingEmailSender _email = new();

    private PasswordResetService Service(ApplicationDbContext db, TimeProvider? time = null) =>
        new(db, _email, new PasswordHasher(), time ?? TimeProvider.System,
            Options.Create(new AppOptions { FrontendUrl = "http://app.test" }),
            new ForgotPasswordRequestValidator(), new ResetPasswordRequestValidator());

    private static AuthService Auth(ApplicationDbContext db) =>
        new(db, new PasswordHasher(),
            new JwtTokenGenerator(Options.Create(new JwtOptions { Secret = new string('x', 40) }), TimeProvider.System),
            new FakeCurrentUser(null),
            new RegisterRequestValidator(), new LoginRequestValidator(),
            new UpdateProfileRequestValidator(), new ChangePasswordRequestValidator());

    private async Task<string> RegisterAsync(string language = "sq")
    {
        var email = $"reset{Guid.NewGuid():N}@test.local";
        await using var db = fixture.CreateContext();
        await Auth(db).RegisterAsync(new RegisterRequest("Reset User", email, "OldPassword1", null, language), default);
        return email;
    }

    private async Task<string> RequestTokenAsync(string email)
    {
        await using var db = fixture.CreateContext();
        await Service(db).RequestAsync(new ForgotPasswordRequest(email), default);
        return _email.LastResetToken();
    }

    private async Task ResetAsync(string token, string password, TimeProvider? time = null)
    {
        await using var db = fixture.CreateContext();
        await Service(db, time).ResetAsync(new ResetPasswordRequest(token, password), default);
    }

    private async Task LoginAsync(string email, string password)
    {
        await using var db = fixture.CreateContext();
        await Auth(db).LoginAsync(new LoginRequest(email, password), default);
    }

    [Fact]
    public async Task Reset_link_sets_a_new_password_and_works_once()
    {
        var email = await RegisterAsync();
        var token = await RequestTokenAsync(email);

        Assert.Equal(email, _email.Sent[^1].To);
        Assert.Contains("http://app.test/reset-password/", _email.Sent[^1].HtmlBody);

        await ResetAsync(token, "NewPassword2");
        await LoginAsync(email, "NewPassword2");
        await Assert.ThrowsAsync<UnauthorizedException>(() => LoginAsync(email, "OldPassword1"));

        var reused = await Assert.ThrowsAsync<DomainException>(() => ResetAsync(token, "Another3Pass"));
        Assert.Equal("reset_link_invalid", reused.Code);
    }

    [Fact]
    public async Task Unknown_email_sends_nothing_and_does_not_fail()
    {
        await using var db = fixture.CreateContext();
        await Service(db).RequestAsync(new ForgotPasswordRequest($"nobody{Guid.NewGuid():N}@test.local"), default);

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Expired_link_is_rejected()
    {
        var email = await RegisterAsync();
        var token = await RequestTokenAsync(email);

        var later = new ShiftedTimeProvider(PasswordResetService.Lifetime + TimeSpan.FromMinutes(1));
        var error = await Assert.ThrowsAsync<DomainException>(() => ResetAsync(token, "NewPassword2", later));

        Assert.Equal("reset_link_invalid", error.Code);
        await LoginAsync(email, "OldPassword1");
    }

    [Fact]
    public async Task Requesting_again_invalidates_the_previous_link()
    {
        var email = await RegisterAsync();
        var first = await RequestTokenAsync(email);
        var second = await RequestTokenAsync(email);

        await Assert.ThrowsAsync<DomainException>(() => ResetAsync(first, "NewPassword2"));
        await ResetAsync(second, "NewPassword2");
        await LoginAsync(email, "NewPassword2");
    }

    [Fact]
    public async Task Email_uses_the_user_language()
    {
        var email = await RegisterAsync("en");
        await RequestTokenAsync(email);

        Assert.Equal("Reset your GymFlow password", _email.Sent[^1].Subject);
    }
}
