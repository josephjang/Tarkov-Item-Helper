using System.Windows.Media;

namespace TarkovHelper.Services;

/// <summary>
/// Composite font-family chains replicating Escape from Tarkov's UI typography
/// per app language. WPF resolves composite chains per glyph, so each script
/// lands on the first family in the chain that covers it.
///
/// Bender — the game's actual Latin/Cyrillic face — ships embedded (Regular +
/// Bold; provenance in Fonts\LICENSE-Bender.txt), with the OFL face Play
/// embedded right after it as a backstop for any glyph Bender lacks. Japanese
/// uses the game's face as a system reference: Meiryo (Microsoft-licensed,
/// cannot ship) with a Yu Gothic fallback. See
/// docs/PRDs/feature-eft-font-stack.md for the decision trail.
/// </summary>
public static class FontStacks
{
    /// <summary>
    /// The base URI the chains' relative ./Fonts/#Family tokens resolve
    /// against. Every FontFamily built from a chain must use this URI —
    /// <see cref="CreateFontFamily"/> is the single construction path.
    /// </summary>
    public static readonly Uri PackBaseUri = CreatePackBaseUri();

    /// <summary>
    /// WPF registers the "pack" UriParser from its own static initialization,
    /// not from the URI machinery: in a host that has not touched WPF yet,
    /// new Uri("pack://application:,,,/") throws UriFormatException ("Invalid
    /// port specified") because ",,," parses as an authority with a port. The
    /// running app happens to construct Application first (Program.Main), but
    /// nothing enforces that order and a filtered test run does not — so force
    /// the registration here instead of depending on it.
    /// </summary>
    private static Uri CreatePackBaseUri()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        return new Uri("pack://application:,,,/");
    }

    /// <summary>
    /// The compiled default consumed by App.xaml via x:Static (the EN/KO
    /// chain). Sharing one FontFamily instance between the XAML default and
    /// <see cref="CreateFontFamily"/> keeps a single source of truth for the
    /// chain string — App.xaml can no longer drift from this class.
    /// </summary>
    public static readonly FontFamily DefaultFamily = CreateFontFamily(AppLanguage.EN);

    /// <summary>
    /// Builds the runtime FontFamily for a language. The two-argument
    /// constructor supplies the base URI that the chain's relative
    /// ./Fonts/#Family tokens resolve against; BAML-compiled resources get it
    /// implicitly, code-created FontFamily instances do not.
    /// </summary>
    public static FontFamily CreateFontFamily(AppLanguage language) =>
        new(PackBaseUri, ForLanguage(language));

    /// <summary>
    /// Returns the composite FontFamily chain string for the given language.
    ///
    /// Tokens are relative (./Fonts/#Family), never absolute pack URIs: WPF's
    /// FontFamily parser splits composite strings on commas, and a
    /// pack://application:,,,/ URI embeds commas that tokenize into garbage.
    /// Relative tokens resolve against the base URI supplied by
    /// <see cref="PackBaseUri"/> (via <see cref="CreateFontFamily"/>) or by
    /// App.xaml's BAML for the compiled default.
    /// </summary>
    public static string ForLanguage(AppLanguage language) => language switch
    {
        // Japanese: Meiryo must precede the bundled Noto so kana/kanji render
        // Japanese glyph forms (e.g. 進 keeps its one-dot 辶); Yu Gothic covers
        // machines without Meiryo while preserving Japanese forms.
        AppLanguage.JA => "./Fonts/#Bender, ./Fonts/#Play, Meiryo, Yu Gothic, ./Fonts/#Noto Sans CJK KR, Segoe UI",

        // English/Korean (and the default for any future language): the bundled
        // Noto Sans CJK KR — the game's own Korean face — serves all CJK, so
        // hanja and full-width punctuation keep Korean forms in KO mode.
        _ => "./Fonts/#Bender, ./Fonts/#Play, ./Fonts/#Noto Sans CJK KR, Malgun Gothic, Yu Gothic UI, Segoe UI",
    };
}
