# Intentionality

A Windows 11 focus lock that starts at logon, covers the screen, turns the internet off, and asks what you intend to work on before unlocking the PC for a timed session.

## Requirements

- Windows 11
- .NET 8 SDK (to build)
- Administrator rights (network adapter control and elevated startup task)

## Build and publish

```powershell
dotnet test
dotnet publish src\Intentionality\Intentionality.csproj -c Release -o publish
```

## Install at logon

Run PowerShell **as Administrator**:

```powershell
.\scripts\Install-StartupTask.ps1
```

This publishes if needed and registers an elevated scheduled task named `IntentionalityFocusLock` for your user at logon (no UAC prompt each time).

Remove it with:

```powershell
.\scripts\Uninstall-StartupTask.ps1
```

## Emergency network restore

If you end the app from Ctrl+Alt+Del → Task Manager while the internet is still off:

```powershell
.\scripts\Restore-Network.ps1
```

Ctrl+Alt+Del cannot be blocked by this app and is the intentional escape hatch.

## Log

Session answers are appended to [`logs/intentionality.md`](logs/intentionality.md). Hot timer state lives in `%LOCALAPPDATA%\Intentionality\`.

## Flow

1. Lock screens and sample location  
2. Disable network adapters  
3. Ask: what you are working on → how much time → session goal  
4. Unlock and wait (tray countdown)  
5. When time is up: lock again and ask if the goal was completed  
   - **Yes** → mandatory 5-minute break → start over  
   - **No** → ambition scale + optional “I got distracted” → new time → unlock again  
