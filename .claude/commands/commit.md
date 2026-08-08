---
description: Stage and commit changes by feature using conventional commits
---

Split the current working-tree changes into feature-based commits: one
commit per feature/purpose, not one big commit.

Message format, scopes, and the no-attribution rule are defined in
CLAUDE.md ("Commits & Branches"); follow that section and match the
style of recent `git log`.

## Procedure

- Verify the solution builds before the first commit:
  `dotnet build TarkovHelper.sln`.
- Stage by explicit path only; never `git add -A`, `.`, or `-u`.
- Write each message from that group's staged diff (`git diff --cached`).
- Pass multi-line messages as single-quoted here-strings and keep double
  quotes out of them; in PowerShell they can split git arguments.
- Commit only; never push, never use destructive git commands.
- Nothing to commit, or changes too intermingled to split cleanly →
  report and stop instead of guessing.
