# <Feature Name> — Technical Spec

- **Created**: YYYY-MM-DD

> The sibling `<name>.md`, if it exists, holds the product decision; a spec with no
> PRD is the normal shape for an internal change. Write this on the work's branch
> and merge it in the same PR as the work. Nothing is kept current: fields are
> written once, discoveries are appended. A later change that reverses a decision
> here appends `Superseded by <doc>` below this line, in the PR that reverses it.

## Summary
The design in a few sentences: what changes, and the one or two ideas it rests on.

## Goals
What must become true technically. Omit if a sibling PRD already states it.

## Non-Goals
Technical scope explicitly declined — adjacent problems you chose not to solve here.

## Current Behavior / Root Cause
How it works today, anchored to symbols. For a fix this is the *confirmed* root
cause — read the code first, never a guess.

## Design
Which services, files, and data flows change. Prose plus a file list. No phase
checkboxes: implementation order belongs in the session's plan file.

## Technical Decisions
One short section per decision: the decision in the first sentence, then what else
was considered and why this one won. Keep the evidence to a sentence or two; prose,
not a table. Append decisions reached during implementation as they happen — that
is history, and history does not go stale — including where the implementation
diverged from the design above.

## Open Questions
Anything genuinely unresolved, with what would settle it. Delete the section if empty.

## Test Strategy
- **Unit**: the invariant each guard test locks down
- **E2E**: which user-visible path
- If something cannot be tested automatically, say so here and why.

## Verification
Commands to run, including any manual check, and the observable result that proves
it works.

## Risks & Migration
Data migration, ordering, compatibility, rollback. Known limitations of the shipped
result go here before the final PR merges.
