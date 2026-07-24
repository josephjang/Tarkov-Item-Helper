#Requires -Version 5.1
<#
.SYNOPSIS
Builds the release package artifacts/TarkovHelper.zip.

.DESCRIPTION
Publishes TarkovHelper as a framework-dependent app (requires the .NET 8
Desktop Runtime on the target machine) into a clean staging directory and
zips it. The zip root is the app root: AutoUpdater.NET extracts the archive
directly over the install directory, so TarkovHelper.exe and Assets\ must
sit at the top level of the archive.

Used both locally and by .github/workflows/release.yml.

.PARAMETER Configuration
Build configuration to publish. Defaults to Release.

.PARAMETER NoBuild
Skip compilation and reuse existing build output (pass --no-build to
dotnet publish). The release workflow builds the solution once before
packaging, so it sets this to avoid a redundant second compile; a local
standalone run leaves it off so publish builds from source.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'

$repoRoot  = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project   = Join-Path $repoRoot 'TarkovHelper\TarkovHelper.csproj'
$artifacts = Join-Path $repoRoot 'artifacts'
$stageDir  = Join-Path $artifacts 'publish'
$zipPath   = Join-Path $artifacts 'TarkovHelper.zip'

# Always publish into a clean staging dir; the incremental bin\...\publish
# folder can carry files deleted from the project between builds.
if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Force $artifacts | Out-Null

# Framework-dependent, no RID: matches how the app has always shipped and
# keeps the zip small for AutoUpdater's in-place update.
$publishArgs = @($project, '-c', $Configuration, '--no-self-contained', '-o', $stageDir)
if ($NoBuild) { $publishArgs += '--no-build' }
dotnet publish @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

# A zero exit with no exe means a broken publish; shipping it would brick every
# client that auto-updates into the hollow zip. Fail loudly instead.
$exePath = Join-Path $stageDir 'TarkovHelper.exe'
if (-not (Test-Path $exePath)) {
    throw "Publish produced no TarkovHelper.exe in $stageDir; refusing to package."
}

# Framework-dependent publish bundles Microsoft.Data.Sqlite's native e_sqlite3
# for every RID SQLitePCLRaw ships (linux/osx/maccatalyst/browser-wasm, ~19 MB).
# This is a Windows-only WPF app, so drop the unreachable native runtimes to
# shrink every release download and auto-update.
$runtimesDir = Join-Path $stageDir 'runtimes'
if (Test-Path $runtimesDir) {
    Get-ChildItem $runtimesDir -Directory -Force |
        Where-Object { $_.Name -notlike 'win*' } |
        Remove-Item -Recurse -Force
}

# Ship no debug symbols (-Force so a hidden/system-attributed .pdb can't slip past)
Get-ChildItem $stageDir -Recurse -Filter *.pdb -Force | Remove-Item -Force

# Create entries with explicit forward-slash names: on PowerShell 5.1 both
# Compress-Archive and .NET Framework's ZipFile.CreateFromDirectory write
# backslash separators (zip-spec violation, mishandled by non-Windows
# extractors), and CI's pwsh would produce a different archive. Writing the
# entries ourselves keeps local and CI zips identical and spec-correct.
# -Force on the enumeration so no staged file is silently omitted from the zip.
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stagePrefix = (Get-Item $stageDir).FullName.TrimEnd('\') + '\'
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stageDir -Recurse -File -Force) {
        $entryName = $file.FullName.Substring($stagePrefix.Length).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName)
    }
}
finally {
    $zip.Dispose()
}

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "Created $zipPath ($sizeMb MB)"
