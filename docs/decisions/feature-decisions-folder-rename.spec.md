# Decisions Folder Rename Technical Spec

- **Created**: 2026-08-08

> The sibling `feature-decisions-folder-rename.md` holds the product decision.
> Written on the work's branch and merged in the same PR as the work.

## Summary

One `git mv docs/PRDs docs/decisions`, plus updates to the eight live files that
carry the old path. Frozen documents are untouched: the path guard's token regex
moves to the new path, which takes historical tokens out of its scope mechanically.

## Current Behavior

`docs/PRDs/` is referenced from eight live files: root `CLAUDE.md` (three path
mentions, one commit-scope example, and the "folder's name predates the split"
sentence), `.claude/commands/release.md` (one path mention),
`TarkovHelper.Tests/PrdDocsTests.cs` (directory helper, token regex, assertion
messages, comments), `TarkovHelper.Tests/TestRepo.cs` (the repo-root walk requires
`docs/PRDs` beside `TarkovHelper.sln`), three path-bearing code comments
(`FontStacks.cs`, `FontAssetsTests.cs`, `FontStacksTests.cs`, each citing the
`feature-eft-font-stack` pair by path, against the reference-by-filename
convention), and the folder's own `README.md` (title, name-explanation paragraph,
structure diagram). Frozen documents additionally mention the path as history.

## Design

- `git mv docs/PRDs docs/decisions`. Every file registers as a rename; no filename
  changes.
- `CLAUDE.md`: the three path mentions update; the name-explanation sentence is
  removed; the commit-scope example `PRDs` becomes `decisions`.
- `.claude/commands/release.md`: the one path mention updates.
- `TarkovHelper.Tests/PrdDocsTests.cs` is renamed to `DecisionDocsTests.cs` with
  class `DecisionDocsTests`; the directory helper, the token regex (now
  `docs/decisions/`), assertion messages, and comments follow.
- `TarkovHelper.Tests/TestRepo.cs`: the repo-root marker becomes `docs/decisions`;
  the comment and the exception message follow.
- The three font-related comments become filename-only references
  (`feature-eft-font-stack.md`, `feature-eft-font-stack.spec.md`), so they can
  never break on a folder move again.
- `docs/decisions/README.md`: retitled to "결정 문서 (Decision Docs)"; the
  name-explanation paragraph is removed; the structure diagram updates.
- Supersede notes are appended to `feature-decision-docs-process.md` and
  `feature-decision-docs-process.spec.md`.

## Technical Decisions

**The test class renames with the folder.** `PrdDocsTests` was named for the
folder; keeping the stale name would recreate in code the mismatch this change
removes from the tree. The only live inbound reference was a `TestRepo.cs` comment
(updated); frozen documents that cite the old class name stay as history.

**Historical tokens leave the guard's scope by regex, not by editing.** The
path-resolution invariant now scans for `docs/decisions/` tokens only, so
`docs/PRDs/` mentions in frozen documents are simply no longer matched. No frozen
file is edited and no new exemption list is introduced. The existing exemption for
`feature-decision-docs-process.spec.md` stays: that spec records removed `active/`
paths that would not resolve under any folder name, and the exemption documents
that design.

**The old-path sweep is verification, not a test.** The guard cannot flag a missed
`docs/PRDs` update, because old tokens are exactly what it no longer scans. A
`git grep` sweep in Verification covers that one-time concern; a permanent test for
a path that no longer exists would be dead weight after this PR merges.

## Test Strategy

- **Unit**: the four existing invariants in `DecisionDocsTests` run against the new
  location. The path-resolution invariant proves every `docs/decisions/` token in a
  tracked file resolves; the other three prove the format survived the move.
- **E2E**: none. No application behavior changes; the app never reads these paths.

## Verification

```powershell
# 1. The folder is renamed
Test-Path docs/PRDs        # expected: False
Test-Path docs/decisions   # expected: True

# 2. No live file references the old path (frozen documents under
#    docs/decisions/ keep historical mentions by design)
git grep -n "docs/PRDs" -- . ":!docs/decisions/"
#    expected: no output

# 3. The move registered as renames, not delete+add (run after committing)
git show --stat -M HEAD

# 4. Build and the four invariants
dotnet build TarkovHelper.sln
dotnet test TarkovHelper.Tests/TarkovHelper.Tests.csproj --filter "FullyQualifiedName~DecisionDocsTests"
#    expected: 4 passing
```

## Risks & Migration

- GitHub blob URLs into the old folder path stop resolving on the default branch;
  accepted in the sibling PRD's Risks.
- `git log --follow` and `gh pr list --search "<name>"` keep resolving documents:
  filenames are unchanged and the move is a rename.
- Rollback: revert the commit; the rename registers as renames in both directions.
