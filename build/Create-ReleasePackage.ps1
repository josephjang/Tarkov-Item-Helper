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
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
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
dotnet publish $project -c $Configuration --no-self-contained -o $stageDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

# Ship no debug symbols
Get-ChildItem $stageDir -Recurse -Filter *.pdb | Remove-Item -Force

# Create entries with explicit forward-slash names: on PowerShell 5.1 both
# Compress-Archive and .NET Framework's ZipFile.CreateFromDirectory write
# backslash separators (zip-spec violation, mishandled by non-Windows
# extractors), and CI's pwsh would produce a different archive. Writing the
# entries ourselves keeps local and CI zips identical and spec-correct.
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stagePrefix = (Get-Item $stageDir).FullName.TrimEnd('\') + '\'
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stageDir -Recurse -File) {
        $entryName = $file.FullName.Substring($stagePrefix.Length).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName)
    }
}
finally {
    $zip.Dispose()
}

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "Created $zipPath ($sizeMb MB)"
