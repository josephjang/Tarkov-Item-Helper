# Quest Chip-Only Status Filter — PRD

- **Created**: 2026-08-07

> The sibling `feature-quest-chip-only-status-filter.spec.md` holds the technical
> design. Write this on the work's branch and merge it in the same PR as the work.
> Nothing is kept current: fields are written once, discoveries are appended. A
> later change that reverses a decision here appends `Superseded by <doc>` below
> this line, in the PR that reverses it.

## Summary

The Quest tab's status dropdown is removed; the status chips on the statistics bar
become the only status filter. The chip row gains a leading "All" chip that both
offers the "show everything" state directly and carries the list's total count.
The "Lv.X | shown/total" statistics text is removed — the level already lives in
the header's profile drawer, and every count already lives on a chip. Chips become
the tab's visual status language: always shown in their status color, with the
selected chip filled.

This finishes what `feature-quest-overview-filters.md` started: that change made
the status numbers clickable but kept the old dropdown alongside them; this one
removes the duplication it left behind.

## Problem

The Quest tab has two controls for one filter: the status dropdown in the filter
bar and the clickable status chips directly beneath it. They always agree, so one
of them is pure duplication — and the dropdown is the weaker of the two (no
counts, one extra click to see the options). Next to the chips, a statistics text
shows the player's level and a shown/total quest count; the level is already
visible and editable in the header's profile drawer, and the counts are already
on the chips. The bar spends space repeating itself instead of being scannable at
a glance.

The chips also under-signal: unselected chips are colored only in their label,
and the selected chip is marked by a subtle neutral highlight — easy to miss now
that the dropdown no longer mirrors the selection.

## Goals

- One status control: the chips. Nothing else on the tab selects a status.
- "Show everything" is one direct click, and the total count stays visible.
- No duplicated level or count text on the tab.
- The selected status is legible at a glance from the chip row alone.

## Non-Goals

- Localizing the chip labels. They stay hardcoded English ("All" and
  "Unavailable" included). The predecessor's Non-Goal justified this by the chips
  having to agree with the combo's hardcoded labels — a reason this change voids
  by deleting the combo. The decision still stands on its own terms: the chips sit
  next to the filter bar's other hardcoded-English chrome ("Kappa", "Item Req",
  "All Traders", "All Maps"), and localizing one control ahead of the rest would
  make the bar read half-translated. All of it moves together in the Quest-tab
  localization pass.
- The Map tab's own status filter. It is a separate control with separate
  semantics and is untouched.
- The status badge on quest list rows keeps its short "N/A" text (see Product
  Decisions).
- No change to which filters persist or how; the saved status filter keeps its
  meaning across this change.

## Requirements / Acceptance Criteria

- R1: The statistics bar shows the chip row — All, Active, Locked, Done, Failed,
  Unavailable, in that order — and the kappa gauge. The status dropdown and the
  "Lv.X | shown/total" text are gone.
- R2: Each chip shows the number of quests the list would show if that chip were
  clicked, with all other filters applied as-is. The All chip's number is what
  the list shows under "All" — not the sum of the other chips and not the raw
  loaded total.
- R3: Clicking a chip applies that status. Clicking the selected status chip
  returns to "All". Clicking "All" while it is selected does nothing.
- R4: Every chip always shows its status color (label and border). The selected
  chip is additionally filled with a translucent tint of its color and a
  full-strength border — the selection is visible without reading any other
  control.
- R5: The unavailable-status chip is labeled "Unavailable", not "N/A".
- R6: The selected status still survives an app restart. A saved status the app
  no longer recognizes falls back to "All"; a fresh install starts on "Active".

## Product Decisions

**A dedicated All chip — reversing the earlier rejection.** The chip PRD rejected
an All chip as noise because the dropdown still offered "All" explicitly. With the
dropdown gone, that reasoning inverts: toggling the selected chip off would be the
only path to "All", an invisible gesture with no resting-state affordance, and the
total count would have no home once the statistics text is removed. The All chip
solves both — it is the explicit route to "show everything" and the place the
total lives. Re-clicking the selected chip still returns to All, so the learned
gesture keeps working.

**The All count is a click-preview, like every other chip.** It equals what the
list shows under "All" with the other filters as-is. The raw loaded total was
rejected: it would disagree with the click result whenever a faction is selected
(an other-faction quest is visible only under "Unavailable"), breaking the
"the number IS what clicking shows" promise the chips already make.

**The level display is removed, not relocated.** The header's profile drawer
already shows and edits the player level, and level's effect on quests is visible
where it matters — in the Locked count and the rows' level badges. A second
read-only copy on the quest tab was redundancy, not convenience.

**Selection is cued by a color fill, not a neutral highlight.** With the dropdown
gone the chips are the only place the current status filter is visible, so the
selection must read at a glance. A filled chip in its own status color is the
strongest cue that keeps the row's color language consistent; the previous
neutral background highlight was designed to defer to the dropdown and is too
subtle to stand alone.

**The chip says "Unavailable"; the row badge keeps "N/A".** "N/A" on the chip
was an abbreviation the removed dropdown spelled out as "Unavailable" anyway —
the full word is clearer and the chip row has the space. The per-row status badge
keeps "N/A": rows are width-constrained and the badge repeats hundreds of times,
where the abbreviation earns its keep. Chip and badge still share the same color,
which is the contract that keeps them recognizably the same status.

## Risks

- With a faction selected, the All chip's number is smaller than the number of
  quests the app has loaded. Accepted: the number is truthful for its own click,
  which is the promise every chip makes; the loaded total is not a number any
  click can produce.
- The player level is no longer visible on the quest tab itself. Accepted: it is
  one click away in the profile drawer, and the level-dependent information
  (which quests are level-locked) remains visible on the tab.
- A very narrow window can clip the end of the chip row. Pre-existing with the
  old layout, but this change makes it worse in two ways, so the row wraps instead
  of clipping. Worse first: the row grows a little (the removed statistics text
  frees ~106px while the All chip plus the "N/A" → "Unavailable" relabel spend
  ~116px), and — the part that actually matters — a clipped chip used to be
  harmless because the dropdown still offered every status, whereas now a clipped
  chip is a status the user cannot select at all. At the window's 600px minimum
  the ~520px row plus the kappa gauge does not fit. So the chip row is a
  `WrapPanel`: it flows onto a second line rather than running off the edge, and
  every status stays one click away at any width.
