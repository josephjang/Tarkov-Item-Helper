# Repository Rename to TarkovHelper PRD

- **Created**: 2026-08-08

> The sibling `feature-repo-rename.spec.md` holds the technical design. Written on
> the work's branch and merged in the same PR as the work.

## Summary

The GitHub repository is renamed from `josephjang/Tarkov-Item-Helper` to
`josephjang/TarkovHelper`, and the assembly product name changes from
"Tarkov Item Helper" to "Tarkov Helper". The old repository name is permanently
retired: no repository may ever be created under that name again, because GitHub's
rename redirects (which keep every installed client updating) last only while the
old name stays unclaimed.

## Problem

The repository name described only a fraction of the product and no longer matched
anything the user touches:

- The app tracks quests, hideout upgrades, items, and live map position with an
  in-game overlay. "Item Helper" describes one tab of five.
- Users downloaded `TarkovHelper.zip` from a repo named `Tarkov-Item-Helper` and ran
  `TarkovHelper.exe`, whose main window is titled "Tarkov Helper". Three names for
  one product.
- The repo shares its name with its upstream (`Zeliper/Tarkov-Item-Helper`), so in
  search results and links it reads as "someone's fork" rather than an independently
  released project.

## Goals

- The repository name matches the shipped artifact names (`TarkovHelper.zip`,
  `TarkovHelper.exe`).
- The fork is distinguishable from upstream by name alone.
- Every client installed before the rename keeps receiving app updates and database
  updates with no user action.

## Non-Goals

- Renaming the executable, assembly, or zip. They already carry the target name.
- Adopting a new distinctive brand (a RatScanner-style invented name). That would
  require renaming the executable and zip too, which creates real auto-update
  migration hazards; reconsider only if the product outgrows the generic name.
- Detaching the repository from the GitHub fork network. Worth doing for search
  visibility (GitHub search hides forks), but it is a separate GitHub Support
  request, not part of this change.
- Changing upstream attribution. `Zeliper/Tarkov-Item-Helper` keeps its name and
  the READMEs keep linking to it.

## Requirements / Acceptance Criteria

- R1: The repository is reachable at `https://github.com/josephjang/TarkovHelper`.
- R2: Old URLs (web pages, git remotes, raw file URLs, release asset downloads)
  redirect to the new name.
- R3: A client installed before the rename receives the next app update and the
  next database update without reinstalling.
- R4: READMEs and install instructions reference only the new URL.
- R5: The file properties of `TarkovHelper.exe` show Product "Tarkov Helper".

## Product Decisions

**`TarkovHelper` over the alternatives.** Matching the shipped artifact names was
the deciding factor: the solution, project, executable, and release zip were all
already `TarkovHelper`, so any other choice would have kept (or created) a mismatch.
The hyphenated `Tarkov-Helper` was rejected because an existing Discord bot repo
(BetrixDev/Tarkov-Helper) uses exactly that name; the unhyphenated form has only
abandoned zero-star namesakes. A new invented brand was rejected for now (see
Non-Goals).

**The old name is retired forever.** GitHub redirects renamed-repo URLs only until
a new repository claims the old name. Installed clients have
`Tarkov-Item-Helper` URLs compiled in, so creating any future repository named
`josephjang/Tarkov-Item-Helper` would silently break their update feeds. This is an
operating constraint on the account, recorded here because nothing in the code can
enforce it.

**Merged decision documents keep the old URLs.** Decision docs are append-only
history and the old URLs in them still resolve via redirect. Only living documents
(READMEs, CLAUDE.md, release command, reference docs) were updated.

## Risks

Installed clients now depend on GitHub's redirect behavior for renamed
repositories. Acceptable: GitHub redirects web, git, raw, and release-download
URLs indefinitely for renamed repositories, and the only way to break that
(reusing the old name) is under our control and forbidden above. New releases bake
in the new URLs, so the redirect-dependent population shrinks with every update.
