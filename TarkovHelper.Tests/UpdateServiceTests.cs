using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Covers the update.xml parsing contract (UpdateService.ParseUpdateXml) and pins the
/// update-feed URL constants to this fork. The URL guards exist because a bad merge from
/// upstream (Zeliper) could silently reintroduce its URLs — an app built with those
/// would offer to replace itself with the upstream build.
/// </summary>
public sealed class UpdateServiceTests
{
    [Fact]
    public void Valid_item_xml_parses_into_update_info()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <item>
                <version>2026.7.0</version>
                <url>https://github.com/josephjang/Tarkov-Item-Helper/releases/download/v2026.7.0/TarkovHelper.zip</url>
                <changelog>https://github.com/josephjang/Tarkov-Item-Helper/releases/latest</changelog>
                <mandatory>false</mandatory>
            </item>
            """;

        var info = UpdateService.ParseUpdateXml(xml);

        Assert.NotNull(info);
        Assert.Equal(new Version(2026, 7, 0), info.Version);
        Assert.Equal(
            "https://github.com/josephjang/Tarkov-Item-Helper/releases/download/v2026.7.0/TarkovHelper.zip",
            info.DownloadUrl);
        Assert.Equal("https://github.com/josephjang/Tarkov-Item-Helper/releases/latest", info.ChangelogUrl);
    }

    [Fact]
    public void Missing_url_element_returns_null()
    {
        var info = UpdateService.ParseUpdateXml("<item><version>2026.7.0</version></item>");

        Assert.Null(info);
    }

    [Fact]
    public void Missing_version_element_returns_null()
    {
        var info = UpdateService.ParseUpdateXml("<item><url>https://example.test/x.zip</url></item>");

        Assert.Null(info);
    }

    [Fact]
    public void Unparseable_version_returns_null()
    {
        var info = UpdateService.ParseUpdateXml(
            "<item><version>not-a-version</version><url>https://example.test/x.zip</url></item>");

        Assert.Null(info);
    }

    [Fact]
    public void Wrong_root_element_returns_null()
    {
        // AutoUpdater feeds are often wrapped in <appcast>; this parser requires a bare <item>.
        var info = UpdateService.ParseUpdateXml(
            "<appcast><item><version>2026.7.0</version><url>https://example.test/x.zip</url></item></appcast>");

        Assert.Null(info);
    }

    [Fact]
    public void Malformed_xml_returns_null()
    {
        var info = UpdateService.ParseUpdateXml("<item><version>2026.7.0");

        Assert.Null(info);
    }

    [Fact]
    public void Update_feed_constants_point_at_fork()
    {
        const string fork = "/josephjang/Tarkov-Item-Helper/";

        Assert.Contains(fork, UpdateService.UpdateXmlUrl);
        Assert.Contains(fork, DatabaseUpdateService.VERSION_URL);
        Assert.Contains(fork, DatabaseUpdateService.DATABASE_URL);
    }
}
