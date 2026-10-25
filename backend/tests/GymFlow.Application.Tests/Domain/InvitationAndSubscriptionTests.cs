using GymFlow.Application.Billing;
using GymFlow.Application.Common;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;

namespace GymFlow.Application.Tests.Domain;

public class InvitationAndSubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Invitation_status_follows_its_lifecycle()
    {
        var invitation = new Invitation { ExpiresAt = Now.AddDays(7) };

        Assert.Equal(InvitationStatus.Pending, invitation.GetStatus(Now));
        Assert.Equal(InvitationStatus.Expired, invitation.GetStatus(Now.AddDays(7)));

        invitation.Accept(Guid.NewGuid(), Now);
        Assert.Equal(InvitationStatus.Accepted, invitation.GetStatus(Now.AddDays(30)));
        Assert.Equal("invitation_accepted", Assert.Throws<DomainException>(() => invitation.Revoke(Now)).Code);
    }

    [Fact]
    public void Revoked_invitation_cannot_be_renewed_but_expired_one_can()
    {
        var revoked = new Invitation { ExpiresAt = Now.AddDays(7) };
        revoked.Revoke(Now);
        var expired = new Invitation { ExpiresAt = Now.AddDays(-1) };

        Assert.Throws<DomainException>(() => revoked.Renew("hash", Now.AddDays(7), Now));

        expired.Renew("new-hash", Now.AddDays(7), Now);
        Assert.Equal(InvitationStatus.Pending, expired.GetStatus(Now));
        Assert.Equal("new-hash", expired.TokenHash);
    }

    [Fact]
    public void Tokens_are_random_and_only_the_hash_matches()
    {
        var (token1, hash1) = SecureTokens.Create();
        var (token2, _) = SecureTokens.Create();

        Assert.NotEqual(token1, token2);
        Assert.Equal(64, hash1.Length);
        Assert.Equal(hash1, SecureTokens.Hash(token1));
        Assert.NotEqual(hash1, SecureTokens.Hash(token2));
    }

    [Theory]
    [InlineData("active", SubscriptionStatus.Active)]
    [InlineData("trialing", SubscriptionStatus.Active)]
    [InlineData("past_due", SubscriptionStatus.PastDue)]
    [InlineData("unpaid", SubscriptionStatus.PastDue)]
    [InlineData("canceled", SubscriptionStatus.Canceled)]
    [InlineData("incomplete_expired", SubscriptionStatus.Canceled)]
    [InlineData("incomplete", SubscriptionStatus.Incomplete)]
    [InlineData("paused", SubscriptionStatus.Incomplete)]
    public void Stripe_statuses_map_to_gym_statuses(string stripe, SubscriptionStatus expected)
    {
        Assert.Equal(expected, BillingService.MapStatus(stripe));
    }

    [Fact]
    public void Subscription_ignores_events_older_than_the_last_one()
    {
        var subscription = new MemberSubscription();

        Assert.True(subscription.Apply(SubscriptionStatus.Canceled, Now, false, "cus_1", Now));
        Assert.False(subscription.Apply(SubscriptionStatus.Active, Now, false, null, Now.AddMinutes(-1)));

        Assert.Equal(SubscriptionStatus.Canceled, subscription.Status);
        Assert.Equal(Now, subscription.CanceledAt);
        Assert.False(subscription.GrantsAccess);
    }
}
