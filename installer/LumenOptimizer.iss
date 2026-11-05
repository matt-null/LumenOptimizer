; Inno Setup 6.3+ script. Build the portable output first (..\build.ps1 does both).
[Setup]
AppId={{B7E1C3A2-4D59-4F0B-8A6E-2C9D1F3E5A70}
AppName=Lumen Optimizer
AppVersion=0.4.3
AppPublisher=Alright Dev
SetupIconFile=..\src\LumenOptimizer.UI\Assets\AppIcon.ico
AppPublisherURL=https://alrightdev.com
AppSupportURL=https://alrightdev.com
DefaultDirName={autopf}\LumenOptimizer
DefaultGroupName=Lumen Optimizer
OutputDir=..\dist
OutputBaseFilename=LumenOptimizer-Setup
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\LumenOptimizer.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\dist\portable\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Lumen Optimizer"; Filename: "{app}\LumenOptimizer.exe"
Name: "{autodesktop}\Lumen Optimizer"; Filename: "{app}\LumenOptimizer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\LumenOptimizer.exe"; Description: "Launch Lumen Optimizer"; Flags: nowait postinstall skipifsilent runascurrentuser
