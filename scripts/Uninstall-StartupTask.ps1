#Requires -RunAsAdministrator
[CmdletBinding()]
param()

$taskName = "IntentionalityFocusLock"
$existing = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Task '$taskName' is not registered."
    exit 0
}

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
Write-Host "Scheduled task '$taskName' removed."
