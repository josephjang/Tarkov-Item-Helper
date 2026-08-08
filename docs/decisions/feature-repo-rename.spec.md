# Repository Rename to TarkovHelper Technical Spec

- **Created**: 2026-08-08

> The sibling `feature-repo-rename.md` holds the product decision. Written on the
> work's branch and merged in the same PR as the work.

## Summary

The rename is executed on GitHub first (`gh repo rename`), then every live
reference in the repository is updated in one pass. Deployed clients are migrated
by nothing at all: GitHub's rename redirects cover every URL compiled into
existing binaries, so old installs keep updating and each update they take moves
them onto binaries that carry the new URLs.

## Current Behavior / Root Cause

Three sets of URLs are compiled into every installed binary and one lives in the
repo's `main` branch:

- `UpdateService.UpdateXmlUrl`: the raw URL of `update.xml` on `main`, polled for
  app updates (AutoUpdater.NET).
- `DatabaseUpdateService.VERSION_URL` / `DATABASE_URL`: raw URLs of
  `db_version.txt` and `tarkov_data.db` on `main`, polled every 5 minutes.
- `update.xml` on `main`: the release-asset download URL and changelog URL that
  old clients follow when an update is offered.

All pointed at `josephjang/Tarkov-Item-Helper`. After the rename, requests to
those URLs are redirected by GitHub (this covers `github.com` pages, git
fetch/push, `raw.githubusercontent.com`, and `releases/download` assets), so no
client-side migration step exists or is needed.

## Design

GitHub-side: `gh repo rename TarkovHelper -R josephjang/Tarkov-Item-Helper`, then
`git remote set-url origin https://github.com/josephjang/TarkovHelper.git` in the
main checkout (shared by all worktrees).

Repository files updated (`josephjang/Tarkov-Item-Helper` to
`josephjang/TarkovHelper`; upstream `Zeliper/...` references untouched):

- `TarkovHelper/Services/UpdateService.cs` (update.xml feed URL)
- `TarkovHelper/Services/DatabaseUpdateService.cs` (DB version + download URLs)
- `update.xml` (release download + changelog URLs)
- `README.md`, `README.ko.md`, `README.ja.md` (badge, release links, clone command)
- `TarkovHelper/TarkovHelper.csproj` (`RepositoryUrl`, identity comment; `Product`
  changed from "Tarkov Item Helper" to "Tarkov Helper" to match the main window
  title)
- `TarkovHelper/Fonts/LICENSE-Bender.txt` (issues URL, product mention)
- `CLAUDE.md`, `.claude/commands/release.md` (release tooling references)
- `docs/DatabaseUpdateMechanism.md` (reference doc)
- `TarkovHelper.Tests/UpdateServiceTests.cs`, `TarkovHelper.Tests/UpdateXmlTests.cs`
  (pinned URL constants)

Deliberately not updated: `docs/PRDs/feature-fork-release-process.md` and its
`.ko.md` twin (append-only history), `docs/PRDs/archive/` (frozen), and all
`Zeliper/Tarkov-Item-Helper` upstream links (upstream keeps its name).

## Technical Decisions

**Rely on GitHub redirects instead of a client migration shim.** The alternative
(a transitional release that teaches old clients the new URLs before the rename)
adds a forced-ordering release for zero benefit: redirects cover every compiled-in
URL class, and the redirect lifetime is controlled by us (see the PRD's retirement
constraint).

**Tests pin the new URLs.** `UpdateServiceTests` and `UpdateXmlTests` assert the
exact constants, so a stray old-name reference in the update path fails the suite
rather than shipping and leaning on redirects unnecessarily.

## Test Strategy

- **Unit**: existing pinned-URL tests (`UpdateServiceTests.cs`, `UpdateXmlTests.cs`)
  updated to the new constants; they now guard against the old name reappearing in
  the update path.
- **E2E**: none. No user-visible behavior changes; the update flow against live
  GitHub is exercised by the next real release.

## Verification

- `git ls-remote origin HEAD` succeeds against the new URL.
- `dotnet build TarkovHelper.sln` and `dotnet test TarkovHelper.Tests` pass.
- Browser check: an old release-asset URL under `Tarkov-Item-Helper` redirects to
  the same asset under `TarkovHelper`.

## Risks & Migration

- The redirect chain breaks only if `josephjang/Tarkov-Item-Helper` is ever
  recreated; the PRD forbids that permanently.
- Local clones elsewhere (other machines) keep working via the git redirect but
  should run `git remote set-url` when convenient.
- Local folder names (`~/projects/Tarkov-Item-Helper`, orca workspace folders) are
  untouched; renaming them is cosmetic and breaks worktree registrations, so it is
  intentionally skipped.
