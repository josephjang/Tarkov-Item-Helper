# Map Tab — Restore Last Viewed Map/Zoom/Pan (Page View-State Persistence, Phase 1) PRD

## Overview

- **Status**: Review (implemented + verified; PR #8 open on josephjang/Tarkov-Item-Helper)
- **Created**: 2026-07-23
- **Updated**: 2026-07-24
- **Owner**: josephjang
- **Translations**: Korean companion at `feature-persist-map-view-state.ko.md` (kept in sync 1:1)

## Problem Statement

### User-visible symptom

While using the Map tab on any map (e.g. Customs), briefly switching to another tab (e.g.
Quests) and returning resets the Map tab to **Woods** at 100% zoom, centered, on the default
floor, with the movement trail cleared. The same reset happens on every app launch. The user
has to re-select their map and re-frame their view after every tab switch.

### Root cause (confirmed by code reading)

`MainWindow` caches page instances and swaps them into a `ContentControl`
(`MainWindow.xaml.cs`, `Tab_Checked`), so `MapPage` is **not** re-constructed on tab switches
— but WPF fires `Loaded`/`Unloaded` on every content swap, and MapPage's handlers undo the
cached state:

1. `MapTrackerPage_Loaded` (`TarkovHelper/Pages/Map/MapPage.xaml.cs`) re-runs **full
   initialization on every tab entry**: clears the trail, re-creates all marker managers,
   and calls `PopulateMapComboBox()`.
2. `PopulateMapComboBox()` unconditionally sets `SelectedIndex = 0` — the first key in
   `Assets/DB/Data/map_configs.json`, which is **Woods**. There is no literal "Woods"
   default in code; it is dictionary position 0.
3. `RestoreMapState()`, which is supposed to reapply the saved
   `SettingsService.MapLastSelectedMap`, runs **after** step 2 and is guarded by
   `string.IsNullOrEmpty(_currentMapKey)` — always false by then. The restore path is
   **dead code** in practice; even when it would run, it forces zoom to 100% and recenters
   instead of restoring the saved zoom/pan.
4. The save side works: `MapTrackerPage_Unloaded` → `SaveMapState()` persists
   `MapLastSelectedMap`/`MapLastZoomLevel`/`MapLastTranslateX/Y`. So after the reset shows
   Woods, the next tab-away **saves "Woods", clobbering the user's real last map**.

Two latent bugs ride on the same structure:

- `Unloaded` unsubscribes `MapMarkerDbService.DataRefreshed` and
  `QuestObjectiveDbService.DataRefreshed` (subscribed once in the constructor) and `Loaded`
  never re-subscribes — after the first tab-away, DB refreshes stop updating map markers
  for the life of the app.
- `StartRaidEventMonitoring()` calls `EftRaidEventService.StartMonitoring(...)` on every
  tab entry, tearing down and recreating the app-wide `FileSystemWatcher`s that
  `MainWindow.AutoStartLogMonitoring` already started.

### Generalized problem

This is one instance of a class: **cached pages whose `Loaded` handlers destroy state the
cache was supposed to preserve.** Survey of the five main pages:

| Page | State a user expects to survive a tab switch | Today |
|------|----------------------------------------------|-------|
| MapPage | Selected map, zoom, pan, floor, trail, drawer/panel state | **Lost** — saved but never restored; saved value clobbered |
| QuestListPage | Search text, filters, selected quest | Kept (`_isDataLoaded` guard — the model to follow) |
| ItemsPage | Search, filters, sort, selected item | Kept (guard + explicit selection restore) |
| HideoutPage | Search text; selected module | Search kept; **selection lost** (ItemsSource rebuild without reselect) |
| CollectorPage | Search, toggles, sort; selected item | Filters kept; **list highlight lost** on rebuild |
| (global) | Last selected main tab | Not persisted — every launch opens Quests |

MapPage is the only page that *saves* state and then fails to restore it; Hideout/Collector
lose list selection; last-tab persistence doesn't exist. Quest/Items show the working
pattern already in this codebase.

## Improvement Principles (generalized)

These are the rules this PRD establishes; Phase 1 applies them to MapPage, later phases to
the rest. Any future page added to the tab cache must follow them too.

1. **Cached pages must have idempotent `Loaded` handlers.** One-time initialization runs
   once behind an `_isInitialized` guard; per-visit work is limited to event
   re-subscription, watcher start/stop, and reconciling signals missed while unloaded.
   Tab re-entry restores nothing — it simply stops destroying what is already there.
2. **Restore precedes default.** A hardcoded default (combo index 0, `isDefault` floor)
   may only ever be the *fallback of a restore decision*, never a step executed before it.
3. **Saved state yields to live game signals.** Precedence: active raid's map > saved map >
   config default.
4. **Decision logic lives in a pure, unit-tested core** (no UI/DB/service dependencies) —
   the `WindowBoundsPersistence` precedent from the window-bounds feature.
5. **Never rely on `Unloaded` alone for persistence.** WPF does not guarantee `Unloaded`
   at application shutdown — save on change, or additionally hook window `Closing`.

## Goals (Phase 1 — Map tab)

- [x] Goal 1: Returning to the Map tab shows the **same map, zoom, and pan** as when the
      user left it.
- [x] Goal 2: Relaunching the app restores the **last viewed map, zoom, and pan** (the
      save infrastructure already exists; restore is the missing half).
- [x] Goal 3: **Raid detection wins**: if a raid is live (detected via
      `EftRaidEventService`), the detected map takes precedence over the saved map — both
      at first load and when a raid started while the user was on another tab.
      *(unit-tested; manual verification on the Release build completed 2026-07-24)*
- [x] Goal 4: **First run** (no saved value) keeps today's behavior: first configured map,
      100% zoom, centered.
- [x] Goal 5: Structural side effects of the idempotent-`Loaded` fix: floor, trail,
      drawer state, and marker-manager identity survive tab switches; the `DataRefreshed`
      dead-subscription bug and the per-entry `FileSystemWatcher` churn are fixed.

## Non-Goals (Scope Out)

- Floor persistence across restarts — auto floor detection plus the per-map default floor
  is the correct behavior on a fresh map load.
- Trail persistence across restarts — the trail is per-raid data; raid-event clearing stays.
- Re-showing the overlay minimap on tab re-entry — current hide-on-leave behavior is kept
  (separate UX decision).
- Per-map zoom/pan memory outside raid auto-switch — only the single global last-view is
  stored, as today.
- Geometry-aware pan clamping (saved pan validated only for finiteness in Phase 1; a clamp
  against map/viewer geometry is future hardening).

**Later phases (explicitly out of this PRD's implementation scope):**

- **Phase 2**: HideoutPage/CollectorPage selection restore across `ItemsSource` rebuilds
  (in-memory only; ItemsPage's save-selection-then-reselect pattern is the template).
- **Phase 3**: persist the last selected main tab (`app.lastSelectedTab`); mind the
  startup-cost interaction (restoring the Map tab constructs MapPage at launch).

## Requirements / Acceptance Criteria

- [x] R1 (tab round-trip): Select map M, zoom/pan → switch tab → return: map M with the
      same zoom/pan; floor, trail, and drawer state intact. *(map selection e2e-verified;
      zoom/pan/floor/trail survive structurally — no re-init on re-entry)*
- [x] R2 (restart round-trip): Select map M, zoom/pan → close app → relaunch → Map tab:
      map M restored with saved zoom/pan. *(e2e: seeded map + view restored and re-saved
      unchanged)*
- [x] R3 (raid precedence): With a live raid on map R, first Map-tab load shows R (not the
      saved map); a raid starting while on another tab switches the map on return.
      *(unit-tested via `DecideInitialMap`/`GetActiveRaidMapKey`; not e2e-drivable)*
- [x] R4 (first run): No saved value → first configured map, 100% zoom, centered. *(e2e)*
- [x] R5 (resilience): A saved map key no longer in `map_configs.json`, or non-finite
      zoom/pan values, fall back to defaults — never a crash or a blank map.
      *(unit-tested fallback rules)*
- [x] R6 (no clobber): Tab switching alone never overwrites the saved map with a
      reset value. *(e2e: pre-fix these tests fail — Woods shown, null/reset persisted)*

## Technical Decisions

| Decision | Rationale | Date |
|----------|-----------|------|
| Fix by making `Loaded` idempotent (`_isInitialized` guard: one-time init vs per-visit re-arm), rather than only teaching `PopulateMapComboBox` the saved key or reordering `RestoreMapState` | Fixes map+zoom+pan+floor+trail+drawer+marker-manager identity in one move; avoids re-parsing the SVG and reloading objectives/extracts/markers on every tab entry; matches the `_isDataLoaded` pattern already proven in QuestListPage/ItemsPage; structurally fixes the `DataRefreshed` dead-subscription bug | 2026-07-23 |
| New pure static core `TarkovHelper/Services/Map/MapViewStatePersistence.cs`: `DecideInitialMap(savedKey, availableKeys, activeRaidKey)` → `(MapKey, Source)` with precedence raid > saved > first; `ValidateView(zoom, tx, ty, minZoom, maxZoom)` → validated view or null; `GetActiveRaidMapKey(raid)` | Mirrors `WindowBoundsPersistence` (pure core + thin UI wiring); case-insensitive key matching returning the canonical config key; invalid-key fallback built in; trivially unit-testable | 2026-07-23 |
| Save the map key immediately on `SelectionChanged`; save zoom/pan on `Unloaded` **and** from `MainWindow.OnWindowClosing` (`PersistViewState()` backstop) | Map key survives process kills; WPF does not guarantee `Unloaded` at shutdown; `MapSettings` setters have change detection so double-saves are cheap no-ops | 2026-07-23 |
| Raid "live" check: `EftRaidEventService.CurrentRaid != null && State != Ended && MapKey` non-empty; on tab re-entry run `ReconcileActiveRaid()` (no-op when the live raid's map already matches — preserves the trail) | No `EftRaidEventService` changes needed; restore never fights raid auto-detection; re-entry mid-raid keeps the trail | 2026-07-23 |
| Delete `RestoreMapState()`; its intent moves into the decision core + a `_pendingViewRestore` applied in `SelectionChanged` (`LoadMapImage(key, centerView: false)` → `SetZoom(saved)` → translate last) | The method is dead code today; `centerView: false` prevents the deferred `CenterMapInView` from overwriting the restored pan | 2026-07-23 |
| Guard `StartRaidEventMonitoring` with `EftRaidEventService.IsMonitoring` | Stops tearing down/recreating the app-wide `FileSystemWatcher`s on every tab entry | 2026-07-23 |
| Programmatic map switches (raid auto-switch, screenshot follow) count as the "last viewed map" for restore — owner confirmed Option 1 of the deep-review question | Matches this PRD's declared last-viewed goal and stays consistent with live-raid precedence at launch; the alternative (persist only user-chosen maps via a suppression flag) is documented in PR #9 should the semantic ever change | 2026-07-24 |
| Accept `CenterMapInView`'s uncancellable deferred self-recenter as-is (no generation-token hardening) — owner confirmed Option B of the deep-review question | The restore and floor-change paths already bypass it via `centerView: false`; the residual clobber needs raid-start-during-first-load + auto-center + a position update in the same gap; a token would change observable timing across every centering caller with no timing-test coverage. Known limitation documented at the reschedule site | 2026-07-24 |

## Implementation Plan

### Phase 1: Map view-state restore (this PRD)

- [x] Task 1.1: Add pure decision core
  - Files: `TarkovHelper/Services/Map/MapViewStatePersistence.cs` (new)
- [x] Task 1.2: Make `MapTrackerPage_Loaded` idempotent — split one-time init (settings,
      marker managers, map decision + `PopulateMapComboBox(selectKey)`, data loads, drawer,
      overlay) from per-visit re-arm (progress/keyboard/raid event re-subscription,
      `StartAutoTracking`, `ReconcileActiveRaid`); parameterize `PopulateMapComboBox` so
      index 0 is only the fallback; consume `_pendingViewRestore` in
      `CmbMapSelect_SelectionChanged` and save the map key there; delete
      `RestoreMapState()`; drop the two `DataRefreshed -=` lines from `Unloaded`; guard
      `StartMonitoring` with `IsMonitoring`; extract `SwitchToRaidMap(...)` from
      `HandleRaidStarted` and add `ReconcileActiveRaid()`
  - Files: `TarkovHelper/Pages/Map/MapPage.xaml.cs`
- [x] Task 1.3: Shutdown backstop — persist map view state from window close
  - Files: `TarkovHelper/MainWindow.xaml.cs` (`OnWindowClosing`: `_mapTrackerPage?.PersistViewState();`)

### Phase 1 tests

- [x] Task 1.4: Unit tests for the decision core (~10): saved-key happy path; case-mismatch
      returns canonical key; saved key missing from configs → first-map fallback; first run
      → first map; live raid beats saved; unknown raid key ignored; empty key list → null;
      `GetActiveRaidMapKey` per raid state (null/Ended/Matching/InRaid/empty MapKey);
      `ValidateView` round-trip, zoom clamping at both ends, NaN/Infinity rejection
  - Files: `TarkovHelper.Tests/MapViewStatePersistenceTests.cs` (new)
- [x] Task 1.5: E2E tests — extract the shared app driver (`App`/`Win32`/`E2EFact`) from
      `MainWindowBoundsE2ETests.cs` into a reusable harness; drive tabs and read the map
      combo via UI Automation (WPF exposes `x:Name` — `TabMap`/`TabQuests`/`CmbMapSelect` —
      as AutomationId). Cases: seeded map restored on launch **and not clobbered** on
      close; map survives Map → Quests → Map; first run shows Woods and saves it.
      Honest gaps: zoom/pan restore is not UIA-assertable (unit tests + manual check);
      raid precedence is unit-tested only (driving a fake EFT log through the watcher is
      too fragile for CI). If UIA proves flaky under the DPI-unaware test host, degrade to
      DB-only assertions (seed → tab round-trip → close → value unchanged), which still
      pins the R6 clobber symptom.
  - Files: `TarkovHelper.Tests/MapStateE2ETests.cs` (new), `TarkovHelper.Tests/MainWindowBoundsE2ETests.cs` (harness extraction)

## Progress Log

| Date | Update | By |
|------|--------|-----|
| 2026-07-23 | PRD created from root-cause analysis: cached MapPage's `Loaded` re-runs full init each tab entry; `PopulateMapComboBox` forces index 0 (Woods) before `RestoreMapState`, whose `IsNullOrEmpty(_currentMapKey)` guard then always fails (dead code); the reset value is then saved, clobbering the real last map. Generalized into the five improvement principles; design decided (idempotent `Loaded`, pure `MapViewStatePersistence` core, raid > saved > default precedence, save-on-change + `Closing` backstop). | josephjang |
| 2026-07-24 | Phase 1 implemented per Tasks 1.1–1.3 (pure core; idempotent `Loaded` with per-visit re-arm + `ReconcileActiveRaid`; `SwitchToRaidMap` extracted; save-on-change in `SelectionChanged`; `OnWindowClosing` backstop; `RestoreMapState` deleted; `DataRefreshed` unsubscribe and watcher churn removed). Tests per Tasks 1.4–1.5: 28 unit tests + 3 e2e (UIA tab driving worked; the shared harness was extracted from the bounds tests). E2e validated against the pre-fix app: all 3 fail there — including the discovery that pre-fix, closing the app while on the Map tab persisted **nothing** (`Unloaded` doesn't fire on window close), so the `Closing` backstop fixes a second latent loss, not just a theoretical one. Harness hardening: loading UI Automation flips the test host DPI-aware mid-run, which broke the bounds e2e coordinate assumptions on a 200% display — the host's DPI awareness is now pinned per-monitor-v2 up front and `GetWindowRect` normalizes physical px to WPF units. Full suite 71 passed / 1 skipped (pre-existing skip). Also fixed a pre-existing CS1998 in `MainWindow.BtnClearAllData_Click`. | josephjang |
| 2026-07-24 | Manual verification on the Release build completed by the owner — all completion criteria met. Remaining step: open the PR. | josephjang |
| 2026-07-24 | Deep review of PR #8: 26 verified findings fixed in stacked PR #9 (re-entry catch-up for markers/drawer, unified raid paths with raid-identity reconciliation, invariant-culture settings round-trip, init retry latch, monitoring gates + log-folder re-point, atomic view-state save, e2e serialization/hardening; 76 unit + 9 e2e green). Owner resolved all three open review questions: Option 1 on semantics (programmatic raid/screenshot switches count as the last viewed map — behavior kept as implemented); `ValidateView` → `NormalizeSavedView` rename approved and applied; Option B on `CenterMapInView` (deferred self-recenter accepted as-is, known limitation documented at the site — no generation-token hardening). Nothing from the review remains open. | josephjang |

## Completion Criteria

- [x] All Goals and Requirements (R1–R6) met
- [x] Build green (`dotnet build`)
- [x] Unit guard: `MapViewStatePersistenceTests` protect the decision/validation rules
      (28 tests)
- [x] E2E: `MapStateE2ETests` verify restore-on-launch, tab-switch survival, and
      no-clobber against the real app in an isolated Config dir
      (filter out of quick runs with `dotnet test --filter Category!=E2E`);
      validated to fail against the pre-fix app
- [x] Manual checks: completed by the owner on the Release build (2026-07-24) — the
      raid auto-switch check was the only item not covered by automation; tab/restart
      round-trips, first-run defaults, and invalid-value fallbacks are e2e/unit-covered
      above

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Idempotent `Loaded` skips a re-init some data relied on → stale content on re-entry | Medium | Event subscriptions (progress, DB refresh, language) already push updates while loaded or re-arm on entry; `ReconcileActiveRaid()` covers raid signals missed while away; manual regression pass over map markers/objectives after tab switches |
| UIA automation flaky under the DPI-unaware test host | Low | Degraded mode documented in Task 1.5: DB-only assertions still pin the R6 clobber symptom |
| Saved pan was computed against a different window size → off-center view after restore | Low | Window bounds are also persisted (window-bounds feature), so geometry is coherent in the common case; Reset View recovers; geometry-aware clamping listed as future hardening |
| Hard process kill skips the close-time zoom/pan save | Low | Accepted — map key is saved on change; at worst one session's zoom/pan is lost (same tradeoff as window bounds) |

---

## Archive Info (fill on completion)

- **Completed**: YYYY-MM-DD
- **Summary**:
- **Actual vs Planned**:
- **Lessons Learned**:
- **Follow-up Items**:
