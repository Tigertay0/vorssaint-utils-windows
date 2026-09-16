using Faqra.App.About;
using Faqra.Core;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

public class AboutWindowTests
{
    [Fact]
    public void AboutContent_ShowsIdentityVersionAndUpstreamCredit()
    {
        StaThread.Run(() =>
        {
            var content = new AboutContent();
            var s = Strings.EnUS;

            Assert.Equal(AppInfo.Name, content.NameText.Text);
            Assert.StartsWith(s.VersionPrefix, content.VersionText.Text);
            Assert.Equal(s.AboutDescription, content.DescriptionText.Text);
            Assert.Equal(s.ViewOnGitHub, content.GitHubButton.Content);
            Assert.Equal(AppInfo.Copyright, content.CopyrightText.Text);
            Assert.Equal(AppInfo.UpstreamCredit, content.CreditText.Text);
            Assert.NotNull(content.GlyphImage.Source);
        });
    }

    [Fact]
    public void AboutWindow_TitlesItselfFromTheMenuString()
    {
        StaThread.Run(() =>
        {
            var window = new AboutWindow();
            try
            {
                Assert.Equal(Strings.EnUS.MenuAbout, window.Title);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
