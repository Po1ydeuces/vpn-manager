# VPN Manager

A lightweight Windows tray utility for managing the built-in VPN connections. Click the tray icon to get a compact popup with all your VPN profiles, their statuses and live connection timers, then connect or disconnect with a single click, without digging through *Settings → Network → VPN*.

## Features

- Tray icon with a compact popup next to it; closes on focus loss or `Esc`.
- Lists all built-in Windows VPN profiles (IKEv2, L2TP, SSTP, PPTP), sorted by name.
- Pin favorite profiles to the top of the list (pin icon on row hover).
- Optional per-profile auto-reconnect: if a connection drops, it is re-dialed up to 5 times, 5 seconds apart. A manual click or turning the option off cancels it.
- Per-profile status (disconnected / connecting / connected / error) and a live `hh:mm:ss` connection timer.
- One-click connect and disconnect; clicking a profile that is still connecting cancels the attempt.
- Several VPNs can be dialed in parallel.
- Connection errors are shown in the popup and cleared automatically after 30 seconds.
- Tray icon reflects whether any VPN is active.
- Light and dark theme following the system setting.
- English and Russian UI. Follows the system language by default; can be switched from the tray menu.
- Autostart toggle, language selection and **Exit** in the tray icon's context menu.
- Pins, auto-reconnect and language are stored in `%LOCALAPPDATA%\VpnManager\settings.json`.
- Single instance only.
- Low footprint: no administrator rights needed, a few MB of memory when idle.
- Unhandled errors are written to `%LOCALAPPDATA%\VpnManager\error.log` instead of closing the app.

### Not included

Creating, editing or deleting profiles, WireGuard/OpenVPN/vendor clients, traffic statistics, an installer or auto-update.

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
│   ├── Tray/            # NotifyIcon, popup positioning
│   ├── Views/           # Popup window and list item
│   └── ViewModels/      # Connection list, timers, commands
├── VpnManager.Tests/    # xUnit tests
└── VpnManager.sln
```

VPN control goes straight through the Windows RAS API (`RasEnumEntries`, `RasEnumConnections`, `RasGetConnectStatus`, `RasGetConnectionStatistics`, `RasDial`, `RasHangUp`), with no PowerShell output parsing.
