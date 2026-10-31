# Dev only: builds + installs ModKit (from ..\dungeonmans-mod-toolkit if present) and the mod, backs up the saves,
# then launches the game with DevCapture (see DevCapture.cs). The game loads the first save, screenshots each
# scene into <game>\Mods\QoL\shots\ and quits without saving.
#   .\tools\capture.ps1 [-Scenes all|hud,coop_birds,...]
param(
	[string]$GameDir = "E:\SteamLibrary\steamapps\common\Dungeonmans",
	[string]$Scenes = "all",
	[int]$TimeoutSec = 240
)
$ErrorActionPreference = "Stop"
if (Get-Process -Name Dungeonmans -ErrorAction SilentlyContinue) { throw "GAME RUNNING: close Dungeonmans first." }

$kit = Join-Path $PSScriptRoot "..\..\dungeonmans-mod-toolkit\install.ps1"
if (Test-Path $kit) { & $kit -GameDir $GameDir | Out-Null }
& (Join-Path $PSScriptRoot "..\install.ps1") -GameDir $GameDir | Out-Null

$saves = Join-Path $env:APPDATA "Dungeonmans"
$backup = Join-Path $env:TEMP ("dungeonmans-save-backup-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
Copy-Item $saves $backup -Recurse
Write-Host "Saves backed up to $backup"

$mod = Join-Path $GameDir "Mods\QoL"
$shots = Join-Path $mod "shots"
if (Test-Path $shots) { Remove-Item $shots -Recurse -Force }
Set-Content (Join-Path $mod "devcapture.txt") ($Scenes -split ',') -Encoding ascii
try {
	$p = Start-Process (Join-Path $GameDir "Dungeonmans.exe") -WorkingDirectory $GameDir -PassThru
	if (-not $p.WaitForExit($TimeoutSec * 1000)) { Stop-Process -Id $p.Id -Force; Write-Host "TIMEOUT" }
}
finally {
	Remove-Item (Join-Path $mod "devcapture.txt") -ErrorAction SilentlyContinue
}
Get-Content (Join-Path $GameDir "ModKit\modkit.log") | Select-String "DevCapture|FAIL|threw"
if (Test-Path $shots) { Get-ChildItem $shots | ForEach-Object { $_.FullName } }
