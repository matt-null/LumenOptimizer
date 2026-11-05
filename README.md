# Lumen Optimizer

A Windows 11 optimizer and debloater, written from scratch in C# / WinUI 3. Every change is detected, backed up, and reversible where Windows allows it.

> Built by Alright Dev.
> License: not chosen yet. This is a clean rewrite, so it is not bound to any other project's license.

## How the app works

Lumen Optimizer is a step-by-step walkthrough:

1. **Splash** - scans your PC and detects which tweaks are already applied.
2. **No presets** - nothing is preselected. Each page has search, filters, and a "Select what fits this PC" button; rows that suit your hardware (for example reduces writes on an SD card) are marked and listed first.
3. **One page per area** - Privacy, Microsoft Edge, Performance, Debloat, Services, Appearance, Advanced (risky). Tick or untick anything.
4. **Review** - save the selection to a file (and load it on another PC from the splash screen) - everything you selected, with warnings. Risky tweaks need an extra "I understand the risks" tick.
5. **Apply** - creates a restore point, applies each change, shows progress.
6. **Done** - results, with a Restart button if needed.

From the splash screen, **Undo previous changes** puts original values back for anything Lumen Optimizer changed.

### Console
After the scan the app asks whether to show the **live console**. It streams every command, registry / service / task change, result and error as it happens, and has Copy, Save log and Clear. The **Console** button in the bottom bar toggles it at any time.

### Windows 10 and 11 awareness
The scan detects the Windows build and edition, laptop vs desktop, SSD vs HDD (system drive) and GPU vendor. Tweaks can be limited with `minBuild` / `maxBuild` (Windows 10 only, Windows 11 24H2+ only) and `requires` (for example `nvidia`, `ssd`). Tweaks with `avoidOn` (for example SysMain on an HDD, Ultimate Performance on a laptop) show a warning and are never preselected by a preset. Windows 10 shows an end-of-support notice on the splash screen. Tweaks marked `unverified` are documented policies that could not be confirmed on every build; the row says so.

### Tools (from the splash screen)
Cleaner, Devices and USB, Memory, Startup manager, Uninstaller, Hosts editor, Integrator (right-click menu) and Network tools. Every action shows what it will do and asks first. **Devices and USB** removes the records Windows keeps for hardware that is gone: ghost devices (via `pnputil /remove-device`), old USB drives in Explorer's MountPoints2 and Portable Devices, and drive letters still reserved in MountedDevices. Registry removals are backed up to `%ProgramData%\LumenOptimizer\device-backups` and restorable. It also lists USB storage history read-only (clearing that key needs TrustedInstaller rights, which the app does not take).

**Memory** shows real figures (in use, available, standby cache, commit) and the three operations Windows exposes: empty standby list, flush modified pages, trim working sets. The page says plainly that these are not a speed-up, and warns when Windows runs from flash.

Undo: Startup (switch back on), Devices (registry backups; ghost removals have no undo but reinstall on reconnect), Integrator (switch off, per-user only), Hosts (timestamped backups), DNS (original saved, one-click restore). Cleaner deletes temp/cache files for good and the Uninstaller runs each program's own uninstaller; neither can be undone, so both preview first.

## 1. One-time setup on your PC

1. Install **Visual Studio 2022 Community** (free): https://visualstudio.microsoft.com/downloads/
2. In the installer, tick the **.NET desktop development** workload.
   Under *Individual components*, also tick **Windows App SDK C# Templates** (and **Windows 11 SDK 10.0.22621 or newer** if it isn't already ticked).
3. Turn on **Developer Mode**: Settings > System > For developers.
4. Optional, for the installer: **Inno Setup 6** (free): https://jrsoftware.org/isdl.php

## 2. Run it from Visual Studio

1. Unzip this folder somewhere simple, for example `C:\dev\LumenOptimizer`.
2. **Right-click Visual Studio > Run as administrator**, then open `LumenOptimizer.sln`. The app asks for admin rights (it edits HKLM, services and tasks), and Visual Studio must be elevated to debug it.
3. At the top, set the configuration to **Debug | x64** and the startup project to **LumenOptimizer.UI**.
4. Press **F5**. The first build restores NuGet packages and takes a few minutes.

If the build shows errors, copy the **Error List** text back to me and I'll fix them. This project was written without a Windows machine to compile the WinUI part on. The engine library (`LumenOptimizer.Core`) was compiled and its catalog validated.

## 3. Build the portable app and installer

In PowerShell, in this folder:

```powershell
.\build.ps1
```

Output in `dist\`:
- `portable\LumenOptimizer.exe` plus its files, and `LumenOptimizer-portable.zip` (unzip anywhere and run)
- `LumenOptimizer-Setup.exe`, if Inno Setup 6 is installed

A *single* .exe file is not practical for WinUI 3 (it ships native libraries), so "portable" means a self-contained folder or zip.

## 4. Code signing (free options)

- Free trusted certificates for closed-source software do not exist. SignPath Foundation signs **open-source** projects for free.
- Azure Artifact Signing (formerly Trusted Signing) is cheap, roughly $10 a month, if your country and identity qualify.
- A self-signed certificate works only on PCs where you install it.
- Even signed, new optimizers get SmartScreen and antivirus warnings until they build reputation, because they edit the registry and services.

## How it is organised

```
src/LumenOptimizer.Core/   Engine, no UI. Compiles on any OS with the .NET 8 SDK.
  Catalog/tweaks.json   Every tweak, as data
  Actions/              One handler per action type (registry, service, task, appx, powerplan, command)
  Tools/                Cleaner, DeviceCleanup, Memory, Startup, Uninstaller, HostsFile, ContextMenu, NetworkTools (no UI)
  Engine/               TweakEngine, BackupStore, CatalogLoader (+ Validate), RestorePointService
src/LumenOptimizer.UI/     WinUI 3 app (unpackaged, self-contained, requires admin)
  MainWindow            Title bar, page frame, Back / Next footer
  Pages/                Splash, Tweaks (one per category), Review, Apply, Done, Undo, Tools + one page per tool
  ConsolePanel          Live console docked above the footer
  WizardState.cs        Shared selection state across the pages
build.ps1               Portable zip + installer
installer/              Inno Setup script
```

### Adding a tweak

Add an entry to `src/LumenOptimizer.Core/Catalog/tweaks.json`. Example:

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
- `minBuild` / `maxBuild`: hide it outside a Windows range (22000 = Windows 11, 26100 = 24H2/25H2; maxBuild 21999 = Windows 10 only).
- `requires`: hardware tags the PC must have (`laptop`, `desktop`, `ssd`, `hdd`, `nvidia`, `amd-gpu`, `intel-gpu`). `avoidOn` + `avoidNote`: warn and never preselect on those PCs. `unverified`: shown to the user.
- The catalog is generated by `tools/gen_catalog.py` (`python tools/gen_catalog.py src/LumenOptimizer.Core/Catalog/tweaks.json`); you can also edit the JSON directly.
- Action types: `registry`, `service`, `task`, `appx`, `powerplan`, `command` (see `Models/Tweak.cs` for the fields).
- `fitsOn` + `fitNote`: hardware tags where the tweak helps most (adds a green hint and the "fits" filter). Tags also include `flash` (SD / USB / eMMC system drive).
- New category? Add its name to `WizardState.Categories` and a title in `TweaksPage.xaml.cs`.
- Run the app once: catalog problems (duplicate ids, missing fields) are written to the log.

### Where things are stored

- Original values for undo: `%ProgramData%\LumenOptimizer\backups.json`
- Logs: `%ProgramData%\LumenOptimizer\logs\`

## Known limits

- Per-user (HKCU) tweaks apply to the account the app runs as. If a standard user elevates with a *different* admin account, HKCU belongs to that admin account.
- Removed Store apps (Debloat page) and some commands cannot be undone automatically. They are labelled "no automatic undo".
- Defender and Windows Update tweaks are fragile on current Windows: Tamper Protection and servicing can re-enable them. They are marked Aggressive and say so.
- Power plan detection compares English display names as a fallback; on other languages the active plan is still matched by GUID.
- Tweaks are untested on real machines so far. Try them in a VM or on a spare PC first.

## Icon and third-party licenses
The app icon uses the "zap" glyph from Feather by Cole Bemis (MIT) and Lucide, its community fork (ISC). Their license texts are in `THIRD-PARTY-FEATHER-LICENSE.txt` and `THIRD-PARTY-LUCIDE-LICENSE.txt`. The icon is set explicitly in `MainWindow` (`AppWindow.SetIcon`) because unpackaged WinUI 3 apps show no taskbar icon otherwise.
