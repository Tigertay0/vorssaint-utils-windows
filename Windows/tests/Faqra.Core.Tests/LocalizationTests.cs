using Faqra.Core.Localization;

namespace Faqra.Core.Tests;

public class LocalizationTests
{
    [Fact]
    public void EveryLanguageResolvesToACatalog()
    {
        foreach (var language in AppLanguages.All)
        {
            Assert.NotNull(Strings.For(language));
        }
    }

    [Fact]
    public void EnglishCatalogHasNoEmptyStrings()
    {
        var properties = typeof(Strings).GetProperties()
            .Where(p => p.PropertyType == typeof(string) && !p.GetMethod!.IsStatic)
            .ToList();
        Assert.NotEmpty(properties);
        foreach (var property in properties)
        {
            var value = (string?)property.GetValue(Strings.EnUS);
            Assert.False(string.IsNullOrWhiteSpace(value), $"{property.Name} is empty");
        }
    }

    [Fact]
    public void RawValuesRoundTrip()
    {
        foreach (var language in AppLanguages.All)
        {
            Assert.Equal(language, AppLanguages.FromRawValue(language.RawValue()));
        }
        Assert.Null(AppLanguages.FromRawValue("xx"));
        Assert.Null(AppLanguages.FromRawValue(null));
    }

    [Theory]
    [InlineData("zh-HK", AppLanguage.ZhHK)]
    [InlineData("zh-Hant-HK", AppLanguage.ZhHK)]
    [InlineData("zh-TW", AppLanguage.ZhTW)]
    [InlineData("zh-Hant", AppLanguage.ZhTW)]
    [InlineData("zh-CN", AppLanguage.ZhHans)]
    [InlineData("zh-Hans-CN", AppLanguage.ZhHans)]
    [InlineData("pt-PT", AppLanguage.PtBR)]
    [InlineData("pt-BR", AppLanguage.PtBR)]
    [InlineData("de-AT", AppLanguage.De)]
    [InlineData("en-GB", AppLanguage.EnUS)]
    [InlineData("xx", AppLanguage.EnUS)]
    [InlineData("", AppLanguage.EnUS)]
    public void SystemDefault_FollowsUpstreamPrefixRules(string culture, AppLanguage expected) =>
        Assert.Equal(expected, AppLanguages.SystemDefault(culture));

    [Fact]
    public void OnlyRussianUsesFewCountForm()
    {
        Assert.True(AppLanguage.Ru.UsesFewCountForm());
        Assert.All(AppLanguages.All.Where(l => l != AppLanguage.Ru), l => Assert.False(l.UsesFewCountForm()));
    }

    [Fact]
    public void ChangingLanguageRaisesChangedOnce()
    {
        var l10n = L10n.Shared;
        var original = l10n.Language;
        var raised = 0;
        EventHandler handler = (_, _) => raised++;
        l10n.Changed += handler;
        try
        {
            var next = original == AppLanguage.De ? AppLanguage.Fr : AppLanguage.De;
            l10n.Language = next;
            l10n.Language = next;
            Assert.Equal(1, raised);
        }
        finally
        {
            l10n.Changed -= handler;
            l10n.Language = original;
        }
    }
}
