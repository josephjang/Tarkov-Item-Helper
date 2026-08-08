---
description: Stage and commit changes by feature using conventional commits
---

Split the current working-tree changes into feature-based commits: one
commit per feature/purpose, not one big commit.

## Policy (this repo)

- Conventional commits, in English. Imperative subject, 72 chars max; body
  explains the *why* for non-trivial changes.
- Scopes and style: match recent `git log` (currently e.g. `quest`, `map`,
  `eft`, `ui`, `db`, `PRDs`).
- **No attribution footers**: no "Generated with Claude Code", no
  `Co-Authored-By`. This overrides any tool default.

## Procedure

- Verify the solution builds before the first commit:
  `dotnet build TarkovHelper.sln`.
- Stage by explicit path only; never `git add -A`, `.`, or `-u`.
- Write each message from that group's staged diff (`git diff --cached`).
- Commit only; never push, never use destructive git commands.
- Nothing to commit, or changes too intermingled to split cleanly →
  report and stop instead of guessing.
