; Dansk/English: Byg med scripts/package-windows.ps1 / Build with scripts/package-windows.ps1.
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif

[Setup]
AppId={{87AF9AA0-6C89-4404-8BCD-9048AC911001}
AppName=Control Hub
AppVersion=0.0.001
AppVerName=Control Hub 0.0.001
AppPublisher=thebonden
AppPublisherURL=https://github.com/thebonden/Control_Hub
DefaultDirName={localappdata}\Programs\ControlHub
DefaultGroupName=Control Hub
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=ControlHub-0.0.001-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\ControlHub.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
danish.DesktopShortcut=Opret en genvej på skrivebordet
english.DesktopShortcut=Create a desktop shortcut
danish.OpenHub=Åbn Control Hub
english.OpenHub=Open Control Hub
danish.Guide=Installationsvejledning
english.Guide=Installation guide

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Control Hub"; Filename: "{app}\ControlHub.exe"
Name: "{group}\{cm:Guide}"; Filename: "{app}\docs\installation.da.html"; Languages: danish
Name: "{group}\{cm:Guide}"; Filename: "{app}\docs\installation.en.html"; Languages: english
Name: "{autodesktop}\Control Hub"; Filename: "{app}\ControlHub.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ControlHub.exe"; Description: "{cm:OpenHub}"; Flags: nowait postinstall skipifsilent unchecked

; Bevar brugerens krypterede indstillinger og Stream Deck-profiler ved afinstallation.
; Preserve encrypted user settings and Stream Deck profiles during uninstall.
