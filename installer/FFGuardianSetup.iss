#define MyAppName "FF GUARDIAN 8.3.4 Full Bug and UI Audit"
#define MyAppVersion "8.3.4"
#define MyAppPublisher "EL.CO di Francesco Fazzina"
#define MyAppExeName "FFGuardian.exe"

[Setup]
AppId={{7D1F1F01-FF50-4D50-9A00-EC0050000001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\FF GUARDIAN
DefaultGroupName=FF GUARDIAN
OutputDir=Output
OutputBaseFilename=FFGuardianSetup-8.3.4-Full-Bug-UI-Audit
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\FF GUARDIAN 8.3.4"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\FF GUARDIAN 8.3.4"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crea il collegamento Dobermann sul Desktop"; GroupDescription: "Collegamenti:"; Flags: checkedonce

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Avvia FF GUARDIAN 8.3.4"; Flags: nowait postinstall skipifsilent runascurrentuser