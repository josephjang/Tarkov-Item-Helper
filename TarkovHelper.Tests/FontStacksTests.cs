using TarkovHelper.Services;

namespace TarkovHelper.Tests;

/// <summary>
/// Guards the per-language chain ordering decisions recorded in
/// docs/PRDs/feature-eft-font-stack.spec.md: the embedded Bender (the game's
/// Latin/Cyrillic face) leads every chain with the embedded Play right after
/// it as glyph backstop, and Meiryo must precede the bundled Noto for
/// Japanese while Korean keeps Noto ahead of every system Japanese font.
/// </summary>
public sealed class FontStacksTests
{
    private const string BenderToken = "./Fonts/#Bender";
    private const string PlayToken = "./Fonts/#Play";
    private const string NotoToken = "./Fonts/#Noto Sans CJK KR";

    [Fact]
    public void Every_language_yields_a_chain_ending_in_segoe_ui()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var chain = FontStacks.ForLanguage(language);
            Assert.False(string.IsNullOrWhiteSpace(chain));
            // Segoe UI is the terminal fallback so no script ever renders in
            // WPF's last-resort global composite.
            Assert.EndsWith("Segoe UI", chain);
        }
    }

    [Fact]
    public void Every_chain_leads_with_bundled_bender_then_play()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var chain = FontStacks.ForLanguage(language);
            Assert.StartsWith(BenderToken + ", ", chain);
            Assert.True(chain.IndexOf(PlayToken, StringComparison.Ordinal) > 0,
                $"{language} chain must carry the bundled Play backstop: {chain}");
        }
    }

    [Fact]
    public void En_and_ko_share_a_chain_with_noto_before_system_japanese_fonts()
    {
        var en = FontStacks.ForLanguage(AppLanguage.EN);
        var ko = FontStacks.ForLanguage(AppLanguage.KO);
        Assert.Equal(en, ko);

        // Korean mode must not let a Japanese-form system font capture hanja or
        // full-width punctuation ahead of the game's own Korean face.
        var noto = ko.IndexOf(NotoToken, StringComparison.Ordinal);
        var yuGothic = ko.IndexOf("Yu Gothic", StringComparison.Ordinal);
        Assert.True(noto >= 0, "EN/KO chain must embed Noto: " + ko);
        Assert.True(yuGothic > noto,
            "EN/KO chain must order the bundled Noto ahead of Yu Gothic: " + ko);
        Assert.DoesNotContain("Meiryo", ko);
    }

    [Fact]
    public void Ja_chain_orders_meiryo_then_yu_gothic_then_noto()
    {
        var ja = FontStacks.ForLanguage(AppLanguage.JA);
        var meiryo = ja.IndexOf("Meiryo", StringComparison.Ordinal);
        var yuGothic = ja.IndexOf("Yu Gothic", StringComparison.Ordinal);
        var noto = ja.IndexOf(NotoToken, StringComparison.Ordinal);

        Assert.True(meiryo >= 0, "JA chain must reference system Meiryo: " + ja);
        Assert.True(yuGothic > meiryo,
            "Yu Gothic is the fallback for machines without Meiryo and must follow it: " + ja);
        Assert.True(noto > yuGothic,
            "The bundled Noto is the JA last-resort CJK face and must follow both: " + ja);
    }
}
