# Developer install: builds the QoL mod and copies it into <game>\Mods\QoL.
# Needs Dungeonmans ModKit installed in the game first (https://github.com/chibranche/dungeonmans-mod-toolkit).
param([string]$GameDir = "E:\SteamLibrary\steamapps\common\Dungeonmans")
$ErrorActionPreference = "Stop"
if (-not (Test-Path (Join-Path $GameDir "ModKit\DungeonmansModKit.dll"))) { throw "ModKit isn't installed in $GameDir. Install dungeonmans-mod-toolkit first." }
if (Get-Process -Name Dungeonmans -ErrorAction SilentlyContinue) { throw "GAME RUNNING: close Dungeonmans first." }

dotnet build "$PSScriptRoot\DungeonmansQoL.csproj" -c Release -p:GameDir="$GameDir" -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$out = Join-Path $PSScriptRoot "bin\Release\net8.0"
$dest = Join-Path $GameDir "Mods\QoL"
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $out "DungeonmansQoL.dll"), (Join-Path $out "mod.json") $dest -Force
Write-Host "Installed to $dest. Log: $GameDir\ModKit\modkit.log"
