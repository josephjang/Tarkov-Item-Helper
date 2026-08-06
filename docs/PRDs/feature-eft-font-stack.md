# EFT Font Stack — PRD

- **Created**: 2026-08-06

> The sibling `feature-eft-font-stack.spec.md` holds the technical design. Write
> this on the work's branch and merge it in the same PR as the work. Nothing is
> kept current: fields are written once, discoveries are appended. A later change
> that reverses a decision here appends `Superseded by <doc>` below this line, in
> the PR that reverses it.

## Summary

Replace the app-wide "Maplestory Light" UI font with the typography Escape from
Tarkov itself uses: bundled Bender (Regular + Bold) for Latin/Cyrillic with the
bundled OFL face Play as glyph backstop, bundled Noto Sans CJK KR for Korean,
and system-referenced Meiryo for Japanese. The app is a second screen for the
game; its text should look like the game's.

## Problem

The app renders every screen in MapleStory's font — a rounded, playful face from
a cartoon MMORPG — while sitting next to Escape from Tarkov's hard-edged
military UI. Users alt-tab between the two constantly (quest tracking during
raids), and the style clash makes the app feel unrelated to the game it serves.
Bold text is additionally fake: the app ships only a Light face, so every bold
label is a synthetically thickened Light.

## Goals

- Text in the app matches the game's task-screen typography in each supported
  language: exactly for Latin/Cyrillic and Korean (the game's faces ship with
  the app), and exactly for Japanese on machines with Meiryo, with a
  Japanese-form fallback (Yu Gothic) elsewhere.
- Bold text is a true bold cut, not synthesized.
- The change is invisible operationally: no settings, no font options, nothing
  for the user to configure.

## Non-Goals

- TarkovDBEditor (internal tool) keeps the system font.
- Chinese/Thai parity — the app has no ZH/TH localization.
- The icon glyph font (Segoe Fluent Icons) and the monospace map-coordinate
  readout (Consolas) are deliberate and unchanged.
- Retuning the default font size; user-selectable font families.

## Requirements / Acceptance Criteria

R1. All windows (main, overlay minimap, dialogs) render Latin and digits in
    the bundled Bender — the same letterforms as the game's "Gunsmith -
    Vector 9x19" rows; the bundled Play face (a square technical grotesque)
    backstops any glyph Bender lacks.
R2. With the app language set to Korean, Hangul renders in Noto Sans CJK KR —
    the same letterforms as the game's Korean task screen.
R3. With the app language set to Japanese, kana/kanji render in Meiryo on
    machines that have it (all JP-locale Windows), and in a Japanese-form
    fallback (Yu Gothic) otherwise. Japanese text never shows Korean-variant
    ideograph forms (e.g. 進 keeps its one-dot 辶).
R4. Text marked bold renders a real bold face in all three fonts that ship
    with the app (Bender, Play, Noto Sans CJK KR).
R5. Switching language in the header re-renders visible text with the correct
    font stack immediately, without restart.
R6. The release package contains the font license texts (the OFL texts for
    Play and for Noto, and the Bender attribution/provenance notice), and the
    app update remains a single zip.

## Product Decisions

**Replicate the game's fonts exactly; bundle what the authors distribute
freely and reference the rest as system fonts.** The font identities were
extracted from the game's own files (TextMeshPro font assets in
`resources.assets`: Bender SDF, NotoSans-Korean SDF, Meiryo-Japanese SDF) and
verified against screenshots by pixel analysis. Bender and Noto ship with the
app; Meiryo (Microsoft-licensed, never freely distributable) stays a system
reference.

**Bundle Bender on its authors' freeware distribution basis.** Bender has no
formal license text — the historical Font Squirrel package shipped the
restrictive MyFonts EULA, and the "OFL" label on aggregator sites is
unattributed boilerplate on byte-identical binaries. The basis for bundling is
first-party: the authors' own 2009 freeware project page permitted free
distribution, and TypeType — the foundry co-founded by author Ivan Gladkikh —
today offers Bender free "for commercial or personal purposes without any
restrictions" (Regular only; the Bold cut ships from the byte-identical family
all distributors carry). The package therefore carries an
attribution/provenance notice, `Fonts\LICENSE-Bender.txt`, naming the
copyright holders and citing both statements, with removal promised on any
rights-holder objection.

**Keep Play (OFL) embedded after Bender as glyph backstop.** Play is a square
technical grotesque with Regular + Bold cuts, full Latin + Cyrillic coverage,
and an unambiguous embedding license — chosen (over Jura and Exo 2) because a
formally licensed fallback behind a freeware-basis face is cheap insurance
(~0.4 MB) for any glyph or dispute contingency.

**Bundle the full Noto Sans CJK KR (Regular + Bold, ~33 MB) rather than a
Korean subset (~10 MB).** The full font is byte-for-byte the design the game
uses, covers Japanese and hanja as the app's last-resort CJK fallback, and the
size cost lands on a desktop app whose users install multi-gigabyte game
updates routinely. The subset option was offered and declined.

**Reference Meiryo as a system font rather than bundling it.** Meiryo is a
Microsoft-licensed font and cannot be redistributed. JP-locale Windows installs
it automatically, so the audience that reads Japanese gets the exact game font;
everyone else falls back to Yu Gothic, which preserves Japanese glyph forms.
Bundling an OFL Japanese font (Noto Sans CJK JP) was considered and rejected:
it would add another ~33 MB to ship a font the game itself does not use.

**Remove MapleStory entirely rather than keeping it as the Korean face.**
Keeping it was considered (it covers Hangul and is the app's current look), but
its rounded style is the problem being solved, and the game's actual Korean
font is freely bundleable.

## Risks

- The update zip grows ~20–30 MB and every auto-update downloads it in full.
  Accepted: one-time decision, typical for game companion tools.
- On non-Japanese Windows without Meiryo, Japanese text approximates the game
  (Yu Gothic) rather than matching it. Accepted: correct glyph forms are
  preserved, and the affected audience (JA readers on non-JP Windows) is small.
- Bender's redistribution basis is the authors' freeware-project statement
  and TypeType's current free distribution — not a formal license text; the
  font's embedded metadata still reads "All rights reserved". Accepted by the
  maintainer: attribution and provenance ship in `Fonts\LICENSE-Bender.txt`,
  and the fonts will be removed promptly on any rights-holder objection.
- CJK glyphs are taller than the Latin face's; line-height clipping in dense
  rows was the concern. Measured before merge, the shipped chain's CJK ink is
  *smaller* than its Latin ink at every size, so no container sized for Latin
  can clip it (guarded in FontAssetsTests); the KO/JA visual sweep still runs
  for letterform correctness.
