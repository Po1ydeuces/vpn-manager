# VPN Manager

A lightweight Windows tray utility for managing the built-in VPN connections. Click the tray icon to get a compact popup with all your VPN profiles, their statuses and live connection timers, then connect or disconnect with a single click, without digging through *Settings → Network → VPN*.

## Features

- Tray icon with a compact popup next to it; closes on focus loss or `Esc`.
- Lists all built-in Windows VPN profiles (IKEv2, L2TP, SSTP, PPTP), sorted by name.
- Row icons (shown on hover): pin to top, connect on app start, auto-reconnect. Right-click a profile to hide it from the list; hidden profiles are restored from the tray menu (**Hidden profiles**).
- Optional per-profile connect on app start: flagged profiles are dialed right after the app launches.
- Optional per-profile auto-reconnect: if a connection drops, it is re-dialed up to 5 times, 5 seconds apart. A manual click or turning the option off cancels it.
- Per-profile state shown by the colored dot (grey = disconnected, yellow = connecting, green = connected, red = error), a live `hh:mm:ss` connection timer and the current transfer speed; hover the speed line for total bytes received / sent.
- One-click connect and disconnect; clicking a profile that is still connecting cancels the attempt.
- Several VPNs can be dialed in parallel.
- Connection errors are shown in the popup and cleared automatically after 30 seconds.
- Tray icon reflects whether any VPN is active; its tooltip lists the connected profiles.
- Balloon notifications when a connection drops, fails, or auto-reconnect gives up (only while the popup is closed; can be turned off).
- Light and dark theme: follows the system by default, or pick Light / Dark in the tray menu.
- Connection log with connect, disconnect, drop (with session length) and failure entries; open it from the tray menu.
- English and Russian UI. Follows the system language by default; can be switched from the tray menu.
- Global `Ctrl+Alt+V` hotkey toggles the popup (can be turned off in the tray menu; it is skipped if another app already uses it).
- Tray icon's context menu: autostart with Windows, language, hotkey, hidden profiles, notifications, theme, connection log and **Exit**.
- Pins, hidden profiles, per-profile options, hotkey, notifications, theme and language are stored in `%LOCALAPPDATA%\VpnManager\settings.json`. The connection log is `%LOCALAPPDATA%\VpnManager\connections.log` (trimmed to the last 2000 lines when it grows over 1 MB).
- Single instance only.
- Low footprint: no administrator rights needed, a few MB of memory when idle.
- Unhandled errors are written to `%LOCALAPPDATA%\VpnManager\error.log` instead of closing the app.

### Not included

Creating, editing or deleting profiles, WireGuard/OpenVPN/vendor clients, traffic history or graphs, an installer or auto-update.

## Requirements

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build from source

VPN profiles must already exist in Windows and have saved credentials. If a profile needs a password that is not saved, the app shows an error instead of prompting for it.

## Build and run

```powershell
git clone <repo-url>
cd vpn-manager/src
dotnet build VpnManager.sln
dotnet run --project VpnManager
```

Open `src/VpnManager.sln` in Rider or Visual Studio.

## Tests

```powershell
dotnet test src/VpnManager.sln
```

## Publish

Produces a self-contained single-file executable:

```powershell
dotnet publish src/VpnManager/VpnManager.csproj -c Release -r win-x64 --self-contained -o publish
```

The result is `publish/VpnManager.exe`.

## Project structure

```
src/
├── VpnManager/          # WPF app (.NET 10)
│   ├── Ras/             # P/Invoke wrappers for rasapi32.dll and RasService
│   ├── Settings/        # JSON settings store
│   ├── Logging/         # Connection log
│   ├── Localization/    # EN/RU strings
│   ├── Tray/            # NotifyIcon, popup positioning, global hotkey
│   ├── Views/           # Popup window and list item
│   └── ViewModels/      # Connection list, timers, commands
├── VpnManager.Tests/    # xUnit tests
└── VpnManager.sln
```

VPN control goes straight through the Windows RAS API (`RasEnumEntries`, `RasEnumConnections`, `RasGetConnectStatus`, `RasGetConnectionStatistics`, `RasDial`, `RasHangUp`), with no PowerShell output parsing.
