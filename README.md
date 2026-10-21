# Intentionality

A Windows 11 focus lock that starts at logon, covers the screen, and asks what you intend to work on before unlocking the PC for a timed session.

## Requirements

- Windows 11
- .NET 8 SDK (to build)
- Administrator rights (the lock runs elevated so it stays above elevated windows; the startup task avoids a UAC prompt)

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

## Escape hatch

Ctrl+Alt+Del cannot be blocked by this app and is the intentional escape hatch.

## Log

Session answers and errors are appended to [`logs/intentionality.md`](logs/intentionality.md). Hot timer state lives in `%LOCALAPPDATA%\Intentionality\state.json`; a corrupt state file is kept as `state.corrupt.json`.

## Flow

1. Lock screens and sample location  
2. Ask: what you are working on → session goal → how much time  
3. Unlock and wait (tray countdown)  
4. When time is up: lock again and ask if the goal was completed  
   - **Yes** → mandatory 5-minute break → start over  
   - **No** → ambition scale → if the timer ran out on a block of 15 minutes or more, mandatory 2-minute break → new time → unlock again (shorter blocks or early end skip the break)  

## Code layout

- `Core/` — session state machine (`SessionController`) and its data; no UI, fully unit-tested
- `Services/` — side effects: lock overlay, tray, location, state file, markdown log
- `UI/` — lock window and the individual question steps
- `AppHost.cs` — lifecycle (startup, timer, power events); `AppHost.Flow.cs` — which step is shown per phase
