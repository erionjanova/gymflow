using GymFlow.Application.Tenancy;

namespace GymFlow.Application.Tests.Tenancy;

public class SlugRulesTests
{
    [Theory]
    [InlineData("Power Gym", "power-gym")]
    [InlineData("  Iron  &  Steel!! ", "iron-steel")]
    [InlineData("Palestra Çelik", "palestra-celik")]
    [InlineData("Fitness-Ëmëra 24/7", "fitness-emera-24-7")]
    [InlineData("---Gym---", "gym")]
    public void Normalize_creates_url_friendly_slug(string input, string expected)
    {
        Assert.Equal(expected, SlugRules.Normalize(input));
    }

    [Theory]
    [InlineData("powergym")]
    [InlineData("power-gym")]
    [InlineData("gym24")]
    public void IsValid_accepts_good_slugs(string slug)
    {
        Assert.True(SlugRules.IsValid(slug));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Power-Gym")]
    [InlineData("power_gym")]
    [InlineData("-powergym")]
    [InlineData("power--gym")]
    [InlineData("admin")]
    [InlineData("api")]
    public void IsValid_rejects_bad_or_reserved_slugs(string slug)
    {
        Assert.False(SlugRules.IsValid(slug));
    }

    [Fact]
    public void IsValid_rejects_slugs_over_max_length()
    {
        Assert.False(SlugRules.IsValid(new string('a', SlugRules.MaxLength + 1)));
    }
}
