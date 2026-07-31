[Setup]
AppName=FFGuardian
AppVersion=8.4.0
AppPublisher=EL.CO di Francesco Fazzina
DefaultDirName={pf}\FFGuardian
DefaultGroupName=FFGuardian
OutputBaseFilename=FFGuardianSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Files]
Source: "artifacts\ffguardian\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FFGuardian"; Filename: "{app}\FFGuardian.exe"
Name: "{commondesktop}\FFGuardian"; Filename: "{app}\FFGuardian.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Crea collegamento sul desktop"; GroupDescription: "Opzioni installazione"; Flags: unchecked

[Run]
Filename: "{app}\FFGuardian.exe"; Description: "Avvia FFGuardian"; Flags: nowait postinstall skipifsilent
