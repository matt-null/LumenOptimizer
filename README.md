<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="src/LumenOptimizer.UI/Assets/AlrightDev-forDark.png">
  <img src="src/LumenOptimizer.UI/Assets/AlrightDev-forLight.png" alt="Alright Dev" width="340">
</picture>

<br><br>

<img src="src/LumenOptimizer.UI/Assets/AppIcon.png" alt="" width="88">

# Lumen Optimizer

**A Windows 10 / 11 optimizer and debloater, written from scratch in C# and WinUI 3.**
Every change is detected, backed up, and reversible where Windows allows it.

[![Download](https://img.shields.io/github/v/release/matt-null/LumenOptimizer?label=download&style=for-the-badge&color=2f7dff)](https://github.com/matt-null/LumenOptimizer/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/matt-null/LumenOptimizer/total?style=for-the-badge&color=2f7dff)](https://github.com/matt-null/LumenOptimizer/releases)

![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078d4?style=flat-square&logo=windows11&logoColor=white)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512bd4?style=flat-square&logo=dotnet&logoColor=white)
![WinUI 3](https://img.shields.io/badge/WinUI-3-2f7dff?style=flat-square)
![x64](https://img.shields.io/badge/arch-x64-555?style=flat-square)
![Tweaks](https://img.shields.io/badge/tweaks-161-2f7dff?style=flat-square)

[Download](#download) · [What it does](#what-it-does) · [Tools](#tools) · [Build it yourself](#build-it-yourself) · [Add a tweak](#adding-a-tweak)

</div>

---

## Download

Grab **`LumenOptimizer-portable.zip`** from the [latest release](https://github.com/matt-null/LumenOptimizer/releases/latest), unzip it anywhere, and run **`LumenOptimizer.exe` as administrator**.

There is also `LumenOptimizer-Setup.exe` if you would rather install it.

The app checks this repository for new releases when it starts, shows you what changed, and only downloads when you say yes. You can also check manually from the About page.

> **Heads up:** the app is not code-signed yet, so SmartScreen will warn you the first time. Click *More info → Run anyway*. See [Code signing](#code-signing) for why.

---

## What it does

Lumen Optimizer is a step-by-step walkthrough, not a wall of checkboxes:

| Step | What happens |
| --- | --- |
| **1. Splash** | Scans your PC and detects which tweaks are already applied. |
| **2. Nothing preselected** | No presets. Each page has search, filters and a **Select what fits this PC** button. Rows that suit your hardware (*reduces writes on an SD card*) are marked and listed first. |
| **3. One page per area** | Privacy, Microsoft Edge, Performance, Debloat, Services, Appearance, Advanced (risky). Tick or untick anything. |
| **4. Review** | See everything you picked, with warnings. Save the selection to a file and load it on another PC. Risky tweaks need an extra *I understand the risks* tick. |
| **5. Apply** | Creates a restore point, applies each change, shows progress. |
| **6. Done** | Results, with a Restart button if one is needed. |

**Undo previous changes** on the splash screen puts the original values back for anything Lumen Optimizer changed.

### Live console

After the scan the app offers a **live console**. It streams every command, registry / service / task change, result and error as it happens, with Copy, Save log and Clear. The **Console** button in the bottom bar toggles it at any time.

### It knows what it is running on

The scan detects the Windows build and edition, laptop vs desktop, SSD vs HDD vs flash, and the GPU vendor. Tweaks can be limited by `minBuild` / `maxBuild` and by hardware with `requires`. Anything listed in `avoidOn` (SysMain on a hard disk, Ultimate Performance on a laptop) shows a warning and is never auto-selected. Windows 10 gets an end-of-support notice. Tweaks marked `unverified` say so on the row.

---

## Tools

Reachable from the splash screen. Every action shows what it will do and asks first.

| Tool | What it does |
| --- | --- |
| **Cleaner** | Temp files, update and browser caches, crash dumps, Recycle Bin, superseded update files, Windows.old. Scan first, then choose. |
| **Devices and USB** | Removes records Windows keeps for hardware that is gone: ghost devices, old USB drives in Explorer, drive letters still reserved. Registry removals are backed up and restorable. |
| **Memory** | Real figures (in use, available, standby cache, commit) and the three operations Windows exposes. It says plainly when these are *not* a speed-up. |
| **Startup manager** | What starts with Windows, switched off or back on. Nothing is deleted. |
| **Services** | Change how a service starts, or start and stop it. The original start type is saved so **Restore original** always works. Core Windows services are protected. |
| **Scheduled tasks** | See, disable, enable, run or delete the tasks Windows and programs run on a schedule. Built-in tasks can only be disabled, which is reversible. |
| **Optimize drives** | Defragments hard disks and TRIMs SSDs using Windows' own optimizer. USB sticks and SD cards are skipped on purpose, because defragging flash only wears it out. |
| **USB / SD cleaner** | Removes `Thumbs.db`, `.DS_Store`, `autorun.inf`, Mac trash folders and similar junk from removable drives. Offers itself when you plug a drive in. Your own files are never touched. |
| **Force delete** | For files and folders that will not go: takes ownership, grants access, ends the programs holding them, retries as SYSTEM, and as a last resort removes them at the next restart. |
| **Local accounts** | Reset a forgotten local password, unlock a locked-out account, enable or disable one, manage administrator rights. |
| **Uninstaller** | Search installed programs and run their own uninstaller. |
| **Hosts editor** | Edit the hosts file with validation and a backup before every save. Optional telemetry block list. |
| **Integrator** | Handy right-click menu entries such as *Open PowerShell here*. Per-user and fully removable. |
| **Network tools** | Flush DNS, renew your IP, reset Winsock and TCP/IP, ping, trace route, switch DNS provider with an undo. |

### What can be undone

| Reversible | One-way (always previewed first) |
| --- | --- |
| Tweaks (original values saved), Startup, Services (start type), Scheduled tasks, Devices (registry backups), Integrator, Hosts (timestamped backups), DNS | Cleaner, Uninstaller, Force delete, removed Store apps, ghost device removals |

### Safety rails

- Requires administrator, and says so loudly if it is not running elevated.
- A restore point is created before tweaks are applied.
- The Windows folder, whole drives, your user profile and the app itself cannot be force-deleted.
- Core Windows services cannot be disabled or stopped.
- The last active administrator account cannot have its rights removed.
- Passwords you type are never written to the log.

---

## Build it yourself

### One-time setup

1. Install **[Visual Studio 2022 Community](https://visualstudio.microsoft.com/downloads/)** (free).
2. Tick the **.NET desktop development** workload. Under *Individual components*, also tick **Windows App SDK C# Templates** and a **Windows 11 SDK (10.0.22621 or newer)**.
3. Turn on **Developer Mode**: *Settings → System → For developers*.
4. Optional, for the installer: **[Inno Setup 6](https://jrsoftware.org/isdl.php)**.

### Run from Visual Studio

Right-click Visual Studio → **Run as administrator**, open `LumenOptimizer.sln`, set **Debug | x64** with **LumenOptimizer.UI** as the startup project, and press **F5**. Visual Studio has to be elevated to debug an app that edits HKLM, services and tasks.

### Build the portable app and installer

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output lands in `dist\`:

- `portable\LumenOptimizer.exe` and its files, plus `LumenOptimizer-portable.zip`
- `LumenOptimizer-Setup.exe`, if Inno Setup 6 is installed

A *single* .exe is not practical for WinUI 3, which ships native libraries, so "portable" means a self-contained folder or zip.

---

## How it is organised

```
src/LumenOptimizer.Core/      Engine, no UI. Compiles on any OS with the .NET 8 SDK.
  Catalog/tweaks.json         Every tweak, as data
  Actions/                    One handler per action type (registry, service, task, appx, powerplan, command)
  Tools/                      Cleaner, DeviceCleanup, Memory, Startup, Services, ScheduledTasks,
                              Accounts, ForceDelete, DiskOptimizer, RemovableCleaner, Uninstaller,
                              HostsFile, ContextMenu, NetworkTools
  Engine/                     TweakEngine, BackupStore, CatalogLoader, RestorePointService
  Update/                     Release check, download and swap
src/LumenOptimizer.UI/        WinUI 3 app (unpackaged, self-contained, requires admin)
  MainWindow                  Title bar, page frame, Back / Next footer
  Pages/                      Splash, Tweaks, Review, Apply, Done, Undo, Tools, About, ManagerPage
  ConsolePanel                Live console docked above the footer
  WizardState.cs              Shared selection state across the pages
build.ps1                     Portable zip + installer
installer/                    Inno Setup script
```

### Adding a tweak

Add an entry to `src/LumenOptimizer.Core/Catalog/tweaks.json`:

```json
{
  "id": "privacy-example",
  "name": "Turn off something",
  "description": "What it does, in one sentence.",
  "category": "Privacy",
  "risk": "Safe",
  "actions": [
    { "type": "registry", "hive": "HKCU", "path": "Software\\Example", "name": "Enabled", "kind": "DWord", "value": "0" }
  ]
}
```

- `risk`: `Safe`, `Moderate` or `Aggressive`. Aggressive tweaks **must** have a `warning`.
- `minBuild` / `maxBuild`: hide it outside a Windows range (22000 = Windows 11, 26100 = 24H2/25H2, maxBuild 21999 = Windows 10 only).
- `requires`: hardware tags the PC must have — `laptop`, `desktop`, `ssd`, `hdd`, `flash`, `nvidia`, `amd-gpu`, `intel-gpu`.
- `avoidOn` + `avoidNote`: warn and never auto-select on those PCs. `unverified`: shown to the user.
- `fitsOn` + `fitNote`: where the tweak helps most. Adds a green hint and feeds the *fits* filter.
- Action types: `registry`, `service`, `task`, `appx`, `powerplan`, `command`. Fields are in `Models/Tweak.cs`.
- New category? Add its name to `WizardState.Categories` and a title in `TweaksPage.xaml.cs`.
- Run the app once: catalog problems (duplicate ids, missing fields) are written to the log.

### Where things are stored

| | |
| --- | --- |
| Original values for undo | `%ProgramData%\LumenOptimizer\backups.json` |
| Service start types | `%ProgramData%\LumenOptimizer\service-backups.json` |
| Device registry backups | `%ProgramData%\LumenOptimizer\device-backups\` |
| Logs | `%ProgramData%\LumenOptimizer\logs\` |

---

## Code signing

- Free trusted certificates for closed-source software do not exist. SignPath Foundation signs **open-source** projects for free.
- Azure Artifact Signing (formerly Trusted Signing) is cheap, roughly $10 a month, if your country and identity qualify.
- A self-signed certificate only works on PCs where you install it.
- Even signed, a new optimizer draws SmartScreen and antivirus warnings until it builds reputation, because it edits the registry and services.

## Known limits

- Per-user (HKCU) tweaks apply to the account the app runs as. If a standard user elevates with a *different* admin account, HKCU belongs to that admin account.
- Removed Store apps and some commands cannot be undone automatically. They are labelled *no automatic undo*.
- Defender and Windows Update tweaks are fragile on current Windows: Tamper Protection and servicing can re-enable them. They are marked Aggressive and say so.
- Power plan detection compares English display names as a fallback; the active plan is still matched by GUID on other languages.
- Clearing USB storage history needs TrustedInstaller rights, which the app does not take. That list is read-only.
- Tweaks are lightly tested so far. Try them in a VM or on a spare PC first.

## Credits and licenses

The app icon uses the **zap** glyph from [Feather](https://feathericons.com) by Cole Bemis (MIT) and [Lucide](https://lucide.dev), its community fork (ISC). Their license texts are in `THIRD-PARTY-FEATHER-LICENSE.txt` and `THIRD-PARTY-LUCIDE-LICENSE.txt`. The icon is set explicitly in `MainWindow` because unpackaged WinUI 3 apps show no taskbar icon otherwise.

Feature ideas were inspired by [Optimizer](https://github.com/hellzerg/optimizer) by hellzerg. No code was used; this is a clean rewrite, so it is not bound to any other project's license.

Project license: **not chosen yet.**

---

<div align="center">

**Built by Alright Dev**

[alrightdev.com](https://alrightdev.com) · [hello@alrightdev.com](mailto:hello@alrightdev.com) · +1 (541) 876-7873

</div>
