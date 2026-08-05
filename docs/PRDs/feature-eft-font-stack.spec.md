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

Bender covers Latin, Latin-1 accents, the full Cyrillic block, and
typographic punctuation in both weights; Play backstops any glyph it lacks
before the chain reaches the CJK faces.

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
- **E2E**: the existing suite boots the real app — a font resource that fails
  to parse or load would fail startup/rendering there.
- Visual appearance (correct letterforms per language, clipping, bold weight)
  cannot be asserted automatically; it is covered by the manual verification
  below.

## Verification

- `dotnet build TarkovHelper.sln`
- `dotnet test TarkovHelper.sln --filter "Category!=E2E"`, then
  `--filter "Category=E2E"`
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
- The composite's line metrics come from the first Latin face while CJK glyphs
  are taller; if clipping appears in the KO/JA visual sweep, the remediation is
  an explicit `LineHeight` on the affected containers, not a stack reorder.
- On machines without Meiryo the JA chain silently degrades to Yu Gothic; this
  is by design and documented in the sibling PRD's Risks.
