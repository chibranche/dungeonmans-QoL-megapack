# Builds dist\DungeonmansQoL-<v>-with-ModKit.zip: the ModKit release zip plus Mods\QoL, ready for players
# (extract into the game folder, run ModKit-Install.bat).
#   -ModKitZip  the toolkit's release zip (default: newest in ..\dungeonmans-mod-toolkit\dist)
param(
	[string]$GameDir = "E:\SteamLibrary\steamapps\common\Dungeonmans",
	[string]$ModKitZip
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $ModKitZip) {
	$ModKitZip = Get-ChildItem (Join-Path $root "..\dungeonmans-mod-toolkit\dist") -Filter "DungeonmansModKit-*.zip" -ErrorAction SilentlyContinue |
		Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $ModKitZip -or -not (Test-Path $ModKitZip)) { throw "ModKit zip not found: run build-release.ps1 in dungeonmans-mod-toolkit, or pass -ModKitZip." }

dotnet build "$root\DungeonmansQoL.csproj" -c Release -p:GameDir="$GameDir" -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$version = (Get-Content (Join-Path $root "mod.json") -Raw | ConvertFrom-Json).version
$dist = Join-Path $root "dist"
$stage = Join-Path $dist "stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
Expand-Archive $ModKitZip $stage
$qol = Join-Path $stage "Mods\QoL"
New-Item -ItemType Directory -Force $qol | Out-Null
$out = Join-Path $root "bin\Release\net8.0"
Copy-Item (Join-Path $out "DungeonmansQoL.dll"), (Join-Path $out "mod.json"), (Join-Path $root "README.md"), (Join-Path $root "LICENSE") $qol

if (Test-Path (Join-Path $root "docs")) { Copy-Item (Join-Path $root "docs") $qol -Recurse }

$zip = Join-Path $dist "DungeonmansQoL-$version-with-ModKit.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Remove-Item $stage -Recurse -Force
Write-Host "-> $zip (ModKit from $(Split-Path $ModKitZip -Leaf))"
