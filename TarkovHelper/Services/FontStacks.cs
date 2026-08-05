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
    /// Returns the composite FontFamily chain string for the given language.
    ///
    /// Tokens are relative (./Fonts/#Family), never absolute pack URIs: WPF's
    /// FontFamily parser splits composite strings on commas, and a
    /// pack://application:,,,/ URI embeds commas that tokenize into garbage.
    /// Relative tokens resolve against the base URI supplied by App.xaml's
    /// BAML or by the two-argument FontFamily constructor in
    /// App.ApplyFontStack.
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
