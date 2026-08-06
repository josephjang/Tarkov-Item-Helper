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
- `MapPage`'s `CmbStatusFilter` and the harness's combo helpers
  (`WaitForComboSelection`, `SelectComboItemByName`) are untouched — the map
  tests still use them.

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
  `{ All, Active, Locked, Done, Failed, Unavailable }` (All first, display
  order); new `QuestStatusTags.IsKnown(string?)` (ordinal membership in
  `ChipTags`), the unit-testable home of the restore validation. Comment updates
  (tags are chip tags now, not ComboBoxItem tags). No predicate changes:
  `MatchesStatusTag` already returns true for "All" and
  `MatchesFaction(vm, faction, "All")` already hides other-faction quests, so
  `CountByStatusTag` handed "All" yields exactly the list-under-All count.
- `TarkovHelper/Pages/QuestListPage.xaml` — delete the `CmbStatus` block and
  `TxtStats`; statistics-bar grid goes 3 → 2 columns (chips, gauge); add
  `ChipAll` first in the chip panel (`Foreground = TextPrimaryBrush`, neutral —
  All is the absence of a status filter); the faction toggle's left margin drops
  to 0 (CmbMap's right margin now provides the 16px gap); `StatusChipStyle`'s
  hover trigger changes from a Background swap to an Opacity dim (see Technical
  Decisions).
- `TarkovHelper/Pages/QuestListPage.xaml.cs` — new `_statusTag` field seeded
  `QuestListSettings.DefaultStatusTag`; `StatusChip_Click` toggles/sets it and
  calls `ApplyFilters` directly (keeping the `_isDataLoaded` guard);
  `ApplyFilters` builds the criteria from `_statusTag`; `RestoreFilterSettings`
  seeds it via `IsKnown` (unknown → All); `ResetFilters` sets it to All;
  `CmbStatus_SelectionChanged` is deleted. `StatusChips` gains the `ChipAll`
  entry, the "Unavailable" label (was "N/A"), and per-chip derived brushes
  (frozen once at array build): a ~20%-alpha fill and a ~40%-alpha border from
  the chip's `Foreground` color. `UpdateStatusChips` applies the new visuals
  (selected: fill + full-color border + SemiBold; unselected: transparent +
  dimmed color border) and publishes
  `AutomationProperties.ItemStatus = "Selected"/"Unselected"` per chip.
- `TarkovHelper/Services/Settings/QuestListSettings.cs` — comment-only (the
  default and the key no longer describe a combo).
- `TarkovHelper.Tests/QuestListFilterTests.cs` — count tests run over the
  production `ChipTags` (now including All); new tests for the All-count
  semantics and `IsKnown`.
- `TarkovHelper.Tests/E2ETestHarness.cs` — `AppDriver.GetItemStatus`;
  `E2ETestBase.SelectStatusChip` / `WaitForSelectedStatusChip` / `StatusChipId`;
  `ShowQuestDetail` selects status via chips.
- `TarkovHelper.Tests/QuestOverviewFiltersE2ETests.cs`,
  `TarkovHelper.Tests/QuestNavigationE2ETests.cs` — combo probes/selects become
  chip waits/selects (`QuestCascadeConfirmE2ETests` is unaffected: it goes
  through `ShowQuestDetail`).

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

**Restore validation moves into `QuestStatusTags.IsKnown`.** The combo's
tag-matching fallback used to absorb unknown persisted tags as a side effect of
`SelectComboByTag`. With no combo, the validation must be explicit, and it must
widen to "All" (never the narrower "Active" default) — the existing e2e test for
an unknown persisted tag pins exactly this. A pure static helper keeps it
unit-testable without WPF.

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
existing chip/badge color contract). The selected fill (~`0x33` alpha) and
unselected border (~`0x66` alpha) are computed and frozen once when the
lazily-built `StatusChips` array is constructed — `UpdateStatusChips` runs on
every `ApplyFilters` and must not allocate brushes per pass. The All chip needs
no special-casing: the derivation is color-generic, so its neutral foreground
yields a subtle white-tint fill.

**The hover trigger dims instead of recoloring.** The style's `IsMouseOver`
trigger used to swap the background to neutral `BackgroundLightBrush`, which
would visually "unfill" a selected colored chip. It now sets the chip border's
`Opacity` — a state-agnostic hover cue that preserves whichever colors the chip
currently has.

## Test Strategy

- **Unit** (`QuestListFilterTests`): the shared tag list the count tests iterate
  becomes `QuestStatusTags.ChipTags`, so every existing count invariant (Locked
  includes LevelLocked, counts independent of the selected tag, faction
  exception, per-tag equality with `Matches`) now also covers the All tag. New:
  the All count equals the list-under-All count and is neither the sum of the
  other chips nor the raw total (explicit faction fixture); `IsKnown` accepts
  every chip tag and rejects unknown/empty/null/case-mismatched tags. The chip
  coverage test asserts All is first and every `QuestStatus` except
  `LevelLocked` has a chip.
- **E2E** (`QuestOverviewFiltersE2ETests`): chip click applies the status,
  re-click returns to All, and a direct All click works — asserted via
  ItemStatus, plus regression pins that `CmbStatus` and `TxtStats` are gone and
  that the All/Unavailable chips carry the new labels with counts. The unknown
  persisted tag still lands on All; the relaunch test still asserts the
  persisted `questList.statusTag` value directly in `user_data.db`.
  (`QuestNavigationE2ETests` conversions are mechanical: same flows, chip
  helpers instead of combo helpers.)
- **Not automated**: the hover-opacity cue and the exact fill/border alpha —
  pixel-level styling; verified manually.

## Verification

- `dotnet build TarkovHelper.sln` — clean, 0 warnings.
- `dotnet test --filter "Category!=E2E"` — unit suite (includes `PrdDocsTests`
  validating this document pair).
- `dotnet test --filter "Category=E2E"` — on an interactive desktop with no
  competing foreground window.
- Manual: chip row reads All → Unavailable with counts; selected chip renders
  filled in its color and survives hover; toggle-to-All and All-on-All no-op;
  restart restores the chip; empty-state reset lands on All; kappa gauge and
  filter-bar spacing intact.

## Risks & Migration

- No settings migration: the `questList.statusTag` key and every value it can
  hold keep their meaning. Rollback restores the combo without touching stored
  data.
- `AutomationProperties.ItemStatus` propagation through the WPF button peer to
  the managed UIA client is the one unproven link in the e2e design; if it does
  not surface, the fallback is reading
  `AutomationElement.ItemStatusProperty` explicitly, or per-chip `HelpText` —
  isolated to `UpdateStatusChips` and one harness method.
- The harness's chips-initialized wait (non-empty ItemStatus) is not a proof
  that `_isDataLoaded` is set — an early pre-restore `ApplyFilters` pass could
  in principle paint the chips first. The same-shaped race exists today with the
  combo helpers and does not bite because the tests first wait on data-dependent
  UI; the helper deliberately stays simple (no re-invoke loop — a double invoke
  could toggle the filter off).
