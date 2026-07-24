using System.IO;
using System.Xml.Linq;
using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Guards the repo-root update.xml — the update feed served raw from main that every
/// installed client polls. It must stay parseable by the app's own parser and must keep
/// pointing at this fork (josephjang), not upstream (Zeliper): a client
/// following an upstream URL would replace itself with the upstream build.
///
/// The xml's version is intentionally NOT asserted against the csproj version: during a
/// release, update.xml lags one step behind by design (it is bumped only after the
/// GitHub Release asset exists, so clients never see a download URL that 404s).
/// The complementary half of this release invariant — the tag matching the csproj
/// version, plus the CalVer tag-format guard — lives in .github/workflows/release.yml.
/// </summary>
public sealed class UpdateXmlTests
{
    private const string ForkRepoUrl = "https://github.com/josephjang/Tarkov-Item-Helper/";

    /// <summary>
    /// Walks up from the test output dir to the repo root. Requires update.xml next to
    /// TarkovHelper.sln so the nested TarkovHelper/TarkovHelper.sln can't match.
    /// </summary>
    private static string RepoUpdateXmlPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var slnPath = Path.Combine(dir.FullName, "TarkovHelper.sln");
            var xmlPath = Path.Combine(dir.FullName, "update.xml");
            if (File.Exists(slnPath) && File.Exists(xmlPath))
            {
                return xmlPath;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate the repo root (TarkovHelper.sln + update.xml) above {AppContext.BaseDirectory}");
    }

    [Fact]
    public void Update_xml_parses_via_app_parser()
    {
        var updateInfo = UpdateService.ParseUpdateXml(File.ReadAllText(RepoUpdateXmlPath()));

        Assert.NotNull(updateInfo);
        Assert.False(string.IsNullOrEmpty(updateInfo.DownloadUrl));
        Assert.False(string.IsNullOrEmpty(updateInfo.ChangelogUrl));
    }

    [Fact]
    public void Update_xml_urls_point_at_fork()
    {
        var updateInfo = UpdateService.ParseUpdateXml(File.ReadAllText(RepoUpdateXmlPath()));

        Assert.NotNull(updateInfo);
        // Ordinal: this is a host-pinning guard, so the match must be byte-exact and
        // independent of the test runner's locale, not a culture-aware comparison.
        Assert.StartsWith(ForkRepoUrl, updateInfo.DownloadUrl, StringComparison.Ordinal);
        Assert.StartsWith(ForkRepoUrl, updateInfo.ChangelogUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_xml_url_is_the_versioned_release_asset()
    {
        // Compare against the raw <version> text (not the parsed Version) so a
        // four-part version string can't be normalized away from the tag name.
        var doc = XDocument.Load(RepoUpdateXmlPath());
        var version = doc.Root?.Element("version")?.Value;
        var url = doc.Root?.Element("url")?.Value;

        Assert.False(string.IsNullOrEmpty(version));
        Assert.Equal(
            $"{ForkRepoUrl}releases/download/v{version}/TarkovHelper.zip",
            url);
    }
}
