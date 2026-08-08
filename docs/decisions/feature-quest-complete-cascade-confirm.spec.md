# Quest Complete Cascade Confirmation — Technical Spec

- **Created**: 2026-08-03

> The sibling `feature-quest-complete-cascade-confirm.md` holds the product
> decision. Written on the work's branch and merged in the same PR as the work.
> Nothing is kept current: fields are written once, discoveries are appended. A
> later change that reverses a decision here appends `Superseded by <doc>` below
> this line, in the PR that reverses it.

## Summary

`QuestProgressService` gains a side-effect-free cascade preview,
`GetCompletionCascade`, and `CompleteQuest` is refactored so both run the same
traversal: one pure static core (`ComputeCompletionCascade`) computes the plan,
`CompleteQuest` applies it, the preview only reads it — preview/apply drift is
impossible by construction. `QuestListPage`'s two completion handlers route
through the preview: an empty cascade completes directly (unchanged one-click),
a non-empty one shows the new modal `QuestCompleteConfirmDialog` and completes
only on Confirm.

## Non-Goals

- `MainWindow.OnQuestEventDetected` (log-sync quest events) keeps calling
  `CompleteQuest`/`CompleteQuestsBatch` directly — not gated.
- No changes to `FailQuest`, `ResetQuest`, or `CompleteQuestsBatch` semantics.
- No undo mechanism.

## Current Behavior / Root Cause

`QuestListPage.CompleteButton_Click` (row "Done") and `BtnComplete_Click`
(detail-panel "Mark Complete") call
`QuestProgressService.CompleteQuest(task, completePrerequisites: true)` with no
confirmation. `CompleteQuest` does two things:

- **Prerequisite cascade** via the recursive
  `CompleteQuestInternalOptimized`: a `visited` set (key = first `Ids` entry,
  else `NormalizedName`; OrdinalIgnoreCase) prevents cycles; a node whose
  recorded progress is already Done — checked directly against
  `_questProgress` under both the task key *and* `NormalizedName` — is
  skipped; prerequisites resolve through `TaskRequirements` (`GetTaskById`,
  falling back to `GetTask` by name per requirement), or through the legacy
  `Previous` name list only when `TaskRequirements == null`; the gate before
  recursing is `GetStatus(prev) != Done`; a prerequisite that itself has
  `AlternativeQuests` is skipped entirely, subtree included (the user must
  choose which alternative to complete).
- **Alternative auto-fail**: after the traversal, every entry of
  `task.AlternativeQuests` that resolves (`GetTask`, then `GetTaskById`) and
  whose `GetStatus` is neither Done nor Failed is marked Failed.

All changes are collected into one list, saved as a single batch
(`SaveProgressBatchAsync`), and announced with a single `ProgressChanged`.
Crucially, the traversal mutates `_questProgress` *as it goes* (each node is
marked Done when its subtree finishes), so later gates observe earlier writes —
a faithful preview must reproduce that.

## Design

Files (app):

- `Services/QuestProgressService.cs` — traversal extracted into the pure static
  `ComputeCompletionCascade`; `CompleteQuest` becomes compute-then-apply; new
  public `GetCompletionCascade(TarkovTask)`.
- `Models/QuestCompletionCascade.cs` (new) — preview result:
  `PrerequisitesToComplete` (post-order, excludes the clicked quest),
  `AlternativesToFail`, `IsEmpty`.
- `Windows/QuestCompleteConfirmDialog.xaml` + `.xaml.cs` (new) — modal preview
  dialog.
- `Services/LocalizationService.Quest.cs` — new EN/KO/JA dialog strings.
- `Pages/QuestListPage.xaml.cs` — `CompleteButton_Click` and
  `BtnComplete_Click` route through a shared `CompleteQuestWithConfirmation`.

Files (tests):

- `TarkovHelper.Tests/QuestCompletionCascadeTests.cs` (new) — unit guards for
  the static core and the localization keys.
- `TarkovHelper.Tests/QuestCascadeConfirmE2ETests.cs` (new) — dialog e2e.
- `TarkovHelper.Tests/E2EQuestData.cs` (new) — the asset-db test-data queries,
  shared with `QuestNavigationE2ETests` (moved there and extended).
- `TarkovHelper.Tests/E2ETestHarness.cs` — owned-window (dialog) helpers.

**Shared traversal core.** `ComputeCompletionCascade` is `internal static` on
`QuestProgressService` (visible to the test project via the
`InternalsVisibleTo` declaration in `TarkovHelper.csproj`, callable without the
singleton). Inputs: the task, the
`completePrerequisites` flag, and four delegates — `taskById`, `taskByName`,
`getStatus` (derived status, i.e. `GetStatus`), and `recordedStatus` (raw
`_questProgress` lookup by key). Output: the ordered list of quests the
traversal would newly mark Done (post-order; the clicked quest last, when it is
not already Done) and the list of alternatives it would mark Failed, each with
the raw `AlternativeQuests` entry it was resolved from (the apply step needs
that string as the fallback progress key, mirroring the old code).

**Compute-then-apply.** `CompleteQuest` calls the core with instance delegates,
then applies: each planned quest is written Done under `firstId ?? NormalizedName`
and appended to the change list; each planned alternative is written Failed
under `firstId ?? listedName`; then the existing single batch save and single
`ProgressChanged`, only when anything changed. `GetCompletionCascade` calls the
same core with `completePrerequisites: true` (matching both UI call sites) and
projects the result into `QuestCompletionCascade`, writing nothing.

**Dialog.** `QuestCompleteConfirmDialog` follows the `SyncResultDialog` /
`InProgressQuestInputDialog` conventions: `WindowStyle="None"`, transparent
background over a `BackgroundDarkBrush` rounded border, `DynamicResource`
brushes/font sizes throughout, `WindowStartupLocation="CenterOwner"`,
`SizeToContent`. Content: a title, the clicked quest's localized name, a
"will also be completed (N)" section, and a "will be FAILED (N)" section
carried in the app's `#FF5722` mutually-exclusive accent (same hue the detail
panel's "Other Choices" section uses). Each row shows the localized quest name
(`LocalizationService.GetQuestName`) and trader. A section with no entries is
collapsed. Buttons: Cancel and Confirm (accent-colored), plus the standard
close "X"; only Confirm sets `Confirmed`. Probe-relevant controls carry
`x:Name` (WPF surfaces it as the UIA AutomationId): `BtnCascadeConfirm`,
`BtnCascadeCancel`, `BtnCascadeClose`, `TxtCascadeTitle`, `TxtCascadeQuest`,
`TxtCascadeCompletedHeader`, `TxtCascadeFailedHeader`, `CascadeCompletedList`,
`CascadeFailedList`.

**Wiring.** `QuestListPage.CompleteQuestWithConfirmation(TarkovTask)`: compute
the cascade; if `IsEmpty`, call `CompleteQuest(task, true)` directly; otherwise
show the dialog with `Owner = Window.GetWindow(this)` and call `CompleteQuest`
only when `Confirmed`. Both button handlers delegate to it; nothing else about
the handlers changes.

## Technical Decisions

**One traversal, two callers — instead of a read-only twin or a sandboxed dry
run.** A separate preview implementation of the same rules would drift on the
next rule change (this traversal has subtle rules already: dual-key done
checks, alternative-prereq skipping, `Previous` fallback). Running the real
`CompleteQuest` against a cloned progress map to diff it would need service
state cloning plus suppressing saves and events — invasive for a preview.
Extracting the decision logic into a pure core that `CompleteQuest` itself
executes makes divergence structurally impossible.

**A planned-keys set reproduces the interleaved-mutation semantics.** The old
code wrote `Done` into `_questProgress` mid-traversal, and later gates read
those writes. The core keeps a `planned` key set consulted *before* the
recorded status in every gate: the node-entry done-check, the
`GetStatus(prev) != Done` recursion gate, and the alternatives' Done/Failed
filter. The last one is observable: when a target's alternative also sits in
its own prerequisite chain (asymmetric data), the traversal completes it and
the alternative pass must then see it as Done and not fail it — the preview
must agree.

**Delegates, not the service instance.** The core takes lookups rather than
touching singleton state, because the node-entry check reads `_questProgress`
directly under both the task key and `NormalizedName` — subtly different from
`GetStatus`, which consults the name only when the task has no Id — so the
core needs both a `recordedStatus` and a `getStatus` delegate to mirror
`CompleteQuest` exactly. Unit tests build all four delegates from plain
dictionaries; no singleton, no WPF, no DB.

**Dialog strings live in `LocalizationService.Quest.cs`, not inline.** The
older `SyncResultDialog` localizes with inline `CurrentLanguage switch`
expressions in code-behind; the newer quest strings (`QuestHiddenByFilters`,
`ShowInList`) are service properties. New strings follow the newer pattern —
one property per string, `switch` per property — which also lets the
completeness unit test enumerate them by name.

**The dialog reports through `Confirmed`, not `DialogResult`.** Matches
`SyncResultDialog`'s result-property convention; every close path (Cancel, the
X button, Alt+F4) leaves `Confirmed == false`, so "closing changes nothing"
needs no extra handling.

**E2E addresses the dialog as a top-level window by title.** The harness's
element search is rooted at the main window's UIA element, and an owned WPF
window is not reliably a UIA descendant of its owner. `AppDriver` gains
owned-window helpers (`Win32.FindTopLevelWindow(processId, title)` +
`AutomationElement.FromHandle`, plus scoped descendant search). The dialog is
opened via UIA `InvokePattern`, which is safe against the modal message pump —
WPF's `ButtonAutomationPeer` raises the click asynchronously
(`Dispatcher.BeginInvoke`), so the invoke returns before the handler enters
`ShowDialog`. A real-mouse click was tried first out of caution about a
blocking invoke and proved flaky (an unacknowledged physical click can miss on
a layout shift or a denied `SetForegroundWindow`); the invoke is deterministic.

**Shared asset-db queries move to `E2EQuestData`.** The cascade e2e needs the
same "Locked quest with an Active prerequisite" query as
`QuestNavigationE2ETests`, with extra exclusions; duplicating a 30-line SQL
constraint block across two files is exactly the drift the navigation tests
avoided by deriving data from the DB. The shared query additionally requires
that neither the quest nor the prerequisite appears in `OptionalQuests` (as
`QuestId` or `AlternativeQuestId`): the prerequisite exclusion keeps the
navigation regression test (`Detail_buttons_act_on_the_shown_quest_...`)
dialog-free — it invokes `BtnComplete` on the prerequisite and expects
immediate completion, which now requires the prerequisite's own cascade to be
empty — and the quest exclusion gives the cascade tests an exact expected
preview (one completion, zero failures).

## Test Strategy

- **Unit** (`QuestCompletionCascadeTests`, driving the static core through
  dictionary-backed delegates):
  - already-Done prerequisites are excluded;
  - a deep chain is fully traversed, post-order, clicked quest last;
  - a diamond dependency lists the shared prerequisite once;
  - a prerequisite with `AlternativeQuests` is skipped, its subtree included;
  - alternatives already Done or Failed are not in the fail list;
  - a prerequisite-free, alternative-free quest yields an empty cascade;
  - cycle safety (mutually-requiring quests terminate, each listed once);
  - `TaskRequirements` empty-but-non-null does **not** fall back to `Previous`;
  - a prerequisite recorded Done under its `NormalizedName` while carrying an
    Id is still excluded (the dual-key done-check);
  - an alternative that the cascade itself completes is not also failed;
  - the preview mutates nothing (recorded state identical after).
  - Localization: every new dialog string resolves non-empty in EN/KO/JA and
    the KO/JA texts differ from EN (uninitialized-instance pattern from
    `LocalizationHeaderStringsTests`).
- **E2E** (`QuestCascadeConfirmE2ETests`, `[Collection("E2E")]`,
  `Category=E2E`, data from the bundled `tarkov_data.db`):
  - completing a Locked quest with an Active prerequisite shows the dialog
    listing that prerequisite; Cancel closes it and nothing changes (the
    prerequisite is still completable, no Reset button appears);
  - the same flow through Confirm marks the quest and its prerequisite Done
    (both flip to the Reset-visible detail state);
  - completing a prerequisite-free, alternative-free Active quest shows no
    dialog and completes immediately.
- **E2E regression**: the full quest e2e suite (`QuestNavigationE2ETests` +
  the new class) runs green — in particular
  `Detail_buttons_act_on_the_shown_quest_while_it_is_hidden_by_filters`, whose
  prerequisite completion must stay dialog-free via the extended shared query.

## Verification

- `dotnet build TarkovHelper.sln` — 0 warnings.
- `dotnet test --filter Category!=E2E` — unit + doc guards.
- `dotnet test --filter Category=E2E` — on an interactive desktop.
- Manual: run via `dotnet TarkovHelper.dll`, click Done on a Locked quest —
  observable result: the dialog lists the chain (and any failures in red);
  Cancel leaves everything untouched; Confirm completes the chain. Clicking
  Done on a plain Active quest completes instantly with no dialog.

## Risks & Migration

No data, schema, or settings change; rollback is a straight revert. The
regression surface is `CompleteQuest` itself — its traversal moved into the
core — pinned by the unit tests above (which encode the old semantics,
including the obscure ones) and by the e2e completion paths. Batch-save and
single-notification behavior are preserved unchanged: the apply step writes
the same keys in the same order the interleaved code did.

## Amendment — deep-review fixes (2026-08-05)

A deep review of this change (branch `fix/quest-cascade-review`) revised two
decisions recorded above and hardened several details. The original text is
kept as the rationale of record; where this section disagrees, it wins.

- **Requirement semantics: mirrored from `ArePrerequisitesMet`, no longer
  "pre-refactor verbatim".** The pre-refactor traversal ignored
  `TaskRequirement.Status` and `GroupId`, which the review showed to be a bug,
  not a decision: it auto-completed Fail-type prerequisites (the bundled DB
  has 2 — e.g. *Another Shipping Delay* requires *Hot Wheels* **failed**, and
  marking it Done locks the dependent quest permanently), over-completed
  Accept-type prerequisites (23 rows), and completed every member of a
  multi-member OR group (e.g. *Make Amends*' three branches). The core now
  auto-completes only plain Complete-type requirements; Fail-/Accept-type
  requirements and multi-member OR groups are skipped entirely — the same
  "user must choose" precedent the alternative-prerequisite skip already set.
  A single-member group still behaves as a plain requirement.
- **The confirmed plan is applied verbatim, not recomputed.** The core returns
  an ordered `QuestCompletionPlan` whose entries carry the progress key they
  are written under; `GetCompletionCascade` wraps it and the new
  `ApplyCompletionCascade` writes it as-is. `ShowDialog` pumps the dispatcher,
  so background log-sync events can mutate progress while the modal is open —
  recomputing after Confirm (the original design) could apply a cascade the
  user never saw. `CompleteQuest(task, bool)` still computes-and-applies for
  the log-sync callers.
- **`_questProgress` is now `OrdinalIgnoreCase`**, matching the task-lookup
  dictionaries, the traversal's key sets, and the dictionary
  `UserDataDbService.LoadQuestProgressAsync` already builds (whose comparer
  was silently dropped on copy-in): stored key casing was never canonical.
- **Progress keys tolerate an empty-string first Id** (first *non-empty* Id,
  else NormalizedName), so an anomalous `Ids = [""]` row can neither become
  the literal key `""` nor silently no-op a completion.
- **Dialog**: construction moved behind a static `Confirm(owner, task,
  cascade)` factory (the `Windows/` convention); Escape/Enter work via
  `IsCancel`/`IsDefault`; the two dismiss buttons share one handler; the
  content sits in one outer `ScrollViewer` (the per-section `MaxHeight`s
  clipped the failed list at larger base font sizes); the `#FF5722` accent
  became the shared `AlternativeQuestBrush` resource, also used by the detail
  panel's "Other Choices" section.
- **The debug skip-traces were removed from the pure core** — they fired twice
  per completed click, and on cancelled clicks, from a method documented as
  side-effect-free.
- **Tests**: the requirement-semantics rules, the plan shape (keys, the
  prerequisites/clicked split), the `completePrerequisites: false` mode, and
  `GetCompletionCascade`/`IsEmpty` (instance-level, uninitialized-service
  pattern) gained unit guards; the `QuestWorld.GetStatus` double now mirrors
  the real name-key fallback; e2e gained the auto-failed-alternative preview,
  X-button dismissal, and user_data.db persistence assertions.
