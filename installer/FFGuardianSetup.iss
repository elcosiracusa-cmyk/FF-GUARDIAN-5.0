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
; Il publish deve essere stato creato con RequireApprovedEngines=true.
; recursesubdirs include EXE, DLL, YAR/YARA, CONF, licenze e payload motori.
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Database aggiornabile fuori da Program Files. FreshClam usa questa directory.
Name: "{commonappdata}\FFGuardian\ClamAV\Database"; Permissions: users-modify
Name: "{commonappdata}\FFGuardian\Logs"; Permissions: users-modify
Name: "{commonappdata}\FFGuardian\Temp"; Permissions: users-modify

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

[Code]
function ExistsAny(const FirstPath, SecondPath: String): Boolean;
begin
  Result := FileExists(FirstPath) or FileExists(SecondPath);
end;

procedure VerifyInstalledPayload;
var
  Missing: String;
  YaraRoot: String;
  ClamRoot: String;
begin
  Missing := '';
  YaraRoot := ExpandConstant('{app}\Engine\Yara\');
  ClamRoot := ExpandConstant('{app}\Engine\ClamAV\');

  if not FileExists(ExpandConstant('{app}\{#MyAppExeName}')) then
    Missing := Missing + #13#10 + '- FFGuardian.exe';

  if not ExistsAny(YaraRoot + 'yara64.exe', YaraRoot + 'yara.exe') then
    Missing := Missing + #13#10 + '- Engine\Yara\yara64.exe oppure yara.exe';

  if not ExistsAny(YaraRoot + 'yarac64.exe', YaraRoot + 'yarac.exe') then
    Missing := Missing + #13#10 + '- Engine\Yara\yarac64.exe oppure yarac.exe';

  if not FileExists(YaraRoot + 'Rules\ffguardian_core.yar') then
    Missing := Missing + #13#10 + '- regola YARA ffguardian_core.yar';

  if not FileExists(ClamRoot + 'clamscan.exe') then
    Missing := Missing + #13#10 + '- Engine\ClamAV\clamscan.exe';

  if not FileExists(ClamRoot + 'freshclam.exe') then
    Missing := Missing + #13#10 + '- Engine\ClamAV\freshclam.exe';

  if Missing <> '' then
    RaiseException('Installazione FFGuardian incompleta. Componenti mancanti:' + Missing);

  if not DirExists(ExpandConstant('{commonappdata}\FFGuardian\ClamAV\Database')) then
    RaiseException('Cartella database ClamAV scrivibile non creata.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    VerifyInstalledPayload;
end;
