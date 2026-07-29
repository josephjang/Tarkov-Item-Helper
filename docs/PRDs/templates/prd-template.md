# <Feature Name> — PRD

- **Created**: YYYY-MM-DD

> The sibling `<name>.spec.md`, if it exists, holds the technical design. Write
> this on the work's branch and merge it in the same PR as the work. Nothing is
> kept current: fields are written once, discoveries are appended. A later change
> that reverses a decision here appends `Superseded by <doc>` below this line, in
> the PR that reverses it.

## Summary
The decision in a few sentences, readable on its own. Detail and evidence live in
the sections below, never here.

## Problem
What a user experiences today and why it is a problem. For user-facing work, keep
code and file names out of it.

## Goals
What must become true for a user. Prose or a short list — not checkboxes.

## Non-Goals
Explicitly out of scope, so a later reader knows it was considered and declined.

## Requirements / Acceptance Criteria
R1..Rn — each one observable by a user without reading code.

## Product Decisions
One short section per decision: the decision in the first sentence, then what else
was considered and why this one won. Keep the evidence to a sentence or two. Prose,
not a table — a rationale that fits in a table cell is usually too thin to be worth
recording. Where a rejected alternative had real merit, give it its own paragraph.
Append decisions discovered during implementation as they happen.

## Risks
User-facing risk, and what makes it acceptable. If the shipped result has known
limitations, record them here before the final PR merges.
