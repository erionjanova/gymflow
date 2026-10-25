using GymFlow.Application.Common;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Application.Notifications;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymFlow.Application.Auth;

public class PasswordResetService(
    IApplicationDbContext db,
    IEmailSender emailSender,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    IOptions<AppOptions> appOptions,
    IValidator<ForgotPasswordRequest> forgotValidator,
    IValidator<ResetPasswordRequest> resetValidator)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    public async Task RequestAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        await forgotValidator.ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
            return;

        var now = timeProvider.GetUtcNow();
        await InvalidateOpenTokensAsync(user.Id, now, ct);

        var (token, hash) = SecureTokens.Create();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = now.Add(Lifetime)
        });
        await db.SaveChangesAsync(ct);

        var url = $"{appOptions.Value.FrontendUrl.TrimEnd('/')}/reset-password/{token}";
        var (subject, html) = EmailTemplates.PasswordReset(
            user.PreferredLanguage, new PasswordResetEmailModel(user.FullName, url, (int)Lifetime.TotalMinutes));
        await emailSender.QueueAsync(new EmailMessage(user.Email, subject, html), ct);
    }

    public async Task ResetAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        await resetValidator.ValidateAndThrowAsync(request, ct);

        var now = timeProvider.GetUtcNow();
        var hash = SecureTokens.Hash(request.Token);
        var resetToken = await db.PasswordResetTokens.Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (resetToken?.User is null || !resetToken.IsUsable(now))
            throw new DomainException("reset_link_invalid", "This reset link is invalid or has expired.");

        resetToken.User.PasswordHash = passwordHasher.Hash(request.NewPassword);
        await InvalidateOpenTokensAsync(resetToken.UserId, now, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task InvalidateOpenTokensAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var open = await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in open)
            token.UsedAt = now;
    }
}
