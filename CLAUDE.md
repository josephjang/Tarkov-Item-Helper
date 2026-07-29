# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Environment

**This is a Windows development environment.** Use PowerShell commands instead of Bash/Unix commands.

## Build & Run Commands

```powershell
# Build entire solution
dotnet build TarkovHelper.sln

# Build specific project
dotnet build TarkovHelper/TarkovHelper.csproj

# Build Release
dotnet build TarkovHelper/TarkovHelper.csproj -c Release

# Run main application
dotnet run --project TarkovHelper/TarkovHelper.csproj

# Run with data fetch
dotnet run --project TarkovHelper/TarkovHelper.csproj -- --fetch
```

## Solution Structure

| Project | Description |
|---------|-------------|
| **TarkovHelper** | Main WPF application for tracking quests, hideouts, items |
| **TarkovDBEditor** | Database editor tool for managing tarkov_data.db (see `TarkovDBEditor/CLAUDE.md` for details) |
| **CheckDb** | Utility project (.NET 10) |

## Documentation & Decision Docs

Decisions are documented in `docs/PRDs/` at the repo root — the single location for
the whole solution (TarkovHelper, TarkovDBEditor, and cross-cutting work), kept at
root since documents routinely span projects. Two document types pair by filename:
`name.md` is a PRD (product decisions), `name.spec.md` is a spec (technical design).
The folder's name predates the split; see its `README.md` for the format.

**When a change needs a document** (write it on the work's branch — it merges in the
same PR as the work):

- A hard-to-reverse **product decision** → PRD (`name.md`)
- A non-obvious **technical decision** → spec (`name.spec.md`)
- Both → both files, sharing one name
- Neither (obvious bug fix, mechanical refactor) → no document; the PR body is enough
- Adding the sibling file mid-flight is expected — just add it.

Documents live flat in `docs/PRDs/`, are never moved, and are append-only: a
document on `main` is a finished decision record, and state (in flight / done /
dropped) belongs to GitHub PRs. Name the documents a PR implements in the PR body.
The only post-merge write: a change that reverses a recorded decision appends
`Superseded by <doc>` to the old document in that same PR. `archive/` holds frozen
legacy documents in their original format.

Pure reference/analysis docs (DB schemas, system analyses, log-format notes — anything
describing how the system currently works rather than planned work) live directly under
root `docs/` too. `TarkovDBEditor/docs/` is a separate, smaller location for that
project's own internal implementation notes (wiki-parsing quirks, test case tracking) —
not repo-wide, so it stays put.

## Architecture Overview

### Pattern: Singleton Services with Event-Driven Data Flow

```
UI Pages (WPF)
    ↓ Subscribe to events
Services (Singleton instances)
    ↓ Persist/load data
Databases (SQLite)
```

### Key Services

**Data Services:**
- `QuestDbService` - Loads quest data from tarkov_data.db
- `HideoutDbService` - Hideout module data
- `ItemDbService` - Item data management
- `UserDataDbService` - User persistence (progress, settings, inventory)

**Sync Services:**
- `LogSyncService` - Monitors EFT game logs, detects quest events (started/completed/failed)
- `EftRaidEventService` - Parses raid state (Idle/Matching/InRaid/Ended) from EFT logs
- `DatabaseUpdateService` - Auto-updates tarkov_data.db from GitHub

**Map Services:**
- `MapTrackerService` - Coordinate tracking for map visualization
- `MapCoordinateTransformer` - Game-to-screen coordinate conversion
- `OverlayMiniMapService` - Manages overlay minimap window

**Other:**
- `LocalizationService` - Multi-language support (EN, KO, JA)
- `QuestProgressService` - Quest progress state management
- `SettingsService` - User preferences persistence

### Database Architecture

**tarkov_data.db** (Asset database - auto-updated from GitHub):
- Quest, hideout, item, trader data from tarkov.dev API
- Read-only during runtime
- Located: `TarkovHelper/Assets/tarkov_data.db`

**user_data.db** (User persistence):
- Quest/hideout progress, item inventory, settings
- Located: `{AppDir}/Config/user_data.db` (next to the executable, e.g.
  `TarkovHelper/bin/Debug/net8.0-windows/Config/` for a Debug build — not `%LocalAppData%`)
- Because the path is relative to the executable, Debug builds, Release builds, and
  installed copies each have their **own separate user data**; the in-app "Data Migration"
  button (`ConfigMigrationService`) imports from another location's Config folder
- Location overridable via the `TARKOVHELPER_CONFIG_PATH` environment variable
  (used by e2e tests to isolate their data)

### UI Structure

- **Pages:** QuestListPage, HideoutPage, ItemsPage, CollectorPage, MapPage
- **Overlay:** OverlayMiniMapWindow (topmost window for in-game use)
- **Global keyboard hooks** for overlay control (requires admin rights)

## Data Flow Patterns

### Game Log Monitoring
```
EFT debug.log → LogSyncService (FileSystemWatcher)
  → EftRaidEventService (regex parsing)
  → QuestEventDetected event
  → QuestProgressService.Update()
  → UserDataDbService persistence
```

### Database Updates
```
DatabaseUpdateService (5-min timer)
  → Check GitHub version
  → Download if newer
  → DatabaseUpdated event
  → Services reload data
  → UI refreshes
```

## Key Patterns

### Dual-Key Quest Mapping
Quests tracked by both `Id` (tarkov.dev) and `NormalizedName` (wiki-legacy) for migration compatibility.

### Event-Driven Updates
Services emit events (ProgressChanged, DatabaseUpdated, DataRefreshed) that UI pages subscribe to for reactive updates.

## External APIs

- **tarkov.dev**: Quest, hideout, item data (embedded in tarkov_data.db)
- **GitHub**: Auto-updates for both app and database
- **EFT Game Logs**: Real-time quest/raid event monitoring

## Releases

This repo (josephjang/Tarkov-Item-Helper) releases independently of upstream
(Zeliper). Versions use CalVer `YYYY.M.N` (N = release counter within the
month, no fix/feature semantics), starting at v2026.7.0.

- Run `/release <version>` — it bumps the csproj, pushes the tag (never `git push
  --tags`), waits for `.github/workflows/release.yml` to build/test/package/publish
  `TarkovHelper.zip`, then bumps `update.xml` **last** so clients never see a 404 URL.
- Design rationale: `docs/PRDs/feature-fork-release-process.md`

## Framework & Dependencies

- **.NET 8.0 WPF** (Windows desktop)
- **Microsoft.Data.Sqlite** - SQLite database access
- **SharpVectors.Wpf** - SVG map rendering
- **Westermo.GraphX.Controls** - Graph visualization (quest dependencies)
- **AutoUpdater.NET** - Application self-updates

## Admin Rights

App requires administrator privileges (via app.manifest) for:
- Global keyboard hooks (overlay hotkeys work in-game)
- Game log file monitoring

## Localization

Multi-language support via `LocalizationService` partial classes:
- `LocalizationService.Core.cs` - Core strings
- `LocalizationService.Map.cs` - Map-specific
- `LocalizationService.Quest.cs` - Quest-specific

Supported languages: English (EN), Korean (KO), Japanese (JA)
