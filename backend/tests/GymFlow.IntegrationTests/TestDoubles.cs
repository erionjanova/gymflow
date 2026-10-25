using System.Text.RegularExpressions;
using GymFlow.Application.Common.Interfaces;

namespace GymFlow.IntegrationTests;

public partial class CapturingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return ValueTask.CompletedTask;
    }

    public string LastInvitationToken() => InviteLink().Match(Sent[^1].HtmlBody).Groups[1].Value;

    public string LastResetToken() => ResetLink().Match(Sent[^1].HtmlBody).Groups[1].Value;

    [GeneratedRegex("/invite/([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();

    [GeneratedRegex("/reset-password/([A-Za-z0-9_-]+)")]
    private static partial Regex ResetLink();
}

public class FakePaymentGateway(bool configured = true) : IPaymentGateway
{
    public List<CheckoutRequest> Checkouts { get; } = [];
    public List<(string SubscriptionId, bool Cancel)> CancelCalls { get; } = [];

    public bool IsConfigured { get; } = configured;

    public Task<string> CreateCheckoutUrlAsync(CheckoutRequest request, CancellationToken ct)
    {
        Checkouts.Add(request);
        return Task.FromResult($"https://checkout.stripe.test/{Guid.NewGuid():N}");
    }

    public Task SetCancelAtPeriodEndAsync(string subscriptionId, bool cancel, CancellationToken ct)
    {
        CancelCalls.Add((subscriptionId, cancel));
        return Task.CompletedTask;
    }
}

public class ShiftedTimeProvider(TimeSpan shift) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => base.GetUtcNow().Add(shift);
}
