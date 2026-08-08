using System.IO;

namespace TarkovHelper.Tests;

/// <summary>
/// Locates the repo root from the test output directory for tests that read
/// source-tree files (fonts, docs, csproj, update.xml). Shared by
/// FontAssetsTests, DecisionDocsTests, and UpdateXmlTests so the walk-up rule
/// lives in one place.
/// </summary>
internal static class TestRepo
{
    /// <summary>
    /// Walks up from the test output dir to the repo root. Requires docs/decisions
    /// next to TarkovHelper.sln so the nested TarkovHelper/TarkovHelper.sln can't match.
    /// </summary>
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var slnPath = Path.Combine(dir.FullName, "TarkovHelper.sln");
            var decisionsPath = Path.Combine(dir.FullName, "docs", "decisions");
            if (File.Exists(slnPath) && Directory.Exists(decisionsPath))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repo root (TarkovHelper.sln + docs/decisions) above {AppContext.BaseDirectory}");
    }
}
