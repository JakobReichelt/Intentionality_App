#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Registers Intentionality to start elevated at user logon (no UAC prompt).
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = $(Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$publishDir = Join-Path $RepoRoot "publish"
$exePath = Join-Path $publishDir "Intentionality.exe"

if (-not (Test-Path -LiteralPath $exePath)) {
    Write-Host "Published exe not found. Publishing now..."
    Push-Location $RepoRoot
    try {
        dotnet publish "src\Intentionality\Intentionality.csproj" -c Release -o $publishDir --self-contained false
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Could not find $exePath after publish."
}

$taskName = "IntentionalityFocusLock"
$existing = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existing) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$action = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $publishDir
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -RunLevel Highest -LogonType Interactive
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::Zero)

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description "Starts Intentionality focus lock at logon" | Out-Null

Write-Host "Scheduled task '$taskName' registered."
Write-Host "Executable: $exePath"
Write-Host "It will start at the next logon (or run it now from Start)."
