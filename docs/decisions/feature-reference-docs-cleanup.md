# Reference Docs Cleanup PRD

- **Created**: 2026-08-08

> No sibling spec: the changes are mechanical moves and renames, and this document
> records the conventions they establish. Written on the work's branch and merged
> in the same PR as the work.

## Summary

Root `docs/` is reduced to living reference documents with one naming convention
and an index (`docs/README.md`). Five point-in-time snapshot documents and two
stray legacy `.prd` files move to the frozen archive, the script that generated one
of the snapshots is deleted, and reference docs adopt the conventions decision docs
already have: kebab-case filenames, new documents in English.

## Problem

The decision-docs side of `docs/` was rebuilt in 2026-07, but the reference-doc
layer next to it was never revisited:

- Half of the documents were dead snapshots posing as living reference. Two map
  analyses compared systems that no longer exist in the codebase (Legacy_Map,
  MapTrackerPage), two log analyses were one-off investigations dated 2025-12-18,
  and one document was a 2025-12 integration proposal implemented long ago by
  LogSyncService.
- Five filename styles coexisted (PascalCase, snake_case, SCREAMING_SNAKE, mixed,
  kebab-case), with no stated convention and no index.
- One document described `EftRaidEventService` under the filename
  EftLogEventService.md, a class name that does not exist.
- An upstream author's local analysis script (with their machine path hardcoded)
  sat inside the app project, and the two stray `.prd` files noted as out of scope
  by the decision-docs-process change were still tracked.

## Goals

- Everything directly under `docs/` describes the current system, and a reader can
  find it from an index.
- One filename convention and one language rule, shared with decision docs.
- The service document's filename and title match the real class.

## Non-Goals

- No content rewrites of the archived snapshots; they are frozen in their original
  format and keep their original filenames, like the rest of the archive.
- No relocation of tarkov-market-markers-api.md or the TarkovDBEditor documents;
  their locations are already correct.
- No translation of the existing Korean reference documents.

## Requirements / Acceptance Criteria

- R1: The top level of `docs/` holds only living reference documents plus
  README.md and `decisions/`.
- R2: Every reference document filename is kebab-case.
- R3: `docs/README.md` lists every reference document with a one-line description
  and states the conventions.
- R4: The EftRaidEventService document's filename and title carry the real class
  name.

## Product Decisions

**Snapshots are archived, not deleted.** The five moved documents
(eft_log_analysis.md, eft_raid_analysis.md, Map_System_Analysis.md,
MapTracker_v350_vs_Current_Analysis.md, LOG_DATA_INTEGRATION.md) are 2025-12-era
investigation output, the same era whose working files (tasks.md, todo.md) the
archive already holds, so `decisions/archive/2025-12/` is the natural address for
them. Deletion was considered and rejected: git history alone does not surface to a
browsing reader that these analyses ever existed, and the archive exists precisely
to be that surface.

**Reference docs adopt the decision-docs conventions.** kebab-case filenames and
English for new documents, recorded in `docs/README.md`. Renaming the four
surviving documents now was safe and cheap: measured before the change, no live
file referenced any of them by path (the only inbound references were the generator
script deleted here and one example line in the decisions README, updated here).
Existing Korean documents stay Korean, matching the decision-docs rule.

**save_raid_analysis.py is deleted, not relocated.** It generated
eft_raid_analysis.md against the upstream author's hardcoded machine path, its
output is now frozen in the archive, and regenerating a raid-statistics snapshot
has no standing use in this fork.

**The two stray `.prd` files join the archive.** Quests_DB_Migration.prd and
TabVerification.prd (both 2025-12) were explicitly left out of scope by the
decision-docs-process change; this cleanup is the natural place to finish the job.
They keep their `.prd` extension: the archive freezes originals as they were.

**coordinate_analysis.md moves to TarkovDBEditor/docs/.** An analysis note lived
in `TarkovDBEditor/Resources/`, a folder for packaged assets (the csproj copies
only svg/json/webp, so the file was never shipped). It moves to the project's
stated home for internal notes, renamed kebab-case: coordinate-analysis.md.

## Risks

The archived snapshots still contain claims a reader could mistake for current
documentation. Accepted: the archive is explicitly frozen history, and
`docs/README.md` states that snapshots do not live at the reference level.

External links to the old document paths break, since in-repo path renames are not
redirected. Accepted for the same reasons as in feature-decisions-folder-rename.md.
