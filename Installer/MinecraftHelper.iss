#define MyAppName "Minecraft Helper"
#define AutoClickerExeName "MinecraftHelper.AutoClicker.exe"
#define MinerExeName "MinecraftHelper.Miner.exe"

#ifndef AppVersion
  #define AppVersion "2.0.1"
#endif

#ifndef AutoClickerPublishDir
  #error "AutoClickerPublishDir define is required."
#endif

#ifndef MinerPublishDir
  #error "MinerPublishDir define is required."
#endif

#ifndef InstallerOutputDir
  #define InstallerOutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{D9C22E2B-2FB1-47F4-8C4B-5AA80A3A3B95}
AppName={#MyAppName}
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}.0
AppPublisher=SzybkiPoPiwo
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#InstallerOutputDir}
OutputBaseFilename=MinecraftHelper-Setup-{#AppVersion}
SetupIconFile=..\MinecraftHelper\Assets\app.ico
UninstallDisplayIcon={app}\MinecraftHelper.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Obie aplikacje (zalecane)"
Name: "autoclicker"; Description: "Tylko Auto Clicker"
Name: "miner"; Description: "Tylko Kopacz"
Name: "custom"; Description: "Własny wybór"; Flags: iscustom

[Components]
Name: "autoclicker"; Description: "1. Auto Clicker — lekkie AUTO LPM/PPM, akcje PVP, BINDY, Auto zbroja i AutoWater"; Types: full autoclicker
Name: "miner"; Description: "2. Kopacz — kopanie 5/3/3 i 6/3/3, Auto EQ, CobbleX, reconnect, ochrona i logi"; Types: full miner

[Tasks]
Name: "desktopicons"; Description: "Utwórz skróty na pulpicie"; GroupDescription: "Dodatkowe ikony:"; Flags: unchecked

[Files]
Source: "..\MinecraftHelper\Assets\app.ico"; DestDir: "{app}"; DestName: "MinecraftHelper.ico"; Flags: ignoreversion
Source: "{#AutoClickerPublishDir}\*"; DestDir: "{app}\AutoClicker"; Components: autoclicker; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MinerPublishDir}\*"; DestDir: "{app}\Kopacz"; Components: miner; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\MinecraftHelper.exe"
Type: files; Name: "{app}\MinecraftHelper.dll"
Type: files; Name: "{app}\MinecraftHelper.deps.json"
Type: files; Name: "{app}\MinecraftHelper.runtimeconfig.json"
Type: filesandordirs; Name: "{app}\AutoClicker"; Check: not WizardIsComponentSelected('autoclicker')
Type: filesandordirs; Name: "{app}\Kopacz"; Check: not WizardIsComponentSelected('miner')

[Icons]
Name: "{autoprograms}\{#MyAppName}\Auto Clicker"; Filename: "{app}\AutoClicker\{#AutoClickerExeName}"; Components: autoclicker
Name: "{autoprograms}\{#MyAppName}\Kopacz"; Filename: "{app}\Kopacz\{#MinerExeName}"; Components: miner
Name: "{autodesktop}\Minecraft Helper Auto Clicker"; Filename: "{app}\AutoClicker\{#AutoClickerExeName}"; Components: autoclicker; Tasks: desktopicons
Name: "{autodesktop}\Minecraft Helper Kopacz"; Filename: "{app}\Kopacz\{#MinerExeName}"; Components: miner; Tasks: desktopicons

[Run]
Filename: "{app}\AutoClicker\{#AutoClickerExeName}"; Description: "Uruchom Auto Clicker"; Components: autoclicker; Flags: nowait postinstall skipifsilent unchecked
Filename: "{app}\Kopacz\{#MinerExeName}"; Description: "Uruchom Kopacza"; Components: miner; Flags: nowait postinstall skipifsilent unchecked

[Code]
const
  LegacyUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{D9C22E2B-2FB1-47F4-8C4B-5AA80A3A3B95}_is1';

function FindLegacyInstallationAtRoot(
  RootKey: Integer;
  var LegacyExePath: String;
  var UninstallerPath: String): Boolean;
var
  InstallLocation: String;
  UninstallCommand: String;
begin
  Result := False;
  if not RegQueryStringValue(RootKey, LegacyUninstallKey, 'InstallLocation', InstallLocation) then
    Exit;

  LegacyExePath := AddBackslash(InstallLocation) + 'MinecraftHelper.exe';
  if not FileExists(LegacyExePath) then
    Exit;

  Result := True;
  UninstallerPath := '';
  if RegQueryStringValue(RootKey, LegacyUninstallKey, 'UninstallString', UninstallCommand) then
    UninstallerPath := RemoveQuotes(Trim(UninstallCommand));

  if (UninstallerPath = '') or (not FileExists(UninstallerPath)) then
    UninstallerPath := AddBackslash(InstallLocation) + 'unins000.exe';
end;

function FindLegacyInstallation(
  var LegacyExePath: String;
  var UninstallerPath: String): Boolean;
var
  DefaultInstallLocation: String;
begin
  Result := FindLegacyInstallationAtRoot(
    HKEY_CURRENT_USER,
    LegacyExePath,
    UninstallerPath);
  if Result then
    Exit;

  Result := FindLegacyInstallationAtRoot(
    HKEY_LOCAL_MACHINE,
    LegacyExePath,
    UninstallerPath);
  if Result then
    Exit;

  { Awaryjne wykrycie instalacji bez prawidłowego wpisu rejestru. }
  DefaultInstallLocation := ExpandConstant('{localappdata}\Programs\{#MyAppName}');
  LegacyExePath := AddBackslash(DefaultInstallLocation) + 'MinecraftHelper.exe';
  if FileExists(LegacyExePath) then
  begin
    UninstallerPath := AddBackslash(DefaultInstallLocation) + 'unins000.exe';
    Result := True;
  end;
end;

function InitializeSetup(): Boolean;
var
  LegacyExePath: String;
  UninstallerPath: String;
  ResultCode: Integer;
begin
  Result := True;
  if not FindLegacyInstallation(LegacyExePath, UninstallerPath) then
    Exit;

  if not WizardSilent then
  begin
    if MsgBox(
      'Wykryto starszą, połączoną wersję Minecraft Helper.' + #13#10 + #13#10 +
      'Nowe wydanie instaluje Auto Clicker i Kopacza jako dwa osobne programy. ' +
      'Przed kontynuowaniem stara wersja musi zostać odinstalowana. ' +
      'Ustawienia zapisane w AppData pozostaną zachowane.' + #13#10 + #13#10 +
      'Czy uruchomić deinstalację starej wersji teraz?',
      mbInformation,
      MB_YESNO or MB_DEFBUTTON2) <> IDYES then
    begin
      Result := False;
      Exit;
    end;
  end;

  if not FileExists(UninstallerPath) then
  begin
    MsgBox(
      'Nie znaleziono deinstalatora starej wersji.' + #13#10 +
      'Odinstaluj Minecraft Helper ręcznie w Ustawieniach Windows, a następnie uruchom ten instalator ponownie.',
      mbError,
      MB_OK);
    Result := False;
    Exit;
  end;

  if (not Exec(
        UninstallerPath,
        '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART',
        '',
        SW_SHOWNORMAL,
        ewWaitUntilTerminated,
        ResultCode)) or (ResultCode <> 0) then
  begin
    MsgBox(
      'Nie udało się odinstalować starej wersji Minecraft Helper.' + #13#10 +
      'Zamknij działający program, odinstaluj go ręcznie i uruchom instalator ponownie.',
      mbError,
      MB_OK);
    Result := False;
    Exit;
  end;

  if FileExists(LegacyExePath) then
  begin
    MsgBox(
      'Stara wersja nadal znajduje się na dysku.' + #13#10 +
      'Zamknij Minecraft Helper lub uruchom ponownie Windows, dokończ deinstalację i spróbuj jeszcze raz.',
      mbError,
      MB_OK);
    Result := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectComponents) and
     (not WizardIsComponentSelected('autoclicker')) and
     (not WizardIsComponentSelected('miner')) then
  begin
    MsgBox('Wybierz Auto Clicker, Kopacza albo obie aplikacje.', mbError, MB_OK);
    Result := False;
  end;
end;
