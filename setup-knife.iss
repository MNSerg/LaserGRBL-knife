; Inno Setup script for LaserGRBL-knife (plotter fork)
; Does not conflict with original LaserGRBL AppId.

#define MyAppName "LaserGRBL-knife"
#define MyAppVersion "7.14.1-knife"
#define MyAppVersionName "Plotter"
#define MyAppPublisher "MNSerg"
#define MyAppURL "https://github.com/MNSerg/LaserGRBL-knife"
#define MyAppExeName "LaserGRBL.exe"

[Setup]
AppId={{A8C2E5F1-9B34-4D7A-8E21-6F0C4B91D3A7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersionName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=yes
OutputDir=.\artifacts
OutputBaseFilename=LaserGRBL-knife-setup
Compression=lzma
SolidCompression=yes
SetupIconFile=.\install.ico
UninstallDisplayIcon={app}\LaserGRBL.exe
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: ".\LaserGRBL\bin\Release\LaserGRBL.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\LaserGRBL.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\LaserGRBL.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\Autotrace\autotrace.exe"; DestDir: "{app}\Autotrace"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\it\LaserGRBL.resources.dll"; DestDir: "{app}\it"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\da\LaserGRBL.resources.dll"; DestDir: "{app}\da"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\de\LaserGRBL.resources.dll"; DestDir: "{app}\de"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\es\LaserGRBL.resources.dll"; DestDir: "{app}\es"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\fr\LaserGRBL.resources.dll"; DestDir: "{app}\fr"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\pt-BR\LaserGRBL.resources.dll"; DestDir: "{app}\pt-BR"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\ru\LaserGRBL.resources.dll"; DestDir: "{app}\ru"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\zh-CN\LaserGRBL.resources.dll"; DestDir: "{app}\zh-CN"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\sk-SK\LaserGRBL.resources.dll"; DestDir: "{app}\sk-SK"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\hu-HU\LaserGRBL.resources.dll"; DestDir: "{app}\hu-HU"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\cs-CZ\LaserGRBL.resources.dll"; DestDir: "{app}\cs-CZ"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\pl-PL\LaserGRBL.resources.dll"; DestDir: "{app}\pl-PL"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\zh-TW\LaserGRBL.resources.dll"; DestDir: "{app}\zh-TW"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\el-GR\LaserGRBL.resources.dll"; DestDir: "{app}\el-GR"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\tr-TR\LaserGRBL.resources.dll"; DestDir: "{app}\tr-TR"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\ro-RO\LaserGRBL.resources.dll"; DestDir: "{app}\ro-RO"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\nl-NL\LaserGRBL.resources.dll"; DestDir: "{app}\nl-NL"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\uk\LaserGRBL.resources.dll"; DestDir: "{app}\uk"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\ja-JP\LaserGRBL.resources.dll"; DestDir: "{app}\ja-JP"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\Driver\*"; DestDir: "{app}\Driver"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\Firmware\*"; DestDir: "{app}\Firmware"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\StandardMaterials.psh"; DestDir: "{userappdata}\LaserGRBL\"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\StandardButtons.zbn"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\LaserGRBL\bin\Release\Sound\*"; DestDir: "{app}\Sound"; Flags: ignoreversion
Source: ".\lasergrblfile.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\zippedbutton.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{commonprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCR; SubKey: ".nc"; ValueType: string; ValueData: "LaserGRBLknife gcode"; Flags: uninsdeletekey
Root: HKCR; SubKey: "LaserGRBLknife gcode"; ValueType: string; ValueData: "GCode for LaserGRBL-knife"; Flags: uninsdeletekey
Root: HKCR; SubKey: "LaserGRBLknife gcode\Shell\Open\Command"; ValueType: string; ValueData: """{app}\LaserGRBL.exe"" ""%1"""; Flags: uninsdeletekey
Root: HKCR; Subkey: "LaserGRBLknife gcode\DefaultIcon"; ValueType: string; ValueData: "{app}\lasergrblfile.ico,0"; Flags: uninsdeletevalue

Root: HKCR; SubKey: ".dxf"; ValueType: string; ValueData: "LaserGRBLknife dxf"; Flags: uninsdeletekey
Root: HKCR; SubKey: "LaserGRBLknife dxf"; ValueType: string; ValueData: "DXF for LaserGRBL-knife plotter"; Flags: uninsdeletekey
Root: HKCR; SubKey: "LaserGRBLknife dxf\Shell\Open\Command"; ValueType: string; ValueData: """{app}\LaserGRBL.exe"" ""%1"""; Flags: uninsdeletekey
Root: HKCR; Subkey: "LaserGRBLknife dxf\DefaultIcon"; ValueType: string; ValueData: "{app}\lasergrblfile.ico,0"; Flags: uninsdeletevalue
