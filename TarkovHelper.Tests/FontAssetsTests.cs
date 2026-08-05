using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using TarkovHelper.Services;
// The test project also references WinForms (for TarkovDBEditor); disambiguate.
using FontFamily = System.Windows.Media.FontFamily;

namespace TarkovHelper.Tests;

/// <summary>
/// Guards the shipped font assets named in docs/PRDs/feature-eft-font-stack.spec.md:
/// every embedded file must open as a real GlyphTypeface (CFF/TTF parse guard),
/// group into exactly the two expected WWS families with true Regular + Bold cuts
/// (no synthesized bold), cover the scripts each face is responsible for, stay in
/// sync with the csproj resource list, and leave no Maplestory reference behind.
///
/// Fonts load by file URI from the source tree, not pack URI: pack://application:
/// needs WPF application bootstrapping that is brittle under the xunit host, and
/// the on-disk files are byte-identical to what gets embedded. The repo-root walk
/// matches PrdDocsTests/UpdateXmlTests.
/// </summary>
public sealed class FontAssetsTests
{
    /// <summary>
    /// The three families the shipped files must group into, with the exact
    /// weights the app's chains rely on.
    /// </summary>
    private static readonly string[] ExpectedFamilies = { "Bender", "Play", "Noto Sans CJK KR" };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var slnPath = Path.Combine(dir.FullName, "TarkovHelper.sln");
            var prdsPath = Path.Combine(dir.FullName, "docs", "PRDs");
            if (File.Exists(slnPath) && Directory.Exists(prdsPath))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repo root (TarkovHelper.sln + docs/PRDs) above {AppContext.BaseDirectory}");
    }

    private static string FontsDir() => Path.Combine(RepoRoot(), "TarkovHelper", "Fonts");

    private static IReadOnlyList<string> ShippedFontFiles() =>
        Directory.EnumerateFiles(FontsDir())
            .Where(path => Path.GetExtension(path) is ".ttf" or ".otf")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    [Fact]
    public void Every_shipped_font_opens_as_a_glyph_typeface()
    {
        var files = ShippedFontFiles();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            // GlyphTypeface construction parses the full font through DirectWrite;
            // a corrupt download or an outline format WPF can't render throws here.
            var glyphTypeface = new GlyphTypeface(new Uri(file));
            Assert.True(glyphTypeface.GlyphCount > 0, $"{Path.GetFileName(file)} has no glyphs");
        }
    }

    [Fact]
    public void Shipped_fonts_group_into_exactly_the_expected_families()
    {
        // Trailing separator: Fonts.GetFontFamilies treats a bare directory path
        // as a file location and silently returns nothing.
        var families = Fonts.GetFontFamilies(FontsDir() + Path.DirectorySeparatorChar)
            .Select(family => family.Source[(family.Source.IndexOf('#') + 1)..])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ExpectedFamilies.OrderBy(name => name, StringComparer.Ordinal),
            families);
    }

    [Theory]
    [InlineData("Bender")]
    [InlineData("Play")]
    [InlineData("Noto Sans CJK KR")]
    public void Bold_and_normal_typefaces_resolve_without_simulation(string familyName)
    {
        var baseUri = new Uri(FontsDir() + Path.DirectorySeparatorChar);
        var family = new FontFamily(baseUri, "./#" + familyName);

        foreach (var weight in new[] { FontWeights.Normal, FontWeights.Bold })
        {
            var typeface = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
            Assert.True(typeface.TryGetGlyphTypeface(out var glyphTypeface),
                $"{familyName} {weight} did not resolve to a glyph typeface");
            // StyleSimulations.None proves this is a true cut — a missing Bold
            // face would resolve with BoldSimulation (the old Maplestory faux bold).
            Assert.Equal(StyleSimulations.None, glyphTypeface.StyleSimulations);
            Assert.Equal(weight, glyphTypeface.Weight);
        }
    }

    [Theory]
    // Bender owns Latin, digits, and Cyrillic in every chain — the same
    // scripts it serves in the game's own UI.
    [InlineData("Bender-Regular.otf", "ABCXYZ0189АяЁë№")]
    [InlineData("Bender-Bold.otf", "ABCXYZ0189АяЁë№")]
    // Play backstops the same scripts for any glyph Bender lacks.
    [InlineData("Play-Regular.ttf", "ABCXYZ0189АяЁë")]
    [InlineData("Play-Bold.ttf", "ABCXYZ0189АяЁë")]
    // Noto is the bundled CJK face: Hangul for KO plus kana/kanji as the
    // last-resort JA fallback (調査 appears in quest text).
    [InlineData("NotoSansCJKkr-Regular.otf", "가한調査あア")]
    [InlineData("NotoSansCJKkr-Bold.otf", "가한調査あア")]
    public void Shipped_fonts_cover_their_scripts(string fileName, string sampleChars)
    {
        var glyphTypeface = new GlyphTypeface(new Uri(Path.Combine(FontsDir(), fileName)));
        var missing = sampleChars
            .Where(ch => !glyphTypeface.CharacterToGlyphMap.ContainsKey(ch))
            .ToList();

        Assert.True(missing.Count == 0,
            $"{fileName} is missing glyphs for: {string.Join(" ", missing)}");
    }

    [Fact]
    public void Chain_fragments_match_the_embedded_families_exactly()
    {
        var chainFamilies = Enum.GetValues<AppLanguage>()
            .Select(FontStacks.ForLanguage)
            .SelectMany(chain => chain.Split(','))
            .Select(token => token.Trim())
            .Where(token => token.StartsWith("./Fonts/#", StringComparison.Ordinal))
            .Select(token => token["./Fonts/#".Length..])
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ExpectedFamilies.OrderBy(name => name, StringComparer.Ordinal),
            chainFamilies);
    }

    [Fact]
    public void App_xaml_compiled_default_equals_the_en_chain()
    {
        var appXaml = File.ReadAllText(Path.Combine(RepoRoot(), "TarkovHelper", "App.xaml"));
        var match = Regex.Match(appXaml,
            "<FontFamily x:Key=\"AppFont\">(?<chain>[^<]+)</FontFamily>");

        Assert.True(match.Success, "App.xaml must define the AppFont FontFamily resource");
        Assert.Equal(FontStacks.ForLanguage(AppLanguage.EN), match.Groups["chain"].Value);
    }

    [Fact]
    public void Csproj_resource_entries_match_the_fonts_directory()
    {
        var csprojPath = Path.Combine(RepoRoot(), "TarkovHelper", "TarkovHelper.csproj");
        var csproj = File.ReadAllText(csprojPath);

        var declared = Regex.Matches(csproj, "<Resource Include=\"Fonts\\\\(?<file>[^\"]+)\" */>")
            .Select(m => m.Groups["file"].Value)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var onDisk = ShippedFontFiles()
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(onDisk, declared);

        // The license texts ride through publish via the Fonts\*.txt None entry.
        Assert.Contains("<None Update=\"Fonts\\*.txt\">", csproj);
        Assert.True(File.Exists(Path.Combine(FontsDir(), "LICENSE-Bender.txt")),
            "LICENSE-Bender.txt (attribution/provenance) must ship next to the Bender faces");
        Assert.True(File.Exists(Path.Combine(FontsDir(), "LICENSE-Play.txt")),
            "LICENSE-Play.txt must ship next to the Play faces");
        Assert.True(File.Exists(Path.Combine(FontsDir(), "LICENSE-NotoSansCJK.txt")),
            "LICENSE-NotoSansCJK.txt must ship next to the Noto faces");
    }

    [Fact]
    public void No_maplestory_reference_survives_in_the_app_project()
    {
        var projectRoot = Path.Combine(RepoRoot(), "TarkovHelper");
        var extensions = new[] { ".cs", ".xaml", ".csproj", ".md", ".txt", ".json", ".xml", ".ps1" };
        var skippedDirs = new[] { "bin", "obj" };

        var survivors = new List<string>();
        foreach (var path in Directory.EnumerateFiles(projectRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(projectRoot, path);
            if (skippedDirs.Any(dir =>
                    relative.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            if (!extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            if (File.ReadAllText(path).Contains("maplestory", StringComparison.OrdinalIgnoreCase))
            {
                survivors.Add(relative);
            }
        }

        Assert.True(survivors.Count == 0,
            "Maplestory was removed by the EFT font stack change; stale references:\n"
            + string.Join("\n", survivors));

        // The unreferenced duplicate at the repo root is gone too.
        Assert.False(File.Exists(Path.Combine(RepoRoot(), "fonts", "Maplestory Light.ttf")),
            "The repo-root fonts/Maplestory Light.ttf duplicate must stay deleted");
    }
}
