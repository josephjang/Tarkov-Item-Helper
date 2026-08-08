# EFT Font Stack — Technical Spec

- **Created**: 2026-08-06

> The sibling `feature-eft-font-stack.md` holds the product decision. Write this
> on the work's branch and merge it in the same PR as the work. Nothing is kept
> current: fields are written once, discoveries are appended. A later change
> that reverses a decision here appends `Superseded by <doc>` below this line,
> in the PR that reverses it.

## Summary

One `AppFont` composite `FontFamily` resource, swapped per language at runtime,
replaces the static `MaplestoryFont`. WPF's per-glyph composite fallback does
the script routing: the embedded Bender serves Latin/Cyrillic (embedded Play
backstops it), the embedded Noto Sans CJK KR serves Korean, and the Japanese
chain routes kana/kanji to system Meiryo before the embedded Noto. Bender,
Play, and Noto all ship Regular + Bold as embedded resources (Bender's
redistribution basis: the sibling PRD's Product Decisions).

## Non-Goals

- No per-user font configuration; no font settings persistence.
- No change to `IconFont` (Segoe Fluent Icons composite), the Consolas
  coordinate readout in `MapPage`, or anything in TarkovDBEditor.

## Current Behavior

`App.xaml` defines `MaplestoryFont` as
`pack://application:,,,/Fonts/#Maplestory` and applies it through ten implicit/
named style setters (Window, TextBlock, TextBox, Button, CheckBox, ComboBox,
ComboBoxItem, TabButtonStyle, Menu, MenuItem), all `StaticResource`. One
code-behind lookup exists (`QuestListPage` objective rendering, via
`FindResource("MaplestoryFont")`). The single embedded face is
`Fonts\Maplestory Light.ttf`; every `FontWeight="Bold|SemiBold"` site (159)
renders a synthesized bold. Japanese text already falls back to system fonts
today — Maplestory has no kana. Font sizes are already runtime-swappable
(`App.ApplyBaseFontSize` writing `Resources["BaseFontSize"]`); font family is
not.

## Design

**Files added**: `Fonts\Bender-Regular.otf`, `Fonts\Bender-Bold.otf`,
`Fonts\Play-Regular.ttf`, `Fonts\Play-Bold.ttf`,
`Fonts\NotoSansCJKkr-Regular.otf`, `Fonts\NotoSansCJKkr-Bold.otf`,
`Fonts\LICENSE-Bender.txt`, `Fonts\LICENSE-Play.txt`,
`Fonts\LICENSE-NotoSansCJK.txt` (all under `TarkovHelper\`),
`Services\FontStacks.cs`, and this document pair.
**Files removed**: `TarkovHelper\Fonts\Maplestory Light.ttf` and the
unreferenced duplicate `fonts\Maplestory Light.ttf` at the repo root.

`FontStacks.ForLanguage(AppLanguage)` returns the chain string:

- EN, KO: `./Fonts/#Bender, ./Fonts/#Play, ./Fonts/#Noto Sans CJK KR, Malgun Gothic, Yu Gothic UI, Segoe UI`
- JA: `./Fonts/#Bender, ./Fonts/#Play, Meiryo, Yu Gothic, ./Fonts/#Noto Sans CJK KR, Segoe UI`

Bender covers Latin, most Latin-1 accents (76/96), modern Russian Cyrillic
(94 of the Cyrillic block's 256 codepoints), and typographic punctuation in
both weights; Play is load-bearing, not insurance — it supplies the rest of
Latin-1/Latin Ext-A and most of the wider Cyrillic block (216/256) before the
chain reaches the CJK faces, so Play cannot be dropped without losing ~160
Cyrillic codepoints to system fallback.

`App.xaml` keeps the EN/KO chain as the compiled default; `App.ApplyFontStack`
(modeled on `App.ApplyBaseFontSize`) overwrites `Resources["AppFont"]` with
`new FontFamily(new Uri("pack://application:,,,/"), FontStacks.ForLanguage(lang))`
at startup and on `LocalizationService.LanguageChanged`. The ten style setters
move from `StaticResource` to `DynamicResource` so the swap propagates live —
the same pattern the `FontSize*` resources already use. The `QuestListPage`
lookup only renames its key: `FindResource` resolves current dictionary state.

The csproj replaces the Maplestory `<Resource>` with six explicit per-file
`<Resource Include="Fonts\...">` entries (explicit so the test-suite
cross-check of csproj-vs-directory is meaningful) and a
`<None Update="Fonts\*.txt">` with `PreserveNewest`, which carries the license
texts through `dotnet publish` into the release zip. `LICENSE-Bender.txt` is
an attribution/provenance notice rather than a license text — Bender has none
(see the sibling PRD); the Play and Noto files are their OFL texts.

## Technical Decisions

**Relative `./Fonts/#Family` tokens, not absolute pack URIs, in composite
strings.** WPF's FontFamily string parser splits on commas; an absolute
`pack://application:,,,/` URI embeds commas and tokenizes into garbage inside a
multi-family string. Relative tokens resolve against the resource's base URI
(App.xaml's BAML, or the explicit base URI passed to the two-argument
`FontFamily` constructor in `ApplyFontStack`) and work from every window and
from code.

**Per-language chains rather than one static chain.** A single chain cannot
serve both CJK languages faithfully: Meiryo must precede Noto for Japanese, but
then Meiryo would also capture kana, hanja, and full-width punctuation in
Korean mode with Japanese glyph forms the game's Korean screen does not use.
The runtime-swap machinery already exists for font sizes, so the marginal cost
is one helper class and one event subscription.

**`DynamicResource` for the ten `FontFamily` setters.** Prerequisite for the
runtime swap. The lookup overhead is negligible at UI scale, and the font-size
resources have used the same mechanism app-wide since the base-font-size
feature shipped.

**Ship font files as-is (Bender and Noto as CFF OTF, Play as TTF); verify
with `GlyphTypeface` in CI; never patch name tables.** WPF renders both
outline formats via DirectWrite. The unit tests open every shipped file and
assert family grouping (the file pairs must join exactly the "Bender", "Play",
and "Noto Sans CJK KR" families, each with true 400/700 weights), so a bad
face fails CI rather than silently faux-bolding. Editing name tables is ruled
out — both OFL license headers declare Reserved Font Names, and Bender ships
byte-identical to its distributed form on purpose (provenance is its basis).

**Tests load fonts by file URI from the source tree, not pack URI.**
`pack://application:` requires WPF application bootstrapping that is brittle
under the xunit host; the files on disk are byte-identical to what gets
embedded, and the repo-root walk (`PrdDocsTests.RepoRoot` pattern) locates them
deterministically.

**Plain git commit, no LFS.** The repo already commits multi-megabyte binaries
(`tarkov_data.db`, the Maplestory TTF); the largest font is ~17 MB, far under
GitHub's limits; introducing LFS would complicate clone/CI/release for zero
benefit.

## Open Questions

- Whether the Font Squirrel Bender build's name tables group Regular + Bold
  into one WWS family. Settled by the mandatory pre-wiring inspection
  (`Fonts.GetFontFamilies` over the downloaded files); contingency is an
  alternate distribution, and the blocker is escalated if none passes.
  *Answered during implementation: it does — one "Bender" family with true
  weights, so the embedded Regular + Bold resolve without simulation.*

## Test Strategy

- **Unit** (`FontAssetsTests`): every shipped font opens as a `GlyphTypeface`
  (format-parse guard); the App.xaml default equals `FontStacks.ForLanguage(EN)`;
  chain `./Fonts/#` fragments match the exactly-three embedded families; Bold
  and Normal typefaces resolve with `StyleSimulations.None`; glyph coverage
  (Bender and Play: A–Z, digits, Cyrillic; Noto: 가 한 あ ア 調 査); csproj
  `<Resource>` entries ↔ `Fonts\` directory contents; no "Maplestory"
  reference survives anywhere in `TarkovHelper\`.
- **Unit** (`FontStacksTests`): JA chain orders Meiryo → Yu Gothic → Noto;
  EN/KO chains are identical and order Noto ahead of system Japanese fonts;
  every chain leads with embedded Bender then embedded Play; every
  `AppLanguage` value yields a chain.
- **Unit** (`AppFontSwapTests`): `App.ApplyFontStack` actually swaps
  `Resources["AppFont"]` to the requested language's chain with the pack base
  URI attached — the runtime half the string tests can't see.
- **Unit** (`FontPackUriTests`): every embedded `./Fonts/#Family` chain token
  resolves to a real embedded face through a pack URI (the test host addresses
  the app assembly via the `pack://application:,,,/TarkovHelper;component/`
  form), with a negative control proving the assertion can fail.
- **E2E**: the existing suite boots the real app, but WPF font resolution
  never throws — an unresolvable `./Fonts/#Family` token substitutes silently,
  so e2e catches only total startup failure, not font fallback. The
  assembly-embedding half is covered by
  `FontAssetsTests.Fonts_are_embedded_in_the_app_assembly` (ResourceReader
  over `TarkovHelper.g.resources`) and the resolution half by
  `FontPackUriTests`; only visual appearance stays manual.
- Visual appearance (correct letterforms per language, clipping, bold weight)
  cannot be asserted automatically; it is covered by the manual verification
  below.

## Verification

- `dotnet build TarkovHelper.sln`
- `dotnet test TarkovHelper.sln --filter "Category!=E2E"`, then
  `--filter "Category=E2E"`
- `dotnet test TarkovHelper.Tests --filter "FullyQualifiedName~FontStacksTests"`
  in isolation — deterministically covers the FontStacks type-initializer
  (pack UriParser registration) that a full-suite run can mask via ordering.
- Run `dotnet TarkovHelper\bin\Debug\net8.0-windows\TarkovHelper.dll` with
  `TARKOVHELPER_CONFIG_PATH` pointed at a scratch dir: EN shows Bender; KO
  shows Noto Hangul; JA shows Japanese forms (on a machine without Meiryo,
  Yu Gothic — 進 must keep the one-dot radical); switching language live
  re-renders; bold is a true cut; icons and the map coordinate readout are
  unchanged; overlay and dialogs inherit.
- `./build/Create-ReleasePackage.ps1`: the zip contains
  `Fonts/LICENSE-Bender.txt`, `Fonts/LICENSE-Play.txt`, and
  `Fonts/LICENSE-NotoSansCJK.txt`.

## Risks & Migration

- No data migration; user data is untouched. Rollback is a single-commit
  revert (fonts, markup, and tests travel together).
- The composite's line metrics come from the first Latin face (Bender,
  1.130 em) while the CJK faces are internally taller (Noto, 1.448 em).
  Measured, this does not clip: Noto's Hangul/kanji/full-width ink stays
  inside the envelope Bender's own accented Latin already occupies at every
  size the app can render, and it is *smaller* than the removed Maplestory
  face produced for the same Korean text (12.86px vs 13.77px at 12px em,
  against a 13.56px line box — Maplestory's Korean ink overflowed its own
  13.38px box). `FontAssetsTests.Cjk_ink_stays_inside_the_chains_latin_ink_envelope`
  pins this; if a future chain change does clip, the remediation is an
  explicit `LineHeight` on the affected containers, not a stack reorder.
- On machines without Meiryo the JA chain silently degrades to Yu Gothic; this
  is by design and documented in the sibling PRD's Risks.
- The EN/KO chain resolves CJK glyphs through the embedded ~16 MB Noto face
  before system Malgun Gothic, so the first CJK glyph rendered in a session
  parses that face on the UI thread (one-time per process). Accepted: the
  ordering exists precisely so hanja and full-width punctuation keep Korean
  glyph forms; reordering would trade correctness for a one-off load cost.
- Non-linguistic symbols the app renders as text (✓ ○ ▲ ▼ ↑ ↓) are not in
  Bender/Play, so they route to the first CJK-capable face. Measured, only ✓
  actually differs in width between chains (9.56px on EN/KO via Noto vs
  14.00px on JA via the system JA face, at 14px em); the other five are
  full-width 14.00px in both. Cosmetic today (the glyph cells have slack; ✓
  was already a fallback glyph under Maplestory), and Noto is the only named
  family in the EN/KO chain that covers the six at all — the coverage test
  pins them so a future font swap can't silently drop them. Pin a fixed
  symbol-capable family on those TextBlocks if alignment ever matters.
