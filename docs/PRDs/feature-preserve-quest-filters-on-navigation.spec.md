# Preserve Quest Filters on Navigation — Technical Spec

- **Created**: 2026-08-01

> The sibling `feature-preserve-quest-filters-on-navigation.md` holds the product
> decision. Written on the work's branch and merged in the same PR as the work.
> Nothing is kept current: fields are written once, discoveries are appended. A
> later change that reverses a decision here appends `Superseded by <doc>` below
> this line, in the PR that reverses it.

## Summary

`QuestListPage.SelectQuestInternal` stops resetting filters. It rests on two ideas:
the detail panel can already display a quest independently of the list
(`UpdateDetailPanel` takes an override view model, tracked in `_currentDetailTask`),
and the list never needs rebuilding on navigation because it already reflects the
current filters. Navigation becomes: select-and-scroll when the target passes the
current filters, otherwise show the details with the list deselected and a notice
offering an explicit "show in list" action that performs today's reset.

## Non-Goals

- No navigation/back stack, and no change to how the detail panel renders quest
  content — only to how navigation interacts with filters and selection.
- No change to `MapPage` / `MapQuestMarkerManager` quest markers: they raise
  `ObjectiveSelected` inside the map page and never enter this path.
- No change to the `_pendingQuestSelection` deferral in `SelectQuest` (navigation
  before data load); it continues to replay into `SelectQuestInternal`.

## Current Behavior / Root Cause

All four quest-navigation entry points converge on
`QuestListPage.SelectQuestInternal`:

- `PrerequisiteQuest_Click` (prerequisite links in the detail panel)
- `RecommendationsPanel_RecommendationClicked` (via
  `QuestRecommendationsPanel.RecommendationClicked`)
- `ItemsPage.QuestName_Click` and `CollectorPage.QuestName_Click`, both through
  `MainWindow.NavigateToQuest` → `QuestListPage.SelectQuest`

`SelectQuestInternal` starts with `ResetFiltersForNavigation()`, which sets
`CmbStatus` to *All*, clears `TxtSearch`, unchecks `ChkKappaOnly` and
`ChkItemRequired`, and resets `CmbTrader` and `CmbMap` — under an `_isInitializing`
guard so the per-control handlers don't each call `ApplyFilters`. It then runs
`ApplyFilters()` to rebuild the list (necessary only because the reset changed the
filters), and finally selects the target with `LstQuests.SelectionChanged` detached
and forces `UpdateDetailPanel(questVm)`.

The reset exists solely to guarantee the target appears in the left list; the
`UpdateDetailPanel(overrideVm)` mechanism it already uses proves the detail display
does not need it. Two side effects of the current shape: the `questVm == null`
early return sits *after* the reset, so navigating to an unknown quest wipes
filters and does nothing else; and because `ApplyFilters` replaces
`LstQuests.ItemsSource`, a plain filter change by the user clears the selection and
collapses the detail panel even when the selected quest is still in the list.

## Design

All changes are in `TarkovHelper` (no other project changes):

- `Pages/QuestListPage.xaml.cs` — navigation, filter, and notice logic
- `Pages/QuestListPage.xaml` — notice UI in the detail panel
- `Pages/QuestListFilter.cs` (new) — extracted filter predicate
- `Services/LocalizationService.Quest.cs` — notice strings (EN/KO/JA)
- `TarkovHelper.Tests/QuestListFilterTests.cs` (new) — unit guards
- `TarkovHelper.Tests/QuestNavigationE2ETests.cs` (new) — end-to-end coverage

**`SelectQuestInternal` reshape.** Resolve `questVm` first and return early if the
quest is unknown (nothing mutated). Do not reset filters and do not call
`ApplyFilters` — the list already reflects the current filters. Then branch on
whether `LstQuests.ItemsSource` (the filtered `List<QuestViewModel>`; same
instances as `_allQuestViewModels`, so reference `Contains` suffices) holds the
target:

- *Visible*: hide the notice, set `LstQuests.SelectedItem` and `ScrollIntoView` —
  `LstQuests_SelectionChanged` updates the panel as for a normal click. The
  detach/`Dispatcher.BeginInvoke` choreography existed to survive the
  `ItemsSource` swap and is no longer needed on this path.
- *Hidden*: clear `LstQuests.SelectedItem` with `SelectionChanged` detached (so the
  panel isn't collapsed by the null selection), call `UpdateDetailPanel(questVm)`,
  and show the notice.

`ResetFiltersForNavigation` is kept but is no longer called from any navigation
path — only from the notice's button (below).

**Notice UI.** A `Border` (`PnlFilteredOutNotice`) at the top of `DetailPanel` in
`QuestListPage.xaml`, collapsed by default: a `TextBlock` (`TxtFilteredOutNotice`)
with the localized "hidden by current filters" text and a `Button`
(`BtnShowInList`). `BtnShowInList_Click` runs `ResetFiltersForNavigation()`,
`ApplyFilters()`, then selects and scrolls to `_currentDetailTask`'s view model and
hides the notice — exactly the old navigation behavior, now user-invoked. Strings
follow the existing `CurrentLanguage switch` pattern in
`LocalizationService.Quest.cs` (e.g. `QuestHiddenByFilters`, `ShowInList`) and are
applied wherever the page refreshes localized UI text.

**Filter-change reconciliation (R6).** `ApplyFilters` replaces
`LstQuests.ItemsSource` with `SelectionChanged` detached, then reconciles against
`_currentDetailTask`: if its view model is in the new filtered list, restore
`SelectedItem` to it and hide the notice; if not, leave the detail panel showing it
and show the notice (the panel no longer collapses on a filter change). A real user
click on a list row still flows through `LstQuests_SelectionChanged`, which updates
`_currentDetailTask` and hides the notice.

**Filter predicate extraction.** The inline `Where` lambda in `ApplyFilters` moves
to a pure static `QuestListFilter.Matches(QuestViewModel vm, QuestFilterCriteria
criteria)` in `Pages/QuestListFilter.cs`, where `QuestFilterCriteria` is a record
of the seven filter inputs (search text, Kappa-only, item-required, trader, map,
status tag, faction). `ApplyFilters` builds the criteria from the controls and
filters through it — no behavior change, but the predicate (status mapping, faction
exception, multi-language search) becomes unit-testable without WPF.

## Technical Decisions

**Membership check against `ItemsSource` instead of re-evaluating the predicate.**
The filtered list is already the source of truth for "visible under current
filters", and the view-model instances are shared with `_allQuestViewModels`, so a
reference `Contains` cannot drift from what the user sees. Re-running the predicate
for one item would duplicate the decision `ApplyFilters` already made.

**`ApplyFilters` swaps `ItemsSource` with `SelectionChanged` detached.** Today the
swap fires `SelectionChanged` with a null selection, which is why navigation needed
its own detach choreography and why filter changes collapse the detail panel.
Routing all `ItemsSource` swaps around the handler makes `SelectionChanged` mean
exactly "the user picked a row", and the reconciliation step decides panel state
explicitly. The alternative — keeping the event hot and special-casing null
selections inside the handler — spreads the same logic across two places.

**Restore selection after filter changes.** Reconciliation re-selecting
`_currentDetailTask` when it survives the new filters is a deliberate behavior
improvement beyond the PRD's minimum (today the selection is dropped and the panel
collapses on every filter change). It falls out of the same reconciliation needed
for R6, so implementing R6 without it would take extra code to preserve a worse
behavior.

**Predicate extraction over UI-level unit tests.** Driving `QuestListPage` in unit
tests would require a WPF dispatcher and page construction; extracting the pure
predicate gets the filter semantics under test cheaply, and the E2E tests cover the
wiring. The extraction is behavior-preserving by construction (same expression,
inputs captured into a record).

## Test Strategy

- **Unit** (`QuestListFilterTests`): lock down the predicate invariants that the
  navigation change now leans on — the *Locked* tag matches both
  `QuestStatus.Locked` and `QuestStatus.LevelLocked`; *All* passes every status;
  faction-restricted quests are hidden except under the *Unavailable* tag; search
  matches `Name`/`NameKo`/`NameJa` case-insensitively; empty criteria pass
  everything; each single-criterion rejection (trader, map, Kappa, item-required).
- **E2E** (`QuestNavigationE2ETests`, `Category=E2E`, driven through `AppDriver`
  UI Automation; controls addressed by `x:Name` as AutomationId): with the status
  filter on *Active* plus a trader filter and search text set, (1) clicking a
  prerequisite link of an active quest shows the prerequisite's details, leaves
  every filter control unchanged, and shows the notice with no list row selected;
  (2) `BtnShowInList` resets the filters and selects the quest in the list;
  (3) navigation from the Items page and (4) from the Collector page switches tabs
  and preserves the Quests-tab filters; (5) clicking a recommendation preserves
  filters; (6) changing the status filter so the shown quest becomes visible
  selects it in the list and hides the notice.
- The `_pendingQuestSelection` replay (navigation before data load) is exercised
  implicitly by the cross-tab E2E launched fresh; it has no new logic of its own.

## Verification

- `dotnet build TarkovHelper.sln`
- `dotnet test --filter Category!=E2E` (unit + doc guards), then
  `dotnet test --filter Category=E2E` on an interactive desktop
- Manual: run via `dotnet TarkovHelper.dll` (bypasses the requireAdministrator
  manifest), set status *Active* + a search term, click a Done prerequisite —
  observable result: filters and search box unchanged, prerequisite details shown,
  notice visible; the notice button reproduces the old reset-and-highlight
  behavior.

## Risks & Migration

No data or settings migration — the change is confined to in-page interaction
state. Rollback is a straight revert; no persisted format changes. The main
regression surface is selection/panel state after `ItemsSource` swaps (the detach
choreography moves from navigation into `ApplyFilters`), which is exactly what the
E2E set pins down.
