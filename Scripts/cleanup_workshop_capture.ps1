# Stop only the isolated screenshot session and remove only its owned font-pack copy.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$sessionPath = Join-Path $repo '.task/workshop-screenshots/session.json'
if (-not (Test-Path -LiteralPath $sessionPath)) { return }
$session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
$profile = [IO.Path]::GetFullPath((Join-Path $repo '.task/workshop-screenshots/profile'))
if ([IO.Path]::GetFullPath($session.profile) -ne $profile) { throw 'Unexpected screenshot profile.' }
$exe = [IO.Path]::GetFullPath((Join-Path $session.game 'RimWorld.exe'))
$proc = Get-CimInstance Win32_Process -Filter "ProcessId = $($session.pid)"
if ($proc) {
    if ($proc.ExecutablePath -ne $exe -or -not $proc.CommandLine.Contains($profile)) {
        throw 'Session PID belongs to a different process. Nothing was stopped.'
    }
    Stop-Process -Id $session.pid
    Get-Process -Id $session.pid -ErrorAction SilentlyContinue | Wait-Process -Timeout 20 -ErrorAction SilentlyContinue
}
$expected = [IO.Path]::GetFullPath((Join-Path $session.game 'Mods/CustomFontsScreenshotExample'))
$recorded = [IO.Path]::GetFullPath($session.temporary_pack)
if ($recorded -ne $expected) { throw 'Unexpected fixture path. Nothing was removed.' }
if (Test-Path -LiteralPath $expected) {
    $marker = Join-Path $expected '.customfonts-capture-fixture'
    if (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne 'rw-customfonts-workshop-capture') {
        throw 'Missing ownership marker. Nothing was removed.'
    }
    Remove-Item -LiteralPath $expected -Recurse -Force
}
Write-Output 'Isolated screenshot session stopped; owned example pack removed. Personal profiles were not changed.'
