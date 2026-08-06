# Quest Chip-Only Status Filter — Technical Spec

- **Created**: 2026-08-07

> The sibling `feature-quest-chip-only-status-filter.md` holds the product
> decision. Write this on the work's branch and merge it in the same PR as the
> work. Nothing is kept current: fields are written once, discoveries are
> appended. A later change that reverses a decision here appends
> `Superseded by <doc>` below this line, in the PR that reverses it.

## Summary

`CmbStatus` is deleted and `QuestListPage` gets a `_statusTag` field as the single
source of status-filter truth — the chips stop routing through a combo and drive
`ApplyFilters` directly. "All" joins `QuestStatusTags.ChipTags`, so its chip count
falls out of the existing `CountByStatusTag` pass for free. Chip selection state
is published to UI Automation via `AutomationProperties.ItemStatus`
("Selected"/"Unselected"), which is what the e2e tests read now that there is no
combo to probe. Chip visuals (colored border always, translucent fill when
selected) are derived once from each chip's `Foreground` color.

## Non-Goals

- No re-architecture of `ApplyFilters` — it stays the single apply/render pass.
- The accepted "persistence rides the render pass" risk recorded in
  `feature-quest-overview-filters.spec.md` is unchanged by this work; the
  `_isDataLoaded` gate stays load-bearing.
- `MapPage`'s `CmbStatusFilter` and the harness's `WaitForComboSelection` are
  untouched — `MapStateE2ETests` still uses it. (`AppDriver.SelectComboItemByName`
  is a different story: this change deletes its last caller, so it goes with them.)

## Current Behavior / Root Cause

The chips have no state of their own: `StatusChip_Click` mutates `CmbStatus` via
`SelectComboByTag`, the combo's `SelectionChanged` calls `ApplyFilters`, and
`UpdateStatusChips` derives chip visuals from the criteria snapshot — the
"Chips route through `CmbStatus`" decision of
`feature-quest-overview-filters.spec.md`. `ApplyFilters` reads the status from
the combo's selected item; `RestoreFilterSettings` restores it with
`SelectComboByTag(CmbStatus, settings.StatusTag, QuestStatusTags.All)`, whose
tag-matching is also what absorbs an unknown persisted tag (no ComboBoxItem
matches → fallback "All"). `TxtStats` is an interpolated
`"Lv.{level} | {shown}/{total}"` set at the end of `ApplyFilters`. Selected-chip
visuals are a neutral `BackgroundLightBrush` highlight; unselected borders are
the neutral `BorderBrush` resource, and the chip style's `IsMouseOver` trigger
swaps the background to `BackgroundLightBrush`.

## Design

Changed files:

- `TarkovHelper/Pages/QuestListFilter.cs` — `ChipTags` becomes
  `{ All, Active, Locked, Done, Failed, Unavailable }` (All first, display order)
  and an `ImmutableArray<string>`, because it is now the allow-list persisted user
  data is validated against and `readonly` would pin only the reference. New on
  `QuestStatusTags`: `IsKnown(string?)` (ordinal membership), `Coerce(string?)`
  (the restore policy — known tag kept, anything else widened to All),
  `NextTag(current, clicked)` (the chip toggle rule), and the
  `ChipSelected`/`ChipUnselected` ItemStatus constants the e2e harness reads.
  Comment updates (tags are chip tags now, not ComboBoxItem tags). No predicate
  changes: `MatchesStatusTag` already returns true for "All" and
  `MatchesFaction(vm, faction, "All")` already hides other-faction quests, so
  `CountByStatusTag` handed "All" yields exactly the list-under-All count.
- `TarkovHelper/Pages/QuestListPage.xaml` — delete the `CmbStatus` block and
  `TxtStats`, plus the four unreferenced `Status*Brush` resources; statistics-bar
  grid goes 3 → 2 columns (chips, gauge); add `ChipAll` first in the chip panel
  (`Foreground = TextPrimaryBrush`, neutral — All is the absence of a status
  filter); the chip panel becomes a `WrapPanel` (see Technical Decisions); the
  faction toggle's left margin drops to 0 (CmbMap's right margin now provides the
  16px gap, noted at both sites); `StatusChipStyle`'s hover trigger changes from a
  Background swap to a pair of `Foreground`-tinted overlay layers (see Technical
  Decisions).
- `TarkovHelper/Pages/QuestListPage.xaml.cs` — new `_statusTag` field seeded
  `QuestListSettings.DefaultStatusTag`; `StatusChip_Click` resolves the clicked
  chip's entry, applies `QuestStatusTags.NextTag` and calls `ApplyFilters`
  directly (keeping the `_isDataLoaded` guard); `ApplyFilters` builds the criteria
  from `_statusTag`; `RestoreFilterSettings` seeds it via `Coerce`; `ResetFilters`
  sets it to All (outside the suppression scope — a field write raises nothing);
  `CmbStatus_SelectionChanged` is deleted. `StatusChips` becomes a
  `StatusChipEntry` record-struct array built by `BuildStatusChips`, which reads
  each chip's tag from its own XAML `Tag` and throws unless the resulting tags
  equal `ChipTags` exactly. Each entry carries three brushes derived once from the
  chip's `Foreground` color and frozen: a 0x33-alpha fill, the full-strength
  selected border, and a 0x66-alpha unselected border. `UpdateStatusChips` paints
  from that snapshot only (selected: fill + full-color border + SemiBold;
  unselected: transparent + dimmed color border) and publishes
  `AutomationProperties.ItemStatus` per chip, gated on `_isDataLoaded`.
  `RefreshDisplay` now delegates to `RefreshAllForStateChange` (see Technical
  Decisions).
- `TarkovHelper/Services/Settings/QuestListSettings.cs` — comment-only (the
  default and the key no longer describe a combo).
- `TarkovHelper.Tests/QuestListFilterTests.cs` — count tests run over the
  production `ChipTags` (now including All); new tests for the All-count
  semantics, `IsKnown`, `Coerce`, `NextTag`, and a literal set/order oracle for
  `ChipTags` itself.
- `TarkovHelper.Tests/E2ETestHarness.cs` — `AppDriver.GetItemStatus` and the
  poll-safe `TryGetItemStatus`; `AppDriver.SelectComboItemByName` is deleted (this
  change removed its last caller).
- `TarkovHelper.Tests/QuestTabDriver.cs` (new) — the Quests-tab page object:
  `StatusChipId` / `SelectedStatusChips` / `WaitForSelectedStatusChip` /
  `SelectStatusChip`, plus `ShowQuestDetail` moved off `E2ETestBase` (which is
  shared with the map, header and window-bounds suites and has no business
  carrying `QuestListPage`'s chip conventions).
- `TarkovHelper.Tests/QuestStatusChipStyleTests.cs` (new) — pins the two chip-style
  properties no C#-side test can see: the hover cue is tinted from the chip's own
  `Foreground`, and it does not fade the label.
- `TarkovHelper.Tests/QuestOverviewFiltersE2ETests.cs`,
  `TarkovHelper.Tests/QuestNavigationE2ETests.cs` — combo probes/selects become
  chip waits/selects (`QuestCascadeConfirmE2ETests` is unaffected: it goes
  through `ShowQuestDetail`), plus new coverage for the All-on-All no-op and for
  every chip count matching the row count its click produces.

Data flow after the change: chip click → `_statusTag` → `ApplyFilters` snapshot
(`StatusTag: _statusTag`) → filter/sort/empty-state/persist as before →
`UpdateStatusChips` (counts, visuals, ItemStatus) → kappa gauge. Restore:
`QuestListPage_Loaded` → `RestoreFilterSettings` seeds `_statusTag` → first
`ApplyFilters` renders it. Persistence is unchanged: same `questList.statusTag`
key via `SaveFilterSnapshot`.

## Technical Decisions

**Chips own the status state as a plain field.** The previous spec's decision
("no separate chip state to drift out of sync") was about *two* controls sharing
one filter; with the combo deleted the chips are the only control, and the field
they drive is the criteria's own input, so there is nothing left to drift.
Alternatives — keeping a hidden combo as the state holder, or a dependency
property — add machinery for no observer.

**Restore validation moves into `QuestStatusTags.Coerce`.** The combo's
tag-matching fallback used to absorb unknown persisted tags as a side effect of
`SelectComboByTag`. With no combo, the validation must be explicit, and it must
widen to "All" (never the narrower "Active" default) — the existing e2e test for
an unknown persisted tag pins exactly this. The policy lives with the tag table
rather than at the call site, so a future second reader of `questList.statusTag`
inherits the safe widening instead of re-deriving it; `IsKnown` stays public as
the membership half. Pure statics, so both are unit-testable without WPF. Note
`Coerce` never sees an empty value: `QuestListSettings.StatusTag` substitutes the
fresh-install default for a missing row, because "no stored preference" and "a tag
this build does not know" are different questions with different answers.

**The chip row is a `WrapPanel`, not a `StackPanel`.** The row measures ~520px and
the window's `MinWidth` is 600, so a horizontal `StackPanel` in the statistics
bar's `*` column had its trailing chips clipped on a narrow window. That was
survivable while `CmbStatus` existed — it lived in the filter bar's `Auto` column
and still offered every status — but with the chips as the only status filter, a
clipped chip is a status the user cannot select. Wrapping keeps every chip
clickable at any width and costs nothing at full width; the statistics-bar row is
`Height="Auto"`, so it grows only when it has to. A horizontal `ScrollViewer` was
rejected: it hides chips behind a gesture instead of showing them.

**Chip tags have one declaration, and the chip row is pinned to `ChipTags`.**
`StatusChip_Click` routes by the Button's XAML `Tag`, so a second copy of the tag
in the C# table could disagree with it — a chip that filters to one status while a
different chip paints as selected. `ChipEntry` therefore reads the tag from
`chip.Tag`, and `BuildStatusChips` throws unless the resulting tags equal
`ChipTags` exactly, in order. That one check also closes the two failure modes the
`Coerce` allow-list would otherwise let through: a tag in `ChipTags` with no chip
(accepted from the database, filters the list, nothing renders selected) and a
chip whose tag is missing from `ChipTags` (works in-session, widened back to "All"
on every relaunch). The chip's rendered label is that same tag rather than a
separate `Label` field: with the "N/A" relabel gone every label equalled its tag,
so the field was a third copy — and the only one nothing validated. A first-paint
exception is a deterministic failure any run
catches; the alternatives were silent.

**ItemStatus is published only once `_isDataLoaded`.** `ApplyFilters` can run
before `Loaded` restores the saved filters (that is why the persistence write is
gated on the same flag), and `StatusChip_Click` drops clicks until then. Without
the gate, such a pass would publish `Selected`/`Unselected` while clicks were still
being discarded, and the harness's "chips have a status, so they are ready" wait
would let a click through to be swallowed — a 30-second timeout with no retry. The
gate makes the readiness signal exactly true rather than coincidentally true. Chip
counts and colors still paint on those early passes; only the automation property
waits.

**`RefreshDisplay` delegates to `RefreshAllForStateChange`.** Not strictly part of
the chip work, but this change makes it load-bearing: the PRD justifies dropping
the level readout on the grounds that "level's effect on quests is visible where it
matters", and `RefreshDisplay` is the only refresh path for player-level and
Scav-Rep edits (the page subscribes to neither event; `MainWindow` calls the method
directly). It was a second, shorter copy of the shared sequence that omitted
`UpdateRecommendations`, so the recommendations panel and its count badge went
stale after every level edit even though level flips quests between `LevelLocked`
and `Active`. Delegating restores the "one refresh sequence" invariant the shared
method's own comment claims.

**Chips stay `Button`s; e2e reads `AutomationProperties.ItemStatus`.**
ToggleButton + UIA TogglePattern was rejected: WPF's
`ToggleButtonAutomationPeer.Toggle()` flips `IsChecked` without raising `Click`,
so the chip logic would have to move into Checked/Unchecked handlers, which the
programmatic writes in `UpdateStatusChips` would re-enter — requiring a new
suppression scope for no user-visible gain. Reading the live settings database
mid-session was also rejected (only written post-`_isDataLoaded` on the render
pass; a UIA property read is direct and synchronous). ItemStatus is the UIA
property designed for ad-hoc state, and its empty-until-first-render value
doubles as a readiness signal for the harness.

**`SelectStatusChip` is idempotent by design.** Chips toggle: a blind click on
the already-selected chip would deselect it to "All". The harness helper waits
for the chips to initialize (non-empty ItemStatus), clicks only if the target is
not already selected, then waits for Selected. Assert-only flows (e.g. the
relaunch-restore test) must use `WaitForSelectedStatusChip` instead — an invoke
racing the restore could toggle the very state being asserted.

**Brushes are derived from `Foreground` and cached in `StatusChips`.** Each
chip's XAML `Foreground` stays the single place its color is declared (the
existing chip/badge color contract). The selected fill (~`0x33` alpha), the
selected border (full strength) and the unselected border (~`0x66` alpha) are
computed and frozen once when the lazily-built `StatusChips` array is constructed
— `UpdateStatusChips` runs on every `ApplyFilters` and must not allocate brushes
per pass. All three come from that snapshot, including the selected border: paint
reads no live control property, so a chip cannot end up with a border from one
source and a fill from another. The All chip needs no special-casing: the
derivation is color-generic, so its neutral foreground yields a subtle white-tint
fill. A `Foreground` that is not a `SolidColorBrush` breaks the derivation, so it
asserts in Debug and falls back to white — a color no chip uses, rather than a gray
that would silently impersonate the Unavailable chip.

**The hover cue is two `Foreground`-tinted overlay layers.** The style's
`IsMouseOver` trigger used to swap the background to neutral
`BackgroundLightBrush`, which would visually "unfill" a selected colored chip. An
`Opacity` dim on the chip border was the first replacement and is wrong for two
reasons: `Opacity` composites the whole visual subtree, so it faded the label with
the chrome (reads "disabled", and drops `ChipLocked`'s already-low 3.78:1 contrast
to 3.09:1), and on a transparent unselected chip a 15% fade of a 1px border is
barely perceptible — no cue at all on the tab's only status control.

The template now carries two layers UNDER the label, both bound to
`{TemplateBinding Foreground}`: a ring that redraws the border at full status
strength (exactly the step an unselected chip's dimmed 0x66 border is missing) and
a 12% wash of the same color. `Foreground` is the one chip property
`UpdateStatusChips` never writes, so a XAML trigger can reach the chip's status
color without any of the code-set frozen brushes and without new page state. The
label is now a sibling of the layers rather than their descendant, so its contrast
is unaffected — hovered `ChipLocked` measures 3.33:1, better than both the
`Opacity` version and the original neutral-background hover (2.99:1). The chip's
measured size is unchanged: the padding moved from the Border to the
`ContentPresenter`'s margin, so nothing reflows and the `WrapPanel`'s wrap point is
the same.

## Test Strategy

- **Unit** (`QuestListFilterTests`): the shared tag list the count tests iterate
  becomes `QuestStatusTags.ChipTags`, so every existing count invariant (Locked
  includes LevelLocked, counts independent of the selected tag, faction
  exception, per-tag equality with `Matches`) now also covers the All tag. New:
  the All count equals the list-under-All count and is neither the sum of the
  other chips nor the raw total (explicit faction fixture); `IsKnown` accepts
  every chip tag and rejects unknown/empty/null/case-mismatched/`LevelLocked`
  tags; `Coerce` keeps known tags and widens everything else to All, never to the
  narrower default; `NextTag` selects the clicked tag, toggles the active one back
  to All, and is not special-cased for All. The chip table test asserts `ChipTags`
  against a literal expected sequence (both membership directions and the R1
  display order — an oracle derived from `ChipTags` could only agree with itself),
  cross-checked against the live `QuestStatus` enum so a new member cannot be added
  without updating it.
- **E2E** (`QuestOverviewFiltersE2ETests`): chip click applies the status,
  re-click returns to All, a direct All click works, and All-on-All changes
  neither the selection nor the row count — asserted via ItemStatus, plus
  regression pins that `CmbStatus` and `TxtStats` are gone, that every chip in
  `ChipTags` is on screen, and that the All/Unavailable chips carry the new labels
  with counts. A new test walks every chip and asserts its previewed count equals
  the number of rows its click produces against the real database, which is the
  only place `CountByStatusTag` and `ApplyFilters` are checked against each other
  at full scale. The unknown persisted tag still lands on All; the relaunch test
  still asserts the persisted `questList.statusTag` value directly in
  `user_data.db`. (`QuestNavigationE2ETests` conversions are mechanical: same
  flows, chip helpers instead of combo helpers.)
- The harness's status-filter probes assert EXCLUSIVITY — exactly one chip
  selected — because the deleted ComboBox's `SelectionPattern` guaranteed that
  structurally and per-element ItemStatus strings do not.
- **Style** (`QuestStatusChipStyleTests`): the two hover-cue properties that no
  runtime assertion can reach are pinned against the XAML source text (the
  `FontAssetsTests` approach) — the hover layer is tinted from the chip's own
  `Foreground`, and the trigger never targets an ancestor of the label.
- **Not automated**: the exact fill/border alpha values — pixel-level styling;
  verified manually.

## Verification

- `dotnet build TarkovHelper.sln` — clean: 0 warnings, 0 errors.
- `dotnet test --filter "Category!=E2E"` — 268 passed, 0 failed, 1 skipped
  (includes `PrdDocsTests` validating this document pair). The new
  `Coerce`/`NextTag`/`ChipTags` and chip-style tests were each checked fail-first
  against a mutated implementation before being kept.
- `dotnet test --filter "Category=E2E"` — 30 passed, 0 failed (on an interactive
  desktop with no competing foreground window). That run is also what closes the
  design's one recorded unknown: the chips' `ItemStatus` really does reach an
  out-of-process UIA client.
- Manual: chip row reads All → Unavailable with counts and wraps rather than
  clipping on a narrow window; selected chip renders filled in its color and
  survives hover; toggle-to-All and All-on-All no-op; restart restores the chip;
  empty-state reset lands on All; kappa gauge and filter-bar spacing intact.

## Risks & Migration

- No settings migration: the `questList.statusTag` key and every value it can
  hold keep their meaning. Rollback restores the combo without touching stored
  data.
- `AutomationProperties.ItemStatus` propagation through the WPF button peer to
  the managed UIA client was the one unproven link in the e2e design. Resolved: a
  standalone probe confirmed the write flows through
  `UIElementAutomationPeer.GetItemStatusCore` and that an out-of-process
  `AutomationElement.Current.ItemStatus` read returns the live value across a
  flip, and the full e2e suite then passed 30/30 against the real app. The
  recorded fallbacks (an explicit `ItemStatusProperty` read, or per-chip
  `HelpText`) are no longer needed but stay isolated to `UpdateStatusChips` and
  one harness method if they ever are.
- The harness's chips-initialized wait was not a proof that `_isDataLoaded` is
  set — an early pre-restore `ApplyFilters` pass could paint the chips first,
  after which a click would be silently dropped and the helper (deliberately
  without a re-invoke loop — a double invoke could toggle the filter off) would
  time out. Resolved by gating the ItemStatus publish on `_isDataLoaded`, so
  "the chip reports a status" and "a click on it is honored" are the same
  condition. The chips still repaint their counts and colors on those early
  passes.
- The status-filter selection no longer has a UIA selection pattern: the deleted
  ComboBox exposed `SelectionPattern`/`SelectionItemPattern`, and plain `Button`
  chips expose only `InvokePattern` plus the free-form ItemStatus string. Screen
  readers therefore announce "Active 42, button" without a selected state. The
  chips do keep a visual selection cue (fill + full-color border + SemiBold), the
  system focus visual (neither this style nor the app-wide Button style overrides
  `FocusVisualStyle`, so the default adorner still draws), and the `Hand` cursor.
  Accepted for now, and the deeper fix is scoped: `ButtonAutomationPeer` is public
  and unsealed, so a `StatusChipButton : Button` overriding `OnCreateAutomationPeer`
  with a peer implementing `ISelectionItemProvider` would add the pattern purely
  additively — `InvokePattern` and ItemStatus both survive, so no e2e flow changes.
  (A `RadioButton`-based chip was rejected on inspection: `RadioButtonAutomationPeer`
  does not implement `IInvokeProvider`, so it breaks `AppDriver.InvokeElement` and
  needs `StatusChip_Click` reworked into Checked/Unchecked handlers plus a
  suppression scope — the same objection that ruled out ToggleButton.) This belongs
  to a dedicated accessibility pass, not to the chip conversion.

## Deferred, with reasons

Four improvements were identified while reviewing this change and deliberately not
taken here. Recorded so the next reader does not have to re-derive them:

- **Filter state is still split** between six criteria scraped off controls in
  `ApplyFilters` and the `_statusTag` field, which is also why the filter bar has
  two "not ready yet" flags (`_isInitializing` for the controls, `_isDataLoaded`
  for the chips). No interleaving where they disagree is reachable — every
  `SuppressFilterHandlers` scope body is synchronous on the UI thread — and both
  boundaries that could admit an invalid tag are now closed (`Coerce` at the DB
  read, `BuildStatusChips` at the chip row), so a single-snapshot rewrite or a
  `QuestStatusFilter` enum would be restructuring for its own sake. The enum in
  particular would ripple through the public `QuestFilterCriteria` record, the
  persisted string and the XAML `Tag`s, and would need a second status vocabulary
  alongside `QuestStatus` (an `All` member; a `Locked` meaning Locked+LevelLocked).
- **Persistence still rides the render pass** (`ApplyFilters`' `_isDataLoaded`-gated
  `SaveFilterSettings`). Note the older deferral note in
  `feature-quest-overview-filters.spec.md` justified moving it partly as "retires
  the `_isDataLoaded` gate entirely" — that payoff is now gone, because this change
  gives the same flag two more jobs (gating the ItemStatus publish and dropping
  pre-restore clicks, deliberately the same condition). Moving the save would also
  not be mechanical: `PopulateTraderFilter`/`PopulateMapFilter` legitimately widen
  the trader/map filter during a repopulation and persist that via the render pass.
- **Status colors are declared twice** — the chip `Foreground` hexes in XAML and
  the `*Brush` statics in the code-behind. Consolidation cannot be complete
  (`ChipLocked` `#757575` is deliberately not `LockedBrush` `#666666`, `ChipAll` is
  the theme's `TextPrimaryBrush`, `LevelLockedBrush` has no chip) and it would
  contradict the "each chip's XAML `Foreground` is the single place its color is
  declared" contract that `ChipEntry`'s derivation rests on. A drift-guard unit
  test over the two declarations is the cheaper option if this ever bites.
- **`QuestListSettings`' five filter setters are dead** (`KappaOnly`,
  `ItemRequired`, `Trader`, `Map`, `StatusTag`; `SaveFilterSnapshot` is the only
  writer). Deleting them would make the "these five change together in one
  transaction" invariant mechanical rather than documented, but it diverges from
  `MapSettings`, the sibling this class cites as its pattern.
