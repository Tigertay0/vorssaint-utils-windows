using Faqra.App.About;
using Faqra.Core;
using Faqra.Core.Localization;

namespace Faqra.App.Tests;

public class AboutWindowTests
{
    [Fact]
    public void ConstructsAndShowsUpstreamContent()
    {
        StaThread.Run(() =>
        {
            var window = new AboutWindow();
            try
            {
                var s = Strings.EnUS;
                Assert.Equal(s.MenuAbout, window.Title);
                Assert.Equal(AppInfo.Name, window.NameText.Text);
                Assert.StartsWith(s.VersionPrefix, window.VersionText.Text);
                Assert.Equal(s.AboutDescription, window.DescriptionText.Text);
                Assert.Equal(s.ViewOnGitHub, window.GitHubButton.Content);
                Assert.Equal(AppInfo.Copyright, window.CopyrightText.Text);
                Assert.NotNull(window.GlyphImage.Source);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
