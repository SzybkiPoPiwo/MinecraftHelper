#define MyAppName "Minecraft Helper"
#define MyAppExeName "MinecraftHelper.exe"

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef PublishDir
  #error "PublishDir define is required. Example: /DPublishDir=C:\path\to\publish"
#endif

#ifndef InstallerOutputDir
  #define InstallerOutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{D9C22E2B-2FB1-47F4-8C4B-5AA80A3A3B95}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher=SzybkiPoPiwo
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#InstallerOutputDir}
OutputBaseFilename=MinecraftHelper-Setup-{#AppVersion}
SetupIconFile=..\MinecraftHelper\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
