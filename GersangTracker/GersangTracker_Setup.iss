[Setup]
AppName=GersangTracker
AppVersion=2.2.1
AppPublisher=JIK
DefaultDirName={autopf}\GersangTracker
DefaultGroupName=GersangTracker
OutputDir=installer
OutputBaseFilename=GersangTracker_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=GersangTracker
UninstallDisplayIcon={app}\GersangTracker.exe
UsedUserAreasWarning=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 아이콘:"; Flags: unchecked

[Files]
Source: "bin\Release\net10.0-windows10.0.19041.0\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\GersangTracker"; Filename: "{app}\GersangTracker.exe"
Name: "{group}\GersangTracker 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GersangTracker"; Filename: "{app}\GersangTracker.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GersangTracker.exe"; Description: "GersangTracker 실행"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\GersangTracker"