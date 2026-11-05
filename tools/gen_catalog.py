import json, sys

T = []

def reg(hive, path, name, value=None, kind="DWord", **kw):
    d = {"type": "registry", "hive": hive, "path": path, "name": name}
    if kw.get("delete"):
        d["delete"] = True
    else:
        d["kind"] = kind
        d["value"] = str(value)
    if "cleanup" in kw:
        d["cleanupKey"] = kw["cleanup"]
    return d

def svc(name, start="Disabled"):
    return {"type": "service", "name": name, "startType": start}

def task(path):
    return {"type": "task", "path": path}

def appx(name):
    return {"type": "appx", "name": name}

def tweak(id, name, desc, cat, risk, actions, warning=None, reboot=False, min_build=0, max_build=0,
          requires=None, avoid=None, avoid_note=None, unverified=False):
    t = {"id": id, "name": name, "description": desc, "category": cat, "risk": risk}
    if warning: t["warning"] = warning
    if reboot: t["requiresReboot"] = True
    if min_build: t["minBuild"] = min_build
    if max_build: t["maxBuild"] = max_build
    if requires: t["requires"] = requires
    if avoid: t["avoidOn"] = avoid
    if avoid_note: t["avoidNote"] = avoid_note
    if unverified: t["unverified"] = True
    t["actions"] = actions
    T.append(t)

HKLM, HKCU = "HKLM", "HKCU"
POL = r"SOFTWARE\Policies\Microsoft\Windows"
CDM = r"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
ADV = r"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
W11 = 22000

# ------------------------------------------------------------------ Privacy
P = "Privacy"
tweak("privacy-telemetry-policy", "Reduce diagnostic data to the minimum",
      "Sets the diagnostic-data policy to 0. Home and Pro treat this as 'Required' data; only Enterprise/Education can switch it off completely.",
      P, "Safe", [reg(HKLM, POL + r"\DataCollection", "AllowTelemetry", 0)])
tweak("privacy-advertising-id", "Turn off the advertising ID",
      "Stops apps from using a per-user advertising ID for targeted ads.",
      P, "Safe", [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0)])
tweak("privacy-tailored-experiences", "Turn off tailored experiences",
      "Stops Microsoft from using your diagnostic data to personalise tips, ads and recommendations.",
      P, "Safe", [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0)])
tweak("privacy-activity-history", "Turn off activity history",
      "Stops Windows from collecting and uploading your activity history (Timeline-style data).",
      P, "Safe", [reg(HKLM, POL + r"\System", n, 0) for n in ("EnableActivityFeed", "PublishUserActivities", "UploadUserActivities")])
tweak("privacy-feedback-prompts", "Stop feedback prompts",
      "Windows will no longer ask you for feedback.",
      P, "Safe", [reg(HKCU, r"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0)])
tweak("privacy-start-suggestions", "Remove suggestions, tips and silent app installs",
      "Turns off Start menu suggestions, lock-screen tips, 'suggested' apps and Windows silently installing promoted apps.",
      P, "Safe", [reg(HKCU, CDM, n, 0) for n in (
          "SubscribedContent-338387Enabled", "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled",
          "SubscribedContent-353694Enabled", "SubscribedContent-353696Enabled", "SystemPaneSuggestionsEnabled",
          "SilentInstalledAppsEnabled", "RotatingLockScreenOverlayEnabled", "SoftLandingEnabled",
          "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled", "ContentDeliveryAllowed")])
tweak("privacy-consumer-features", "Disable Windows consumer features",
      "Stops Windows from auto-installing promoted third-party apps and showing 'consumer' content. Policy-based, so it survives feature updates better.",
      P, "Safe", [reg(HKLM, POL + r"\CloudContent", "DisableWindowsConsumerFeatures", 1)])
tweak("privacy-web-search", "Remove web results from Start search",
      "Start search only searches your PC instead of sending queries to Bing.",
      P, "Safe", [reg(HKCU, r"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1)])
tweak("privacy-recall", "Disable Windows Recall data analysis",
      "Policy that stops Windows from saving snapshots for Recall on Copilot+ PCs. Has no effect on PCs without Recall.",
      P, "Safe", [reg(HKLM, POL + r"\WindowsAI", "DisableAIDataAnalysis", 1)], min_build=26100)
tweak("privacy-copilot-policy", "Turn off Windows Copilot (policy)",
      "Applies the 'Turn off Windows Copilot' policy. Newer Copilot app versions may ignore it; use the Debloat page to remove the Copilot app itself.",
      P, "Safe", [reg(HKCU, r"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)], min_build=22621)
tweak("privacy-telemetry-services", "Disable telemetry services",
      "Disables the Connected User Experiences and Telemetry service (DiagTrack) and the WAP Push message routing service.",
      P, "Moderate", [svc("DiagTrack"), svc("dmwappushservice")],
      warning="Some Microsoft diagnostic and feedback features stop working. Windows Update is not affected.")
tweak("privacy-telemetry-tasks", "Disable telemetry scheduled tasks",
      "Disables the compatibility appraiser, customer-experience and disk-diagnostic data collection tasks.",
      P, "Moderate", [task(p) for p in (
          r"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
          r"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
          r"\Microsoft\Windows\Application Experience\PcaPatchDbTask",
          r"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
          r"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
          r"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector")],
      warning="Disabling the compatibility appraiser can stop Windows from checking whether this PC is ready for a feature update.")

# -------------------------------------------------------------- Performance
F = "Performance"
tweak("perf-ultimate-power", "Use the Ultimate Performance power plan",
      "Switches to the Ultimate Performance plan (created from the built-in template if needed). The previous plan is restored on revert.",
      F, "Moderate", [{"type": "powerplan", "guid": "e9a42b02-d5df-448d-aa00-03f14749eb61", "name": "Ultimate Performance"}],
      warning="Higher power use and heat. On laptops this drains the battery much faster.")
tweak("perf-fast-startup-off", "Turn off Fast Startup",
      "Fast Startup keeps parts of the kernel hibernated between shutdowns, which can cause stale-driver and update problems. A full shutdown becomes a real shutdown.",
      F, "Safe", [reg(HKLM, r"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0)], reboot=True)
tweak("perf-hibernation-off", "Turn off hibernation",
      "Removes hiberfil.sys and frees several GB of disk space.",
      F, "Moderate", [{
          "type": "command",
          "applyCommand": "powercfg.exe /hibernate off",
          "revertCommand": "powercfg.exe /hibernate on",
          "detectCommand": "$v=(Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power' -Name HibernateEnabled -ErrorAction SilentlyContinue).HibernateEnabled; if ($v -eq 0) { exit 0 } else { exit 1 }"}],
      warning="Hibernate and Fast Startup stop working.")
tweak("perf-game-dvr-off", "Turn off Game DVR background recording",
      "Stops Xbox Game Bar from recording in the background, which can cost frames in games.",
      F, "Safe", [reg(HKCU, r"System\GameConfigStore", "GameDVR_Enabled", 0),
                  reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0)])
tweak("perf-transparency-off", "Turn off transparency effects",
      "Disables acrylic/Mica transparency. Small GPU saving on older hardware.",
      F, "Safe", [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0)])
tweak("perf-animations-off", "Turn off window and taskbar animations",
      "Windows minimise/maximise and taskbar animations are disabled so the UI feels snappier.",
      F, "Safe", [reg(HKCU, r"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", "String"),
                  reg(HKCU, ADV, "TaskbarAnimations", 0)], reboot=True)
tweak("perf-menu-delay", "Remove the menu show delay",
      "Menus open instantly instead of after the default 400 ms.",
      F, "Safe", [reg(HKCU, r"Control Panel\Desktop", "MenuShowDelay", "0", "String")], reboot=True)
tweak("perf-startup-delay", "Remove the startup-app delay",
      "Windows waits several seconds after sign-in before launching startup apps. This removes that wait.",
      F, "Safe", [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0)], reboot=True)
tweak("perf-background-apps", "Block Store apps from running in the background",
      "Policy that stops Microsoft Store apps from running in the background when you are not using them.",
      F, "Moderate", [reg(HKLM, POL + r"\AppPrivacy", "LetAppsRunInBackground", 2)],
      warning="Store apps such as Mail or Calendar will not sync or notify until you open them.")
tweak("perf-network-throttling", "Disable multimedia network throttling",
      "Removes the network throttling index used by the multimedia scheduler. Benefit on modern Windows is small; harmless for most PCs.",
      F, "Moderate", [reg(HKLM, r"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 4294967295)],
      warning="Mostly a placebo on current Windows versions; may slightly raise CPU use during heavy network and media load.")

# ----------------------------------------------------------------- Debloat
D = "Debloat"
APPS = [
    ("Microsoft.BingNews", "News", "Safe"), ("Microsoft.BingWeather", "Weather", "Safe"),
    ("Microsoft.GetHelp", "Get Help", "Safe"), ("Microsoft.Getstarted", "Tips", "Safe"),
    ("Microsoft.MicrosoftOfficeHub", "Microsoft 365 (Office) hub", "Safe"),
    ("Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection", "Safe"),
    ("Microsoft.People", "People", "Safe"), ("Microsoft.WindowsFeedbackHub", "Feedback Hub", "Safe"),
    ("Microsoft.YourPhone", "Phone Link", "Moderate"), ("Clipchamp.Clipchamp", "Clipchamp", "Safe"),
    ("Microsoft.WindowsMaps", "Maps", "Safe"), ("Microsoft.MixedReality.Portal", "Mixed Reality Portal", "Safe"),
    ("Microsoft.PowerAutomateDesktop", "Power Automate", "Safe"), ("Microsoft.Todos", "Microsoft To Do", "Safe"),
    ("MSTeams", "Microsoft Teams (personal)", "Safe"), ("Microsoft.Copilot", "Copilot app", "Safe"),
    ("Microsoft.BingSearch", "Bing Search", "Safe"), ("Microsoft.Windows.DevHome", "Dev Home", "Safe"),
    ("Microsoft.OutlookForWindows", "New Outlook", "Moderate"), ("Microsoft.SkypeApp", "Skype", "Safe"),
    ("Microsoft.Microsoft3DViewer", "3D Viewer", "Safe"), ("Microsoft.MSPaint", "Paint 3D", "Safe"),
    ("Microsoft.549981C3F5F10", "Cortana", "Safe"),
    ("king.com.CandyCrushSaga", "Candy Crush Saga", "Safe"), ("king.com.CandyCrushSodaSaga", "Candy Crush Soda Saga", "Safe"),
    ("Microsoft.ZuneVideo", "Movies & TV", "Safe"), ("Microsoft.WindowsCommunicationsApps", "Mail and Calendar", "Moderate"),
    ("Microsoft.OneConnect", "Mobile Plans", "Safe"), ("Microsoft.Whiteboard", "Whiteboard", "Safe"),
    ("MicrosoftCorporationII.QuickAssist", "Quick Assist", "Moderate"), ("MicrosoftCorporationII.MicrosoftFamily", "Microsoft Family", "Safe"),
    ("MicrosoftWindows.Client.WebExperience", "Widgets (Web Experience Pack)", "Moderate"),
    ("Microsoft.Office.OneNote", "OneNote for Windows 10", "Safe"), ("Microsoft.Messaging", "Messaging", "Safe"),
    ("Microsoft.Print3D", "Print 3D", "Safe"), ("Microsoft.Wallet", "Microsoft Pay", "Safe"),
]
for pkg, label, risk in APPS:
    extra = {}
    if pkg == "Microsoft.YourPhone":
        extra["warning"] = "Phone Link, notification mirroring and Link to Windows stop working."
    if pkg == "Microsoft.OutlookForWindows":
        extra["warning"] = "If you use the new Outlook app for mail, it will be removed."
    if pkg == "Microsoft.WindowsCommunicationsApps":
        extra["warning"] = "If you use the Mail and Calendar apps, they will be removed."
    if pkg == "MicrosoftCorporationII.QuickAssist":
        extra["warning"] = "You will not be able to give or get remote help with Quick Assist."
    if pkg == "MicrosoftWindows.Client.WebExperience":
        extra["warning"] = "Removes the Widgets board completely. Reinstall from the Microsoft Store to get it back."
    tweak("debloat-" + pkg.lower().replace(".", "-"), "Remove " + label,
          f"Uninstalls {label} for all users and prevents it from being installed for new users. Reinstall from the Microsoft Store if needed.",
          D, risk, [appx(pkg)], **extra)
tweak("debloat-xbox-apps", "Remove Xbox apps",
      "Removes the Xbox app, Game Bar overlay and related Xbox components.",
      D, "Moderate", [appx(p) for p in ("Microsoft.GamingApp", "Microsoft.XboxGamingOverlay", "Microsoft.XboxIdentityProvider",
                                       "Microsoft.Xbox.TCUI", "Microsoft.XboxSpeechToTextOverlay")],
      warning="Xbox sign-in, Game Pass and games that use Xbox services (for example Minecraft) can stop working.")

# ---------------------------------------------------------------- Services
S = "Services"
tweak("svc-sysmain", "Disable SysMain (Superfetch)",
      "Stops Windows from preloading apps into RAM.",
      S, "Moderate", [svc("SysMain")],
      warning="Helps on SSD systems, but can make a PC with a hard drive feel slower. Leave it on if you have an HDD.")
tweak("svc-wsearch", "Disable Windows Search indexing",
      "Stops the indexer, saving disk and CPU activity.",
      S, "Moderate", [svc("WSearch")],
      warning="Start menu, Explorer and Outlook searches become much slower.")
tweak("svc-fax", "Disable Fax", "The Fax service is unused on virtually all PCs.", S, "Safe", [svc("Fax")])
tweak("svc-maps", "Disable Downloaded Maps Manager", "Only needed by the Maps app.", S, "Safe", [svc("MapsBroker")])
tweak("svc-retaildemo", "Disable Retail Demo service", "Only used on store display PCs.", S, "Safe", [svc("RetailDemo")])
tweak("svc-remote-registry", "Disable Remote Registry", "Prevents remote users from editing this PC's registry. Safer and rarely needed.", S, "Safe", [svc("RemoteRegistry")])
tweak("svc-error-reporting", "Disable Windows Error Reporting", "Stops crash reports from being collected and sent.", S, "Moderate", [svc("WerSvc")],
      warning="Troubleshooting crashes becomes harder because reports are no longer generated.")
tweak("svc-geolocation", "Disable Geolocation service", "Stops Windows from determining your location.", S, "Moderate", [svc("lfsvc")],
      warning="Apps that need your location (maps, weather, 'find my device') stop working.")
tweak("svc-xbox", "Disable Xbox services", "Disables Xbox Live Auth Manager, Game Save, Network and Accessory services.", S, "Moderate",
      [svc(n) for n in ("XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc")],
      warning="Xbox sign-in, cloud saves and Xbox controllers/accessories may stop working in Store and Game Pass games.")

# -------------------------------------------------------------- Appearance
A = "Appearance"
tweak("ui-classic-context-menu", "Classic right-click menu",
      "Brings back the full Windows 10-style context menu without 'Show more options'.",
      A, "Safe", [reg(HKCU, r"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "", "String",
                      cleanup=r"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}")],
      reboot=True, min_build=W11)
tweak("ui-file-extensions", "Show file extensions", "Always show .exe, .pdf and other extensions.", A, "Safe", [reg(HKCU, ADV, "HideFileExt", 0)])
tweak("ui-hidden-files", "Show hidden files and folders", "Hidden items become visible in File Explorer.", A, "Safe", [reg(HKCU, ADV, "Hidden", 1)])
tweak("ui-explorer-this-pc", "Open File Explorer to 'This PC'", "Instead of Home/Quick access.", A, "Safe", [reg(HKCU, ADV, "LaunchTo", 1)])
tweak("ui-taskbar-search-hide", "Hide the taskbar search box", "Removes the search box/icon from the taskbar.", A, "Safe",
      [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0)])
tweak("ui-taskbar-left", "Align taskbar to the left", "Moves the Start button and icons to the left, like Windows 10.", A, "Safe",
      [reg(HKCU, ADV, "TaskbarAl", 0)], min_build=W11)
tweak("ui-hide-task-view", "Hide the Task View button", "Removes the Task View button from the taskbar.", A, "Safe", [reg(HKCU, ADV, "ShowTaskViewButton", 0)])
tweak("ui-widgets-off", "Turn off Widgets", "Disables the Widgets board (news and weather feed) via policy.", A, "Safe",
      [reg(HKLM, r"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0)], min_build=W11)

# ---------------------------------------------------------------- Advanced
V = "Advanced"
tweak("adv-no-auto-updates", "Disable automatic Windows Updates",
      "Sets the 'Configure Automatic Updates: disabled' policy. You can still check for updates manually.",
      V, "Aggressive", [reg(HKLM, POL + r"\WindowsUpdate\AU", "NoAutoUpdate", 1)],
      warning="Your PC will stop receiving security patches automatically. You must install updates yourself, regularly.")
tweak("adv-disable-wu-services", "Disable the Windows Update services",
      "Disables the Windows Update and Update Orchestrator services entirely.",
      V, "Aggressive", [svc("wuauserv"), svc("UsoSvc")],
      warning="No updates at all, including security patches. This can also break the Microsoft Store, Windows Defender definition updates and some installers. Windows may re-enable these services after a feature update.")
tweak("adv-disable-defender", "Disable Microsoft Defender (policy)",
      "Applies policies that turn off Defender real-time protection and related scanners.",
      V, "Aggressive",
      [reg(HKLM, r"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1)] +
      [reg(HKLM, r"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", n, 1) for n in (
          "DisableRealtimeMonitoring", "DisableBehaviorMonitoring", "DisableOnAccessProtection", "DisableIOAVProtection")],
      warning="Your PC will have no built-in malware protection. Only do this if another antivirus is installed. Windows ignores these policies while Tamper Protection is on - turn Tamper Protection off first in Windows Security.",
      reboot=True)
tweak("adv-disable-vbs", "Disable VBS / Memory Integrity (HVCI)",
      "Turns off virtualization-based security and hypervisor-enforced code integrity, which can cost 5-10 percent performance in some games.",
      V, "Aggressive",
      [reg(HKLM, r"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0),
       reg(HKLM, r"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0)],
      warning="Weakens protection against kernel-level malware and exploits. Some anti-cheat systems and Windows features (Credential Guard, Smart App Control) depend on it.",
      reboot=True)
tweak("adv-disable-smartscreen", "Disable SmartScreen",
      "Turns off SmartScreen checks for downloaded files and apps via policy.",
      V, "Aggressive", [reg(HKLM, POL + r"\System", "EnableSmartScreen", 0)],
      warning="Windows will no longer warn you before running unknown or malicious downloads.")
tweak("adv-disable-spooler", "Disable the Print Spooler",
      "Disables printing completely. Also closes a service that has had many serious vulnerabilities.",
      V, "Aggressive", [svc("Spooler")],
      warning="All printing stops: documents, labels and receipt printers that use Windows drivers. Do not use on a PC that prints.")
tweak("adv-remove-onedrive", "Uninstall OneDrive",
      "Closes and uninstalls OneDrive. Revert re-runs the OneDrive installer.",
      V, "Aggressive", [{
          "type": "command",
          "applyCommand": "Stop-Process -Name OneDrive -Force -ErrorAction SilentlyContinue; $p=\"$env:SystemRoot\\SysWOW64\\OneDriveSetup.exe\"; if (-not (Test-Path $p)) { $p=\"$env:SystemRoot\\System32\\OneDriveSetup.exe\" }; & $p /uninstall; Start-Sleep -Seconds 5; exit 0",
          "revertCommand": "$p=\"$env:SystemRoot\\SysWOW64\\OneDriveSetup.exe\"; if (-not (Test-Path $p)) { $p=\"$env:SystemRoot\\System32\\OneDriveSetup.exe\" }; & $p; exit 0",
          "detectCommand": "if ((Test-Path \"$env:LOCALAPPDATA\\Microsoft\\OneDrive\\OneDrive.exe\") -or (Test-Path \"$env:ProgramFiles\\Microsoft OneDrive\\OneDrive.exe\") -or (Test-Path \"${env:ProgramFiles(x86)}\\Microsoft OneDrive\\OneDrive.exe\")) { exit 1 } else { exit 0 }"}],
      warning="If Desktop, Documents or Pictures are backed up to OneDrive (Known Folder Move), move them back to this PC first or those folders will appear empty. Files only stored in the cloud are not downloaded.")

# ================================================================ v0.2 additions
SS = r"Software\Microsoft\Windows\CurrentVersion\SearchSettings"
EDGE = r"SOFTWARE\Policies\Microsoft\Edge"
E = "Edge"

# ---- more Privacy
tweak("privacy-cortana-policy", "Disable Cortana (policy)",
      "Applies the 'Allow Cortana' = off policy so search never hands queries to Cortana.",
      P, "Safe", [reg(HKLM, POL + r"\Windows Search", "AllowCortana", 0)])
tweak("privacy-search-cloud", "Stop cloud and history search in Start",
      "Turns off Microsoft-account and work-account cloud search, and the on-device search history.",
      P, "Safe", [reg(HKCU, SS, n, 0) for n in ("IsAADCloudSearchEnabled", "IsMSACloudSearchEnabled", "IsDeviceSearchHistoryEnabled")])
tweak("privacy-search-highlights", "Turn off search highlights",
      "Removes the daily doodles and trending items from the search box.",
      P, "Safe", [reg(HKCU, SS, "IsDynamicSearchBoxEnabled", 0)])
tweak("privacy-track-progs", "Stop tracking which apps you launch",
      "Start no longer records app launches to rank your 'most used' apps.",
      P, "Safe", [reg(HKCU, ADV, "Start_TrackProgs", 0)])
tweak("privacy-input-personalization", "Stop typing and inking data collection",
      "Stops Windows from learning from what you type and write, and from harvesting contacts.",
      P, "Safe", [reg(HKCU, r"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1),
                  reg(HKCU, r"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1),
                  reg(HKCU, r"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0),
                  reg(HKCU, r"Software\Microsoft\Input\TIPC", "Enabled", 0)])
tweak("privacy-online-speech", "Turn off online speech recognition",
      "Voice typing and dictation will not send audio to Microsoft's cloud for recognition.",
      P, "Safe", [reg(HKCU, r"Software\Microsoft\Speech_OneCore\Settings", "OnlineSpeechPrivacyOptIn", 0)])
tweak("privacy-language-list", "Stop websites reading your language list",
      "Websites can no longer read the language list from Windows to personalise content.",
      P, "Safe", [reg(HKCU, r"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1)])
tweak("privacy-clipboard-sync", "Turn off cross-device clipboard sync",
      "Your clipboard is not synced to other devices through your Microsoft account.",
      P, "Safe", [reg(HKLM, POL + r"\System", "AllowCrossDeviceClipboard", 0)])
tweak("privacy-remote-assistance", "Turn off Remote Assistance",
      "Nobody can connect to this PC with the built-in Remote Assistance invitations.",
      P, "Safe", [reg(HKLM, r"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp", 0)])
tweak("privacy-error-reporting", "Turn off Windows Error Reporting",
      "Crash reports are no longer sent to Microsoft.",
      P, "Safe", [reg(HKLM, r"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1)])
tweak("privacy-location", "Turn off location services (policy)",
      "Windows and apps can no longer determine this PC's location.",
      P, "Moderate", [reg(HKLM, POL + r"\LocationAndSensors", "DisableLocation", 1)],
      warning="Weather, maps and 'find my device' stop working. Apps and the Start menu may show the wrong region.")

# ---- Edge (policy based; Edge shows a 'managed by your organization' notice when policies are set)
tweak("edge-telemetry", "Turn off Edge telemetry and feedback",
      "Stops Edge from sending usage metrics, personalisation data and diagnostic data, and removes the feedback button.",
      E, "Safe", [reg(HKLM, EDGE, n, 0) for n in ("MetricsReportingEnabled", "PersonalizationReportingEnabled", "DiagnosticData", "UserFeedbackAllowed")])
tweak("edge-sidebar", "Turn off the Edge sidebar and Copilot button",
      "Removes the sidebar (Copilot, Shopping, Tools) from Edge.",
      E, "Safe", [reg(HKLM, EDGE, "HubsSidebarEnabled", 0)])
tweak("edge-shopping", "Turn off shopping assistant, rewards and recommendations",
      "Removes coupon popups, price comparisons, Microsoft Rewards prompts and content recommendations.",
      E, "Safe", [reg(HKLM, EDGE, n, 0) for n in ("EdgeShoppingAssistantEnabled", "ShowMicrosoftRewards", "ShowRecommendationsEnabled")])
tweak("edge-background", "Stop Edge running in the background",
      "Disables Startup Boost and background mode so Edge does not stay in memory after you close it.",
      E, "Safe", [reg(HKLM, EDGE, n, 0) for n in ("StartupBoostEnabled", "BackgroundModeEnabled")])
tweak("edge-first-run", "Skip Edge's first-run and welcome pages",
      "Edge opens straight away without the welcome and sign-in pitch.",
      E, "Safe", [reg(HKLM, EDGE, "HideFirstRunExperience", 1)])
tweak("edge-new-tab", "Clean up the Edge new tab page",
      "Removes the Microsoft news feed from the new tab page.",
      E, "Safe", [reg(HKLM, EDGE, "NewTabPageContentEnabled", 0)])
tweak("edge-default-nag", "Stop Edge asking to be the default browser",
      "No more 'make Edge your default browser' prompts.",
      E, "Safe", [reg(HKLM, EDGE, "DefaultBrowserSettingEnabled", 0)])

# ---- more Performance
tweak("perf-delivery-optimization", "Turn off peer-to-peer update sharing",
      "Windows Update downloads only from Microsoft, and stops uploading updates to other PCs on the internet.",
      F, "Safe", [reg(HKLM, r"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0)])
tweak("perf-game-mode", "Make sure Game Mode is on",
      "Game Mode tells Windows to prioritise the game you are playing.",
      F, "Safe", [reg(HKCU, r"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1),
                  reg(HKCU, r"Software\Microsoft\GameBar", "AllowAutoGameMode", 1)])
tweak("perf-mouse-accel", "Turn off mouse acceleration",
      "Turns off 'Enhance pointer precision' so the cursor moves the same distance for the same hand movement. Popular for gaming.",
      F, "Safe", [reg(HKCU, r"Control Panel\Mouse", n, "0", "String") for n in ("MouseSpeed", "MouseThreshold1", "MouseThreshold2")], reboot=True)
tweak("perf-sticky-keys", "Stop sticky-keys popups",
      "Pressing Shift five times (or holding it) no longer opens the Sticky Keys dialogs.",
      F, "Safe", [reg(HKCU, r"Control Panel\Accessibility\StickyKeys", "Flags", "506", "String"),
                  reg(HKCU, r"Control Panel\Accessibility\ToggleKeys", "Flags", "58", "String"),
                  reg(HKCU, r"Control Panel\Accessibility\Keyboard Response", "Flags", "122", "String")])
tweak("perf-hags", "Turn on hardware-accelerated GPU scheduling",
      "Lets the GPU manage its own video memory scheduling, which can reduce latency in some games.",
      F, "Moderate", [reg(HKLM, r"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2)],
      warning="Needs a supported GPU and a recent driver. A few games and capture tools run worse with it. Revert if you see stutter.",
      reboot=True)
tweak("perf-power-throttling", "Turn off power throttling",
      "Stops Windows from slowing down background apps to save power.",
      F, "Moderate", [reg(HKLM, r"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1)],
      warning="Background apps use more CPU. Laptops lose battery faster.")
tweak("perf-shutdown-timeouts", "Shut down faster",
      "Windows waits 2 seconds instead of 5-20 seconds for apps and services that are slow to close.",
      F, "Moderate", [reg(HKLM, r"SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", "2000", "String"),
                      reg(HKCU, r"Control Panel\Desktop", "WaitToKillAppTimeout", "2000", "String"),
                      reg(HKCU, r"Control Panel\Desktop", "HungAppTimeout", "2000", "String"),
                      reg(HKCU, r"Control Panel\Desktop", "AutoEndTasks", "1", "String")],
      warning="Apps that are slow to close are ended automatically, so unsaved work in a hung app can be lost.", reboot=True)
tweak("perf-store-auto-update", "Stop Store apps updating automatically",
      "Microsoft Store apps are only updated when you update them yourself.",
      F, "Moderate", [reg(HKLM, r"SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2)],
      warning="Store apps (and their security fixes) will not update unless you open the Store and update them.")

# ---- more Appearance
W11_22H2 = 22621
tweak("ui-dark-mode", "Use dark mode", "Switches Windows and apps to the dark theme.", A, "Safe",
      [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", n, 0) for n in ("AppsUseLightTheme", "SystemUsesLightTheme")])
tweak("ui-compact-explorer", "Compact File Explorer view", "Reduces the spacing between items in File Explorer.", A, "Safe",
      [reg(HKCU, ADV, "UseCompactMode", 1)], min_build=W11)
tweak("ui-full-path-title", "Show the full path in Explorer's title", "File Explorer windows show the complete folder path.", A, "Safe",
      [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState", "FullPath", 1)])
tweak("ui-no-sync-ads", "Remove OneDrive ads from File Explorer", "Hides the 'sync provider' promotions in File Explorer.", A, "Safe",
      [reg(HKCU, ADV, "ShowSyncProviderNotifications", 0)])
tweak("ui-hide-gallery", "Hide Gallery in File Explorer", "Removes the Gallery entry from the navigation pane.", A, "Safe",
      [reg(HKCU, r"Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}", "System.IsPinnedToNameSpaceTree", 0)], min_build=22631)
tweak("ui-start-recommendations", "Hide recommendations in Start", "Turns off recently opened files and recommended content in Start.", A, "Safe",
      [reg(HKCU, ADV, n, 0) for n in ("Start_TrackDocs", "Start_IrisRecommendations", "Start_AccountNotifications")], min_build=W11)
tweak("ui-chat-icon-off", "Remove the Chat / Teams icon from the taskbar", "Removes the Microsoft Teams chat icon via policy.", A, "Safe",
      [reg(HKLM, POL + r"\Windows Chat", "ChatIcon", 3)], min_build=W11)
tweak("ui-snap-flyout-off", "Turn off the Snap layouts hover flyout", "Stops the layout picker from popping up when you hover the maximize button.", A, "Safe",
      [reg(HKCU, ADV, "EnableSnapAssistFlyout", 0)], min_build=W11_22H2)
tweak("ui-clock-seconds", "Show seconds in the taskbar clock", "The taskbar clock includes seconds.", A, "Safe",
      [reg(HKCU, ADV, "ShowSecondsInSystemClock", 1)], min_build=W11_22H2)
tweak("ui-taskbar-end-task", "Add 'End task' to the taskbar right-click menu", "Right-click a running app on the taskbar to end it.", A, "Safe",
      [reg(HKCU, ADV + r"\TaskbarDeveloperSettings", "TaskbarEndTask", 1)], min_build=22631)

# ---- more Services
tweak("svc-insider", "Disable Windows Insider Service", "Only needed if you are in the Windows Insider program.", S, "Safe", [svc("wisvc")])
tweak("svc-wmp-network", "Disable Windows Media Player Network Sharing", "Shares your media library over the network. Rarely used.", S, "Safe", [svc("WMPNetworkSvc")])
tweak("svc-offline-files", "Disable Offline Files", "Only needed for corporate network folders cached offline.", S, "Moderate", [svc("CscService")],
      warning="Offline copies of network folders stop syncing.")
tweak("svc-link-tracking", "Disable Distributed Link Tracking", "Tracks shortcuts to files moved between NTFS volumes. Rarely helps.", S, "Moderate", [svc("TrkWks")],
      warning="Shortcuts to files moved between drives or PCs may no longer be found automatically.")
tweak("svc-smartcard", "Disable Smart Card services", "Disables the Smart Card, Smart Card Device Enumeration and Smart Card Removal Policy services.", S, "Moderate",
      [svc(n) for n in ("SCardSvr", "ScDeviceEnum", "SCPolicySvc")],
      warning="Smart card and some security-key logins stop working.")
tweak("svc-secondary-logon", "Disable Secondary Logon", "Needed for 'Run as a different user'.", S, "Moderate", [svc("seclogon")],
      warning="'Run as different user' and some installers that elevate to another account stop working.")
tweak("svc-pca", "Disable Program Compatibility Assistant", "Stops the compatibility pop-ups for older programs.", S, "Moderate", [svc("PcaSvc")],
      warning="Older programs lose automatic compatibility fixes.")
tweak("svc-tablet-input", "Disable Touch Keyboard and Handwriting service", "Not needed on a PC without a touch screen or pen.", S, "Moderate", [svc("TabletInputService")],
      warning="The touch keyboard, handwriting panel and pen input stop working. Do not disable on touch screens or 2-in-1 PCs.")
tweak("svc-bluetooth", "Disable Bluetooth Support Service", "Turns Bluetooth off completely.", S, "Moderate", [svc("bthserv")],
      warning="Bluetooth keyboards, mice, headphones and controllers stop working.")
tweak("svc-image-acquisition", "Disable Windows Image Acquisition", "Used by scanners and some cameras.", S, "Moderate", [svc("stisvc")],
      warning="Scanners and some cameras stop working with Windows apps.")

# ---- more Advanced
tweak("adv-defer-feature-updates", "Delay feature updates by one year",
      "Windows installs regular security updates but waits 365 days before installing a new Windows version.",
      V, "Moderate", [reg(HKLM, POL + r"\WindowsUpdate", "DeferFeatureUpdates", 1),
                      reg(HKLM, POL + r"\WindowsUpdate", "DeferFeatureUpdatesPeriodInDays", 365)],
      warning="You will miss new Windows features for a year. Security updates still install.")
tweak("adv-no-driver-updates", "Stop Windows Update replacing drivers",
      "Quality updates no longer include driver updates, so a driver you installed yourself is not overwritten.",
      V, "Moderate", [reg(HKLM, POL + r"\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1)],
      warning="Drivers will not update through Windows Update. Update them yourself from the manufacturer.")
tweak("adv-uac-no-prompt", "Stop UAC prompts for administrators",
      "Administrators run programs as admin without being asked to confirm.",
      V, "Aggressive", [reg(HKLM, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 0)],
      warning="Any program you run can change the system without asking. Malware gets admin rights silently.")
tweak("adv-disable-firewall", "Turn off Windows Firewall",
      "Turns off the firewall on the Domain, Private and Public profiles via policy.",
      V, "Aggressive", [reg(HKLM, r"SOFTWARE\Policies\Microsoft\WindowsFirewall\%s" % p, "EnableFirewall", 0) for p in ("DomainProfile", "StandardProfile", "PublicProfile")],
      warning="Your PC accepts unsolicited connections from the network. Never do this on a laptop that joins public Wi-Fi, or when no other firewall is in place.")
tweak("adv-disable-mitigations", "Turn off Spectre / Meltdown CPU mitigations",
      "Disables the CPU side-channel mitigations. Can give a small speed-up on some older CPUs.",
      V, "Aggressive", [reg(HKLM, r"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride", 3),
                        reg(HKLM, r"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverrideMask", 3)],
      warning="Removes protection against CPU-level data-leak attacks. The speed-up is small or zero on most modern CPUs.", reboot=True)


# ============================================================ Windows 10 specific (build < 22000)
W10 = 21999
tweak("w10-news-interests", "Turn off News and Interests on the taskbar",
      "Removes the weather/news widget from the Windows 10 taskbar and blocks it by policy.",
      "Appearance", "Safe", [reg(HKLM, POL + r"\Windows Feeds", "EnableFeeds", 0),
                              reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", 2)],
      max_build=W10)
tweak("w10-meet-now", "Hide the Meet Now icon",
      "Removes the Skype 'Meet Now' button from the Windows 10 notification area.",
      "Appearance", "Safe", [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1)],
      max_build=W10)
tweak("w10-cortana-leftovers", "Remove Cortana taskbar button and Bing in search",
      "Hides the Cortana button, withdraws Cortana consent and stops Start search from sending your typing to Bing.",
      "Privacy", "Safe", [reg(HKCU, ADV, "ShowCortanaButton", 0),
                          reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0),
                          reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0)],
      max_build=W10)
tweak("w10-people-band", "Hide the People button on the taskbar",
      "Removes the People icon from the Windows 10 taskbar.",
      "Appearance", "Safe", [reg(HKCU, ADV + r"\People", "PeopleBand", 0)], max_build=W10)
tweak("w10-3d-objects", "Hide the 3D Objects folder in This PC",
      "Removes the unused '3D Objects' folder from This PC (64-bit and 32-bit views).",
      "Appearance", "Safe",
      [reg(HKLM, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FolderDescriptions\{31C0DD25-9439-4F12-BF41-7FF4EDA38722}\PropertyBag", "ThisPCPolicy", "Hide", kind="String"),
       reg(HKLM, r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\FolderDescriptions\{31C0DD25-9439-4F12-BF41-7FF4EDA38722}\PropertyBag", "ThisPCPolicy", "Hide", kind="String")],
      max_build=W10)
tweak("w10-ink-workspace", "Turn off Windows Ink Workspace",
      "Disables the Ink Workspace pen panel (only matters if you never use a pen).",
      "Services", "Safe", [reg(HKLM, r"SOFTWARE\Policies\Microsoft\WindowsInkWorkspace", "AllowWindowsInkWorkspace", 0)],
      max_build=W10)
tweak("w10-stay-on-22h2", "Stay on Windows 10 22H2 (stop Windows 11 upgrade offers)",
      "Pins the feature-update target to Windows 10 22H2 so Windows Update does not offer or install Windows 11.",
      "Advanced", "Moderate",
      [reg(HKLM, POL + r"\WindowsUpdate", "TargetReleaseVersion", 1),
       reg(HKLM, POL + r"\WindowsUpdate", "TargetReleaseVersionInfo", "22H2", kind="String"),
       reg(HKLM, POL + r"\WindowsUpdate", "ProductVersion", "Windows 10", kind="String")],
      warning="Windows 10 22H2 no longer gets free security updates. This only stops upgrade offers; it does not make the PC safe. Undo it when you are ready for Windows 11.",
      max_build=W10)
tweak("w10-ceip-tasks", "Disable Customer Experience Improvement tasks",
      "Turns off the scheduled tasks that collect and send CEIP usage data (Consolidator, USB CEIP, Kernel CEIP).",
      "Privacy", "Moderate",
      [task(r"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"),
       task(r"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"),
       task(r"\Microsoft\Windows\Customer Experience Improvement Program\KernelCeipTask")])
tweak("w10-feedback-tasks", "Disable feedback and disk-diagnostic data tasks",
      "Turns off the Siuf feedback tasks and the disk-diagnostic data collector.",
      "Privacy", "Moderate",
      [task(r"\Microsoft\Windows\Feedback\Siuf\DmClient"),
       task(r"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload"),
       task(r"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector")])
tweak("w10-appexp-tasks", "Disable Application Experience data tasks",
      "Turns off ProgramDataUpdater and StartupAppTask, which gather app-compatibility data. Tasks that do not exist on your build are skipped.",
      "Privacy", "Moderate",
      [task(r"\Microsoft\Windows\Application Experience\ProgramDataUpdater"),
       task(r"\Microsoft\Windows\Application Experience\StartupAppTask"),
       task(r"\Microsoft\Windows\Autochk\Proxy")])
tweak("w10-xbox-services", "Disable Xbox background services",
      "Sets the Xbox Game Save, Xbox Live Networking and Xbox Accessory services to Disabled.",
      "Services", "Moderate",
      [svc("XblGameSave"), svc("XboxNetApiSvc"), svc("XboxGipSvc")],
      warning="Xbox game saves and Xbox Live features, and some Xbox controllers, stop working until you undo this.")
tweak("debloat-xbox-overlay", "Remove Xbox overlay and companion apps",
      "Removes the Xbox Game Bar overlays, Xbox Console Companion and the Xbox sign-in helper.",
      "Debloat", "Moderate", [appx(n) for n in ("Microsoft.XboxApp", "Microsoft.XboxGameOverlay", "Microsoft.XboxGamingOverlay",
                                                  "Microsoft.XboxSpeechToTextOverlay", "Microsoft.Xbox.TCUI", "Microsoft.XboxIdentityProvider")],
      warning="Game Bar stops working and some Microsoft Store games can no longer sign in to Xbox. Reinstall the apps from the Store to get them back.")
tweak("debloat-promo-apps", "Remove promotional app stubs",
      "Removes Spotify, Disney+, Prime Video, Netflix, TikTok and Facebook stubs that Windows may install on new accounts.",
      "Debloat", "Safe", [appx(n) for n in ("SpotifyAB.SpotifyMusic", "Disney.37853FC22B2CE", "AmazonVideo.PrimeVideo",
                                             "4DF9E0F8.Netflix", "BytedancePte.Ltd.TikTok", "Facebook.Facebook")])
tweak("privacy-scoobe", "Stop 'Let's finish setting up your device' prompts",
      "Turns off the post-update setup and welcome screens.",
      "Privacy", "Safe", [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0)])
tweak("privacy-onedrive-nag", "Stop OneDrive asking to back up your folders",
      "Blocks the OneDrive prompt that asks to move Desktop, Documents and Pictures into OneDrive.",
      "Privacy", "Safe", [reg(HKLM, r"SOFTWARE\Policies\Microsoft\OneDrive", "KFMBlockOptIn", 1)])
tweak("edge-no-desktop-shortcut", "Stop Edge creating desktop shortcuts",
      "Edge will not add a desktop shortcut when it installs or updates.",
      "Edge", "Safe", [reg(HKLM, r"SOFTWARE\Policies\Microsoft\EdgeUpdate", "CreateDesktopShortcutDefault", 0)])
tweak("ui-gamebar-nag", "Turn off Game Bar controller shortcut and start-up panel",
      "Stops the Game Bar opening when you press the Xbox button on a controller.",
      "Appearance", "Safe", [reg(HKCU, r"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0),
                             reg(HKCU, r"Software\Microsoft\GameBar", "ShowStartupPanel", 0)])

# ============================================================ Windows 11 24H2 / 25H2 (build 26100+; 25H2 is build 26200 on the same code base)
W1124 = 26100
tweak("w11-paint-ai", "Turn off AI features in Paint",
      "Disables Cocreator, Image Creator and Generative Fill in Paint by policy.",
      "Privacy", "Safe", [reg(HKLM, POL + r"\Paint", n, 1) for n in ("DisableCocreator", "DisableImageCreator", "DisableGenerativeFill")],
      min_build=W1124, unverified=True)
tweak("w11-notepad-ai", "Turn off AI features in Notepad",
      "Disables Copilot/AI text features in Notepad by policy.",
      "Privacy", "Safe", [reg(HKLM, r"SOFTWARE\Policies\WindowsNotepad", "DisableAIFeatures", 1)],
      min_build=W1124, unverified=True)
tweak("ui-spotlight-icon", "Hide the 'Learn about this picture' desktop icon",
      "Hides the Windows Spotlight icon that appears on the desktop when Spotlight is your wallpaper.",
      "Appearance", "Safe", [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{2cc5ca98-6485-489a-920e-b3e88a6ccce3}", 1)],
      min_build=22621)

tweak("ui-context-menu-clutter", "Remove 'Give access to', 'Include in library' and 'Restore previous versions' from the right-click menu",
      "Blocks three rarely used shell extensions so the classic right-click menu is shorter. Windows 11's new menu does not show them anyway.",
      "Appearance", "Safe",
      [reg(HKLM, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", g, "", kind="String")
       for g in ("{f81e9010-6ea4-11ce-a7ff-00aa003ca9f6}", "{3dad6c5d-2167-4cae-9914-f99e41c12cfa}", "{596AB062-B4D2-4215-9F74-E9109B0A8153}")],
      reboot=True)

tweak("perf-lastaccess-off", "Stop recording file last-access times",
      "Windows stops updating a timestamp every time a file is read, which saves many small writes. Rarely missed.",
      "Performance", "Safe",
      [{"type": "command",
        "applyCommand": "fsutil behavior set disablelastaccess 1 | Out-Null",
        "revertCommand": "fsutil behavior set disablelastaccess 2 | Out-Null",
        "detectCommand": "$o = (fsutil behavior query disablelastaccess) -join ' '; if ($o -match '= (1|3)') { exit 0 } else { exit 1 }"}])
tweak("perf-crashdump-off", "Turn off crash dump files",
      "Windows stops writing memory dump files after a crash. Saves space and writes.",
      "Performance", "Moderate",
      [reg(HKLM, r"SYSTEM\CurrentControlSet\Control\CrashControl", "CrashDumpEnabled", 0)],
      warning="After a blue-screen crash there will be no dump file to diagnose it with.")

tweak("privacy-autoplay-off", "Turn off AutoPlay for all drives",
      "Windows will not open or act on a disc or USB drive automatically when you plug it in. Closes a common route for malware on USB sticks.",
      "Privacy", "Safe",
      [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 1),
       reg(HKLM, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255)])
tweak("privacy-recent-files-off", "Stop Windows keeping a list of recently opened files",
      "Turns off the recent-files list and taskbar jump lists. Your files are not touched, only the history of them.",
      "Privacy", "Safe",
      [reg(HKCU, r"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs", 0)])

# ============================================================ Hardware-aware: requires / avoid
tweak("svc-nvidia-telemetry", "Disable NVIDIA telemetry service",
      "Turns off NvTelemetryContainer, which sends usage data from older NVIDIA drivers. Newer drivers do not install it.",
      "Services", "Safe", [svc("NvTelemetryContainer")], requires=["nvidia"])
tweak("perf-prefetch-off", "Turn off Prefetch and Superfetch (SSD only)",
      "Stops Windows pre-loading apps into memory. On an SSD that gives little benefit.",
      "Performance", "Moderate",
      [reg(HKLM, r"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", n, 0, ) for n in ("EnablePrefetcher", "EnableSuperfetch")],
      reboot=True, requires=["ssd"])

HW_AVOID = {
    "svc-sysmain": (["hdd"], "On a hard drive, SysMain (Superfetch) makes apps and boot noticeably faster. Leave it on."),
    "perf-fast-startup-off": (["hdd"], "On a hard drive, Fast Startup shortens boot time. Leave it on."),
    "perf-ultimate-power": (["laptop"], "Ultimate Performance drains the battery and makes laptops run hot. Use it on desktops."),
    "perf-power-throttling": (["laptop"], "Power throttling saves battery on laptops. Turning it off costs battery life."),
    "perf-hibernation-off": (["laptop"], "Laptops use hibernate to survive a flat battery. Keep it unless you are short of disk space."),
    "svc-bluetooth": (["laptop"], "Most laptops use Bluetooth for a mouse, headphones or the keyboard."),
    "svc-tablet-input": (["laptop"], "2-in-1 laptops and touch screens need this service for the on-screen keyboard and pen."),
    "adv-disable-firewall": (["laptop"], "Laptops join untrusted networks such as public Wi-Fi. Keep the firewall on."),
}
FITS = {
    "perf-hibernation-off": (["flash"], "Removes a multi-GB hibernation file and its writes on your SD / USB drive."),
    "perf-fast-startup-off": (["flash"], "Fast Startup rewrites the hibernation file every shutdown, which wears flash drives."),
    "svc-wsearch": (["flash"], "Search indexing writes constantly, which flash drives are slow and fragile at."),
    "svc-sysmain": (["flash"], "SysMain keeps reading and writing in the background; flash drives gain from it being off."),
    "privacy-error-reporting": (["flash"], "Fewer report and log files written to your drive."),
    "perf-delivery-optimization": (["flash"], "Stops caching update files for other PCs on your drive."),
    "perf-lastaccess-off": (["flash", "hdd"], "Fewer small writes; helps most on SD cards, USB sticks and hard drives."),
    "perf-crashdump-off": (["flash"], "Saves a large file write after a crash on a small, slow drive."),
    "perf-prefetch-off": (["ssd"], "Prefetch gives little on an SSD."),
    "perf-animations-off": (["flash"], "Helps weak CPUs and slow drives feel faster."),
    "perf-transparency-off": (["flash"], "Lighter on weak CPUs and graphics."),
}
for t in T:
    if t["id"] in FITS:
        t["fitsOn"], t["fitNote"] = FITS[t["id"]]
    if t["id"] in HW_AVOID:
        t["avoidOn"], t["avoidNote"] = HW_AVOID[t["id"]]

# ---- presets
def did(pkg): return "debloat-" + pkg.lower().replace(".", "-")
def mark(t, preset):
    t.setdefault("presets", [])
    if preset not in t["presets"]: t["presets"].append(preset)

REC_DEBLOAT = {did(p) for p in (
    "king.com.CandyCrushSaga", "king.com.CandyCrushSodaSaga", "Microsoft.MicrosoftSolitaireCollection", "Microsoft.Getstarted",
    "Microsoft.WindowsFeedbackHub", "Microsoft.GetHelp", "Microsoft.MicrosoftOfficeHub", "Microsoft.People", "Microsoft.MixedReality.Portal",
    "Microsoft.Microsoft3DViewer", "Microsoft.MSPaint", "Microsoft.Print3D", "Microsoft.SkypeApp", "Microsoft.549981C3F5F10",
    "Microsoft.BingNews", "Microsoft.Windows.DevHome", "Microsoft.PowerAutomateDesktop", "Clipchamp.Clipchamp", "Microsoft.Office.OneNote",
    "Microsoft.Messaging", "Microsoft.Wallet", "Microsoft.OneConnect", "Microsoft.Whiteboard", "MicrosoftCorporationII.MicrosoftFamily",
    "Microsoft.ZuneVideo")}
REC_PERF = {"perf-fast-startup-off", "perf-game-dvr-off", "perf-startup-delay", "perf-delivery-optimization", "perf-sticky-keys"}
GAMING = {"perf-ultimate-power", "perf-fast-startup-off", "perf-game-dvr-off", "perf-startup-delay", "perf-background-apps",
          "perf-game-mode", "perf-hags", "perf-mouse-accel", "perf-power-throttling", "perf-transparency-off", "perf-animations-off",
          "perf-menu-delay", "perf-delivery-optimization", "perf-sticky-keys", "perf-shutdown-timeouts"}
REC_UI = {"ui-file-extensions", "ui-explorer-this-pc", "ui-full-path-title", "ui-no-sync-ads"}
REC_UI |= {"w10-news-interests", "w10-meet-now", "w10-cortana-leftovers", "w10-people-band", "w10-3d-objects", "ui-spotlight-icon", "debloat-promo-apps"}
REC_SVC = {"svc-fax", "svc-maps", "svc-retaildemo", "svc-remote-registry", "svc-insider"}

GAMING |= {"ui-gamebar-nag"}
for t in T:
    if t["risk"] == "Aggressive":
        continue
    cat, tid = t["category"], t["id"]
    if cat == "Privacy" and t["risk"] == "Safe": mark(t, "recommended")
    if cat in ("Privacy", "Edge"): mark(t, "privacy")
    if tid in REC_DEBLOAT or tid in REC_PERF or tid in REC_UI or tid in REC_SVC: mark(t, "recommended")
    if tid in GAMING: mark(t, "gaming")

for t in T: t.pop("presets", None)
# Within the file, list Safe first, then Moderate, then Aggressive (stable, so related tweaks stay together)
T.sort(key=lambda t: {"Safe": 0, "Moderate": 1, "Aggressive": 2}[t["risk"]])

out = sys.argv[1]
ids = [t["id"] for t in T]
assert len(ids) == len(set(ids)), "duplicate ids"
with open(out, "w", encoding="utf-8") as f:
    json.dump({"version": 1, "tweaks": T}, f, indent=2, ensure_ascii=False)
    f.write("\n")
print(len(T), "tweaks")
