# README Restructure PRD

- **Created**: 2026-08-08

> No sibling spec exists: editing the READMEs involves no technical design. Write
> this on the work's branch and merge it in the same PR as the work. Nothing is
> kept current: fields are written once, discoveries are appended. A later change
> that reverses a decision here appends `Superseded by <doc>` below this line, in
> the PR that reverses it.

## Summary

The three READMEs (`README.md`, `README_KR.md`, `README_JA.md`) are stale on
most facts a player would act on, but the deeper problem is structural: this
repository distributes exclusively through GitHub Releases, so the README is
not documentation but the product page, the only surface where a player
decides whether to try the app. This document records the decision to
restructure the READMEs around that goal: show the app first, answer the trust
question ("is this safe to run alongside EFT?") explicitly, make the download
path first-class, shrink usage to the non-obvious, and correct every stale
fact along the way. All three language variants update 1:1 in the same PR with
English as the reference, and the restructured document makes no claim that
predictably rots with the next game patch.

## Problem

The README's likeliest reader is a player who searched for a Tarkov quest
tracker and is deciding in under a minute whether to try this one. The current
document fails that reader twice over.

**Structurally, it is ordered as documentation, not as a product page:**

- The screenshots (the single most persuasive element for a desktop GUI app)
  sit below the feature list, beneath a stale "Screenshots coming soon"
  comment.
- The app requests administrator elevation on every launch and reads game
  files, and for Escape from Tarkov the first question any player asks of a
  companion tool is "will this get me banned?" The README never addresses it:
  not the elevation, not how the app obtains game state, not what it does and
  doesn't touch. The strongest honest answer available (the app only reads
  files the game itself writes) goes unsaid.
- Getting the app, the README's primary call to action, is a one-line link
  buried mid-page under Installation.
- The fork note says what this repository *is* (an independently maintained
  fork) but not what it *offers* a player choosing between this and the
  original.
- The Usage section is a numbered walkthrough of self-explanatory UI ("check
  quest list in the Quests tab"): a mini-manual that adds length, rots with
  every UI change, and helps no one deciding whether to install.

**Factually, it misinforms the player who does read it:**

- The data-storage section describes JSON files (`tasks.json`,
  `progress.json`, ...) in a `Data/` folder. That design no longer exists
  (progress lives in a SQLite database under the app's `Config` folder), so a
  player following the README to back up their progress finds nothing, and a
  player who switches between a Debug build, a Release build, or an installed
  copy has no explanation for "lost" progress (each location keeps its own
  data; the in-app Data Migration button that solves this is never mentioned).
- Documented commands fail: `dotnet run -- --fetch` uses a flag the app no
  longer accepts, and `dotnet run -c Release` at the repo root fails because
  the solution contains three projects. The described first-run behavior
  (fetching data from the tarkov.dev API) is also wrong: the app ships with a
  prebuilt database and auto-updates it in the background.
- Half the app is undocumented: no Collector progress tab, no interactive Map
  with live in-raid position tracking, no in-game overlay minimap with global
  hotkeys, no PvP/PvE profile separation, no automatic app and data updates.
  The Usage section names a "Required Items" tab the app actually calls
  "Items".
- Stale and wrong claims: "Tarkov 1.0 Fully accepted" leads the feature list
  while the game is on patch 1.1 (see `feature-eft-1-1-roadmap.md`), and the
  tech stack claims C# 13 (the project builds with .NET 8 defaults).

The Korean and Japanese READMEs mirror the English content and therefore carry
the same structure and the same staleness.

## Goals

- A player who lands on the README sees what the app looks like, learns what it
  does, and understands why it is safe to run, without scrolling past prose to
  get there, and can reach the download in one click.
- A player can follow every README instruction (install, build, run, back up)
  and it works as written.
- An existing user can answer the likeliest support questions (where is my
  data, why did it disappear after switching builds, why the UAC prompt) from
  the README alone.
- The README makes no claim that predictably rots with the next game patch.
- All three language variants say the same thing.

## Non-Goals

- **No developer documentation expansion.** Architecture, service design, and
  contribution workflow stay in `CLAUDE.md` and `docs/`; the README's
  build-from-source section stays minimal.
- **No documentation of unshipped work.** The EFT 1.1 adaptation phases
  (`feature-eft-1-1-roadmap.md`) enter the README only as their PRs ship
  user-visible behavior, not as promises.
- **No ban-safety guarantee.** The README describes the mechanism and lets the
  player judge; it does not promise anything on Battlestate Games' behalf.
- **No screenshot automation or docs-site tooling.** Screenshots are
  re-captured by hand for this refresh; keeping them current stays a judgment
  call, not a pipeline.
- **No upstream README synchronization.** This fork's README evolves
  independently.

## Requirements / Acceptance Criteria

- R1: A current-UI screenshot appears directly after the opening description,
  before the feature list.
- R2: The README states how the app obtains game state, by reading files the
  game itself writes (log files for quest and raid events; screenshot
  filenames for map position), and states that it does not read game memory,
  inject code, or modify game files; it explains the administrator elevation
  in the same place and carries a use-at-your-own-discretion note.
- R3: A latest-release badge and a prominent download link appear near the top
  of the document; the installation section names Windows, the .NET 8 Desktop
  Runtime, and the administrator elevation.
- R4: Every shell command shown in the README succeeds verbatim from a fresh
  clone on a machine meeting the stated prerequisites.
- R5: The data sections describe the shipped behavior: a bundled game-data
  database updated automatically in-app, and user progress stored under the
  app's `Config` folder with per-install-location separation and the in-app
  migration path. No mention of a `Data/` JSON folder or a `--fetch` flag
  remains in any of the three READMEs or in `CLAUDE.md`.
- R6: The feature list names every main tab (Quests, Hideout, Items, Collector,
  Map) plus the overlay minimap, game-log quest sync, PvP/PvE profiles, and
  automatic app/data updates; it lists nothing the shipped app does not do.
- R7: Usage covers only flows a player cannot discover from the UI itself
  (game-log sync setup, data location and migration); it uses the tab names of
  the current UI and contains no step-by-step walkthroughs of self-evident
  screens.
- R8: The fork note states what this fork offers (its own releases and ongoing
  feature work) in wording that makes no characterization of the upstream
  project.
- R9: No compatibility claim tied to a specific game patch level appears
  anywhere in the READMEs.
- R10: `README.md`, `README_KR.md`, and `README_JA.md` are content-identical
  (modulo language and the Korean-UI screenshot set), and all screenshots show
  the current UI.

## Product Decisions

**The README is treated as the product page, and its structure follows from
that.** This repository has no website and no store listing; GitHub Releases is
the only distribution channel, so the README is the entire surface on which a
player decides to try the app. Treating it as project documentation (the
current shape, and what the first draft of this PRD preserved) was considered
and rejected: documentation ordering (features → install → usage → internals)
serves a reader who has already committed, while the actual likeliest reader
has not. The target structure is a conversion funnel: title and badges →
one-line description and fork note → hero screenshot → download and
requirements → feature list → how it works & safety → getting started →
remaining screenshots → build from source → license and credits. The license
and credits sections are already well done and carry over unchanged.

**A hero screenshot leads, above the fold.** For a desktop GUI app the
screenshot does more persuasion than every feature bullet combined. The quest
list capture leads (it is the app's core view); the other captures remain in a
short section further down. Screenshots are illustrative, not exhaustive:
capturing all five tabs was considered and declined, since three
representative captures set expectations adequately and every additional image
is another thing that rots.

**The README answers the safety question explicitly, by mechanism, without
promises.** A short "how it works" section states the verifiable facts: the
app obtains all game state passively, by reading files the game itself writes
(log files for quest/raid events, screenshot filenames for map position;
`ScreenshotWatcherService`) and never reads game memory, injects code, or
writes to game files; the overlay is an ordinary always-on-top window, and the
global hotkeys use a system-wide keyboard hook in the app's own process, which
is also why the app requests administrator elevation. Saying nothing was
rejected: the elevation prompt happens regardless, and an unexplained UAC
prompt from a game companion tool reads as a red flag. Promising ban safety
was equally rejected: only Battlestate Games controls that, so the section
describes the mechanism and closes with a use-at-your-own-discretion note.

**The download path is first-class.** A latest-release badge joins the language
badges at the top, and a download link appears immediately after the hero
screenshot rather than mid-page. Returning users looking for an update and new
users ready to try it share the same primary action, and the README should not
make either of them hunt for it.

**No game-patch-level claims, ever.** The "Tarkov 1.0 Fully accepted" line is
replaced by a description of the mechanism (game data updates automatically
in-app) rather than updated to "1.1". Updating the number was considered and
rejected: this exact line already rotted once, the 1.1 adaptation is mid-flight
so any current claim would be half-true, and the mechanism description stays
true across patches. This is the general stance: the restructure minimizes the
README's rot surface (evergreen claims, coarse feature list, no step-by-step
walkthroughs) instead of instituting a keep-it-current process, consistent
with the repository's decision-docs philosophy that maintenance-obligation
content predictably decays.

**Feature list = shipped behavior at decide-to-install altitude.** One line per
capability a player would weigh, no internals, no roadmap. Unshipped 1.1
adaptation work is excluded even though it is the repository's current focus:
a README that advertises in-flight work misleads exactly the reader the trust
sections are written for.

**Usage shrinks to the non-obvious.** The numbered walkthroughs of
self-explanatory screens are removed, not corrected: README-as-manual is the
anti-pattern, and every written step is a step that rots. What remains is what
a player cannot discover by clicking around: enabling game-log sync, and where
user data lives. Correcting the walkthroughs in place was the first draft's
approach and was rejected in favor of this.

**User-data documentation covers location, separation, and migration, not the
override.** "Where is my progress and why did it vanish after switching
builds" is the likeliest support question, so the README states where user
data lives, that each install location keeps its own data, and that the in-app
Data Migration button moves it. The `TARKOVHELPER_CONFIG_PATH` environment
variable stays undocumented in the README: it exists for tests and
development, and documenting it invites misuse as a sync mechanism.

**The fork note gains a value sentence and stays neutral.** The existing note
(independently maintained fork, CalVer scheme, link to the original project)
is kept and extended with one sentence on what the fork offers: its own
release cadence and ongoing feature work. The wording describes only this
fork; dropping the note or characterizing the upstream project were both
rejected: the first hides provenance, the second is not this repository's
place.

**Trilingual READMEs stay, 1:1, same PR, English wins.** Dropping the KR/JA
variants was considered (three copies triple the drift surface) and
rejected: the app itself ships in these three languages and the variants
already exist. The mitigation is the same rule the decision docs use: all
three update in the same PR, content-identical, English authoritative on
conflict. Screenshots: the English and Japanese READMEs share the English-UI
captures; the Korean README keeps its Korean-UI set. A third capture set for
Japanese was considered and rejected as maintenance weight without
commensurate value.

**Adjacent staleness found during the audit is fixed in the same PR.**
`CLAUDE.md` documents the same removed `--fetch` flag in its build commands;
correcting the README while leaving the contradicting line in `CLAUDE.md`
would just relocate the confusion, so that line is corrected here rather than
deferred.

## Risks

- **The safety section could be read as a ban-safety guarantee.** Mitigated by
  construction: it states only the mechanism (reads game-written files, no
  memory access, no injection), which is verifiable in the source, and closes
  with an explicit use-at-your-own-discretion note. It promises nothing on
  Battlestate Games' behalf.
- **The feature list will drift again as 1.1 phases ship.** Accepted: the
  restructure reduces the rot surface (no version claims, coarse-altitude
  feature list, no walkthroughs) rather than promising perpetual freshness,
  and each shipping phase naturally touches the README when it changes what a
  player sees.
- **Three language variants can still drift.** Accepted: the same-PR 1:1 rule
  has held for the decision docs, and English-wins resolves any conflict a
  reader finds.
- **Screenshots age with every UI change.** Accepted: they are illustrative,
  and the refresh establishes current-UI captures as the baseline.
