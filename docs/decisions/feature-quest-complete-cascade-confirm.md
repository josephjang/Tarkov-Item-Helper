# Quest Complete Cascade Confirmation — PRD

- **Created**: 2026-08-03

> The sibling `feature-quest-complete-cascade-confirm.spec.md` holds the technical
> design. Written on the work's branch and merged in the same PR as the work.
> Nothing is kept current: fields are written once, discoveries are appended. A
> later change that reverses a decision here appends `Superseded by <doc>` below
> this line, in the PR that reverses it.

## Summary

Marking a quest complete on the Quests tab can silently change other quests:
every incomplete prerequisite is auto-completed, and every mutually exclusive
alternative is auto-failed. This change adds a confirmation dialog that appears
only when such a cascade would happen, previewing exactly which quests would be
completed and which would be failed, with the failures visually emphasized. A
completion that affects only the clicked quest stays one-click.

## Problem

The Quests tab offers two completion controls — the "Done" button on each list
row and "Mark Complete" in the detail panel. Both apply immediately, and both do
more than complete the clicked quest: every incomplete prerequisite in its chain
is marked done, and every mutually exclusive alternative quest is marked failed.
Nothing warns the user, nothing previews the scope, and there is no undo.

Two hazards follow. First, accidental chain completion: the Done button is also
present on Locked quests deep in the quest tree, so one misclick can silently
mark a long prerequisite chain as done, and recovering means finding and
resetting each quest by hand (Reset acts on a single quest). Second, silent
failing: completing a quest permanently fails its mutually exclusive
alternatives, and the only hint is a static "Other Choices" section further down
the detail panel that the user may never have scrolled to.

At the same time, completing quests is the tab's highest-frequency action —
players mark several quests done after every raid — so a remedy must not tax the
common case.

## Goals

- Before a completion that would change any quest other than the clicked one,
  the user sees exactly which quests will be completed and which will be failed,
  and can back out.
- A completion that affects only the clicked quest stays one-click.
- The destructive part of the cascade — failing alternatives — is visually
  unmistakable in the preview.

## Non-Goals

- **Gating log-sync auto-completion.** Quest completions detected from the
  game's own log mirror what already happened in the raid — the raid is the
  confirmation. A dialog there could only delay reflecting reality (and the
  manual log-sync flow already has its own preview dialog).
- **Undo.** A rollback mechanism for an applied cascade was considered and
  declined; see Product Decisions.
- **Changing Reset.** Reset stays single-quest; bulk reset is a different
  feature.

## Requirements / Acceptance Criteria

- **R1** — Clicking Done (list row) or Mark Complete (detail panel) on a quest
  whose completion would auto-complete at least one prerequisite or auto-fail at
  least one alternative opens a confirmation dialog; no quest status changes
  before the user confirms.
- **R2** — The dialog shows two sections with counts: quests that will also be
  completed, and — separately, red-emphasized — quests that will be failed. Each
  entry shows the quest's localized name and its trader.
- **R3** — Confirm applies the completion including the previewed cascade;
  Cancel (or closing the dialog) changes nothing.
- **R4** — Completing a quest with no incomplete prerequisites and no failable
  alternatives applies immediately, with no dialog — exactly today's one-click
  behavior.
- **R5** — The dialog matches the app's dark styling, is fully localized
  (EN/KO/JA), and opens centered over the main window.

## Product Decisions

**Confirm only when the cascade is non-empty.** The alternatives were: confirm
every completion (protects most, but adds a click to the per-raid
high-frequency action where it is nearly always noise), a "don't ask again"
setting (adds a setting and then either under-protects or keeps taxing), and
undo instead of confirmation (below). Gating on the cascade puts friction
exactly where the action is destructive: the overwhelmingly common plain
completion stays one-click, while every completion that would silently change
other quests now says what it is about to do.

**Preview both lists, and emphasize the failures.** A minimal dialog ("This
will also change N quests — continue?") was rejected: the user cannot validate
a number against their intent, only a list. So the dialog names each quest with
its trader, in two sections — "will also be completed" and "will be FAILED" —
with the failed section rendered in the same red accent the detail panel
already uses for its mutually exclusive "Other Choices" section, keeping one
visual language for "this fails quests".

**The Locked-quest hazard is narrowed by construction, not by special case.**
No per-status rule exists to drift out of sync: whenever a completion would
change other quests, the dialog appears, so the misclick that used to silently
complete a chain now shows "N quests will also be completed" first. The
boundary is the cascade itself, not the lock icon: a quest that is Locked or
LevelLocked by a non-prerequisite gate (player level, Scav karma, the DSP
decode counter) or whose only prerequisite is a mutually exclusive choice the
cascade refuses to auto-complete still completes one-click — correctly so,
since completing it changes nothing else.

**Confirmation over undo.** Undo would preserve the one-click flow, but an
applied cascade fans out immediately — per-quest progress rows are written and
UI, sync, and profile logic react to the change — so a faithful rollback is a
far larger mechanism, and the wrong state still exists and propagates until
undone. Prevention is simpler and matches the app's precedent: the log-sync
flow previews bulk changes in a dialog before applying them.

## Risks

The bulk-complete shortcut some users rely on (click a deep quest to complete
its whole branch) survives with one extra click — the dialog doubles as a
preview of that bulk action rather than removing it.

The dialog is new friction on some completions; it is bounded to exactly the
completions that change other quests, and the common case is untouched.

The preview and the applied cascade are computed by one shared traversal (see
the sibling spec), so "preview said X, apply did Y" divergence is designed out;
the residual risk is a rule being wrong for both equally — which the dialog at
least makes visible before it applies.
