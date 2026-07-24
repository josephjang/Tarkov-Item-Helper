# Independent Fork Release Process PRD

## Overview

- **Status**: Completed (PR #10 merged to main; first release v2026.7.0 published 2026-07-25)
- **Created**: 2026-07-24
- **Updated**: 2026-07-25
- **Owner**: josephjang
- **Translations**: Korean companion at `feature-fork-release-process.ko.md` (kept in sync 1:1)

## Problem Statement

This fork (josephjang/Tarkov-Item-Helper) has developed independently of upstream
(Zeliper/Tarkov-Item-Helper) since going fork-first on 2026-07-02. To let other users run
the fork, it needs its own binary releases on GitHub — but as of v4.3.1 the fork cannot
release at all:

- Every update channel hardcodes the upstream repo: the app self-update feed
  (`UpdateService.cs`), the DB auto-update URLs (`DatabaseUpdateService.cs`), and the
  `update.xml` download/changelog URLs. A fork build shipped as-is would offer to
  **replace itself with the upstream build**.
- The zip packaging step (`CreateRelease.bat`) was never committed and no longer exists
  anywhere — releases are unreproducible.
- There is no LICENSE file (README claims MIT in prose only), no CI, and the shipped app
  omits `Assets/db_version.txt`, so every fresh install re-downloads the full DB once.

## Goals

- [x] Goal 1: A fork build updates **only** against the fork: app feed, DB feed, and
      download URLs all point at josephjang/Tarkov-Item-Helper.
- [x] Goal 2: A repeatable, mostly-automated release process: pushing a `v*` tag
      builds, tests, packages, and publishes a GitHub Release with `TarkovHelper.zip`.
- [x] Goal 3: Clients never see a download URL that 404s (update.xml is bumped only
      after the release asset exists).
- [x] Goal 4: The fork is legally distributable: a real MIT `LICENSE` file with dual
      copyright (Zeliper + Jeongho Jang), and READMEs that present the project as a
      maintained fork.
- [x] Goal 5: First release **v2026.7.0** published and verified end-to-end.

## Non-Goals (Scope Out)

- Renaming the app or changing the executable/zip name (`TarkovHelper.exe`,
  `TarkovHelper.zip` — AutoUpdater.NET expects the existing layout).
- An installer (MSI/MSIX/Inno) — distribution stays a portable zip, as upstream shipped.
- Self-contained builds — the app remains framework-dependent (.NET 8 Desktop Runtime).
- Migrating existing upstream installs (they poll the upstream feed; out of our control).
- Pushing the 26 pre-v4.3.1 upstream tags to the fork (only `v4.3.1` is pushed, as the
  release-notes baseline).

## Technical Decisions

| Decision | Rationale | Date |
|----------|-----------|------|
| **CalVer `YYYY.M.N`** (N = 0-based release counter within the month, no fix/feature semantics), starting at **2026.7.0** | Unmistakably distinct from upstream's 4.x SemVer line; always numerically greater (2026 > 4) so `System.Version` comparison keeps working; fits the app's data-driven, game-patch-driven release cadence | 2026-07-24 |
| Release automation via **GitHub Actions on `v*` tag push** (`.github/workflows/release.yml`) | Reproducible builds independent of any local machine; replaces the lost `CreateRelease.bat` step | 2026-07-24 |
| Package stays a **framework-dependent zip** (`build/Create-ReleasePackage.ps1`, zip root = app root) | Matches AutoUpdater.NET's in-place extract-over-install-dir model and the existing user expectation (.NET 8 Desktop Runtime requirement unchanged); keeps downloads small | 2026-07-24 |
| **Two-step update.xml flow**: tag/release first, bump update.xml on main only after the asset is verified | The feed is polled straight from `main` every 3 min; bumping it last means a client can never be offered a URL that doesn't resolve yet | 2026-07-24 |
| `update.xml` version intentionally **lags** the csproj version between releases; the release workflow guards tag == csproj only | Direct consequence of the two-step flow; asserting xml == csproj would break every release run | 2026-07-24 |
| Guard tests pin the three feed constants and the update.xml URLs to `/josephjang/` | A bad merge that reintroduced upstream URLs would make fork builds replace themselves with the upstream app — the worst silent failure this repo can have | 2026-07-24 |
| Push exactly one legacy tag, `v4.3.1`, to origin | Gives `--generate-notes` and the changelog a `v4.3.1...v2026.7.0` baseline; old commits contain no workflow file, so pushing the tag triggers nothing | 2026-07-24 |

## Implementation Plan

### Phase 1: Make fork builds self-hosting (repoint + fix packaging gaps)

- [x] Task 1.1: Repoint app self-update feed and make the parser testable
  - Files: `TarkovHelper/Services/UpdateService.cs` (`UpdateXmlUrl` → fork, `internal`;
    `ParseUpdateXml` → `internal static`)
- [x] Task 1.2: Repoint DB auto-update URLs
  - Files: `TarkovHelper/Services/DatabaseUpdateService.cs` (`VERSION_URL`/`DATABASE_URL`
    → fork, `internal`)
- [x] Task 1.3: Remove the dead upstream URL constant
  - Files: `TarkovHelper/App.xaml.cs` (unused `UpdateXmlUrl` + orphaned
    `using AutoUpdaterDotNET;`)
- [x] Task 1.4: Repoint `update.xml` hosts (version stays 4.3.1 until the first release —
      no fork client can be below 2026.7.0, so the v4.3.1 URL is never followed)
  - Files: `update.xml`
- [x] Task 1.5: Ship `Assets/db_version.txt`; add app identity metadata and test visibility
  - Files: `TarkovHelper/TarkovHelper.csproj` (copy item, `Product`/`Authors`/`Copyright`/
    `RepositoryUrl`, `InternalsVisibleTo`), `TarkovHelper/app.manifest` (identity name)

### Phase 2: Packaging, CI, and release automation

- [x] Task 2.1: Committed packaging script (replaces the lost `CreateRelease.bat`)
  - Files: `build/Create-ReleasePackage.ps1`, `.gitignore` (`artifacts/`)
- [x] Task 2.2: Tag-triggered release workflow (version guard → test → package →
      `gh release create` with `--generate-notes`)
  - Files: `.github/workflows/release.yml`
- [x] Task 2.3: Minimal CI on PRs and main pushes
  - Files: `.github/workflows/ci.yml`

### Phase 3: Legal + docs

- [x] Task 3.1: MIT `LICENSE` with dual copyright
  - Files: `LICENSE`
- [x] Task 3.2: READMEs present the fork (notice, clone URL, Desktop Runtime, license
      link, credits; upstream donation badge removed)
  - Files: `README.md`, `README_KR.md`, `README_JA.md`
- [x] Task 3.3: Rewrite the `/release` command around the new flow; add a short
      Releases section to the root guide; fix stale references
  - Files: `.claude/commands/release.md`, `CLAUDE.md`,
    `docs/DatabaseUpdateMechanism.md`,
    `docs/PRDs/active/feature-hideout-localized-sort.md` + `.ko.md`

### Phase 4: Guard tests

- [x] Task 4.1: update.xml contract tests via the real parser (parseable, fork-hosted,
      URL matches its own version; csproj-version match intentionally NOT asserted)
  - Files: `TarkovHelper.Tests/UpdateXmlTests.cs`
- [x] Task 4.2: `ParseUpdateXml` edge cases + fork-URL constant guards
  - Files: `TarkovHelper.Tests/UpdateServiceTests.cs`

### Phase 5: First release (after this PR merges to main)

- [x] Task 5.1: Push the baseline tag: `git push origin refs/tags/v4.3.1`
- [x] Task 5.2: Run `/release 2026.7.0` (see "Release Flow" below)
- [x] Task 5.3: Verify per the checklist in Completion Criteria (CI + automated + asset/feed
      verified; manual runtime smoke checks of the published zip still pending — see criteria)

## Release Flow (the defined process)

Canonical executable steps live in `.claude/commands/release.md`; summary:

1. **Preflight**: version matches `^\d{4}\.\d{1,2}\.\d+$`; tag `v<ver>` unused; on a
   clean, pulled `main`; `gh auth status` OK (all `gh` calls use
   `-R josephjang/Tarkov-Item-Helper` — two remotes exist).
2. Bump `<Version>` in `TarkovHelper.csproj` (AssemblyVersion/FileVersion derive from
   it; **not** update.xml), commit `chore(release): bump version to <ver>`.
3. `git tag v<ver>` → `git push --atomic origin main v<ver>` — atomic so a rejected
   `main` push can't leave the tag firing CI on an unpublished commit; push exactly this
   tag, never `git push --tags` (26 legacy upstream tags v0.9.0–v4.3.0 remain local-only
   by decision).
4. CI (`release.yml`): tag/csproj guard → tests → package → GitHub Release with
   `TarkovHelper.zip` + generated notes. Wait with `gh run watch --exit-status`.
5. Curate bilingual (EN/KO) notes via `gh release edit --notes-file`, with the
   `compare/<prev>...v<ver>` link.
6. Verify the `TarkovHelper.zip` asset exists (`gh release view --json assets`).
7. **Only now** bump `update.xml` (version + download URL) on main — clients (3-min
   poll of raw main) first see the new version only after its asset provably exists.

**Failure recovery**: update.xml was never touched, so no client saw anything — delete
the release and tag, fix on main, re-tag the **same** version.

## Progress Log

| Date | Update | By |
|------|--------|-----|
| 2026-07-24 | PRD created. Decisions locked with owner: CalVer `YYYY.M.N` from 2026.7.0, tag-triggered Actions release, framework-dependent zip, remove upstream donation badge, push only `v4.3.1` of the legacy tags. Phases 1–4 implemented in the same session (feature/fork-release-process). | josephjang |
| 2026-07-25 | Deep review of the branch: 16 fixes applied (ref_name injection → env; single `<Version>` source so AssemblyVersion/FileVersion can't drift; prune non-Windows runtimes −9 MB; publish sanity check; anchored version XPath + CalVer guard; `--atomic` push; full-URL feed pins + Ordinal + migration/boundary guard tests; preflight local build gate). PR #10 merged to main (CI green). | josephjang |
| 2026-07-25 | **First release published.** Pushed `v4.3.1` baseline tag; bumped csproj → 2026.7.0; `git push --atomic origin main v2026.7.0` triggered `release.yml` (green, 5m3s: version guard → build → test → package → release). GitHub Release `v2026.7.0` live with `TarkovHelper.zip` (36.2 MB, not draft/prerelease); curated bilingual notes applied; `update.xml` bumped to 2026.7.0 on main after asset verification. | josephjang |

## Completion Criteria

- [x] All Goals met
- [x] PR CI (`ci.yml`) green; branch merged to main
- [x] `v4.3.1` baseline tag pushed to origin
- [x] First release published: `release.yml` green for `v2026.7.0`;
      `TarkovHelper.zip` attached (public repo, unauthenticated download)
- [x] Zip layout verified (locally, same packaging script CI runs): `TarkovHelper.exe` +
      `Assets/` (incl. `db_version.txt`) at zip root; no `*.pdb`; no `Config/Data/Cache/Logs`
- [x] `update.xml` on main advertises 2026.7.0 after the post-release bump
- [x] Unit guards green: `UpdateXmlTests`, `UpdateServiceTests`
- [ ] **Pending manual smoke checks** of the *published* zip (not yet run this session):
      extracted app runs via `dotnet TarkovHelper.dll` and shows `v2026.7.0`; in-app update
      check reports up to date; DB update check reports up to date. (Optional AutoUpdater
      self-update rehearsal from a local 2026.6.0 build also pending.)

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| raw.githubusercontent CDN caches update.xml ~5 min | Low — visibility delay only, never a 404 (asset exists before the bump) | Accepted |
| `--generate-notes` noisy on the first release | Low | Step 5 overwrites with curated bilingual notes |
| A future upstream merge reintroduces Zeliper URLs | High — fork builds would replace themselves with the upstream app | `Update_feed_constants_point_at_fork` + `Update_xml_urls_point_at_fork` guard tests fail the build |
| Elevated app + AutoUpdater in-place replacement misbehaves | Medium | Unchanged from upstream behavior; optional rehearsal verifies end-to-end |
| Release workflow fails mid-flight | Low | Two-step flow means clients saw nothing; documented recovery re-tags the same version |
