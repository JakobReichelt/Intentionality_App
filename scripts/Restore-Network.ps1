#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Re-enables network adapters that Intentionality disabled.
.DESCRIPTION
  Reads %LOCALAPPDATA%\Intentionality\network-state.json and enables each saved adapter.
  Use this after ending Intentionality from Ctrl+Alt+Del / Task Manager.
#>
[CmdletBinding()]
param(
    [string]$StatePath = $(Join-Path $env:LOCALAPPDATA "Intentionality\network-state.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $StatePath)) {
    Write-Host "No network state file found at $StatePath"
    Write-Host "Attempting to enable all disabled NetAdapters..."
    Get-NetAdapter | Where-Object { $_.Status -eq 'Disabled' } | ForEach-Object {
        Write-Host "Enabling $($_.Name)..."
        Enable-NetAdapter -Name $_.Name -Confirm:$false
    }
    exit 0
}

$doc = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
if (-not $doc.adapters) {
    Write-Error "Invalid network state file."
}

foreach ($adapter in $doc.adapters) {
    try {
        if ($adapter.interfaceIndex -and $adapter.interfaceIndex -gt 0) {
            Write-Host "Enabling interface index $($adapter.interfaceIndex) ($($adapter.name))..."
            Enable-NetAdapter -InterfaceIndex $adapter.interfaceIndex -Confirm:$false
        }
        elseif ($adapter.name) {
            Write-Host "Enabling $($adapter.name)..."
            Enable-NetAdapter -Name $adapter.name -Confirm:$false
        }
    }
    catch {
        Write-Warning "Failed to enable $($adapter.name): $_"
    }
}

Write-Host "Done."
