#define MyAppName "FF GUARDIAN 10.0.1 Stable"
#define MyAppVersion "10.0.1"
#define MyAppPublisher "EL.CO — by FFsoftware"
#define MyAppExeName "FFGuardian.exe"

[Setup]
AppId={{7D1F1F01-FF50-4D50-9A00-EC0050000001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/elcosiracusa-cmyk/FF-GUARDIAN-5.0
AppSupportURL=mailto:alsafe127.00@gmail.com
DefaultDirName={autopf}\FF GUARDIAN
DefaultGroupName=FF GUARDIAN
OutputDir=Output
OutputBaseFilename=FFGuardianSetup-10.0.1-Stable
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
Uninstallable=yes
CreateUninstallRegKey=yes

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\FF GUARDIAN 10.0.1"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\FF GUARDIAN 10.0.1"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crea il collegamento FF GUARDIAN sul Desktop"; GroupDescription: "Collegamenti:"; Flags: checkedonce

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Avvia FF GUARDIAN 10.0.1"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden waituntilterminated; RunOnceId: "StopFFGuardian"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
