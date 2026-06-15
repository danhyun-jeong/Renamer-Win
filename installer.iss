#define AppName "Renamer"
#define AppVersion "1.0.0"
#define AppPublisher "Danhyun Jeong"
#define AppExeName "Renamer.exe"
#define PublishDir "bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{A7F3B2C1-4D8E-4F9A-B0C2-3E5D6F7A8B9C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=Renamer_Setup
SetupIconFile=Renamer_Icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 아이콘:"; Flags: unchecked
Name: "startupicon"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "추가 옵션:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExeName}";         DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\D3DCompiler_47_cor3.dll";     DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\PenImc_cor3.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\PresentationNative_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\vcruntime140_cor3.dll";       DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\wpfgfx_cor3.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "Renamer_Icon.ico";                          DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; 시작 시 자동 실행
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Renamer 시작하기"; Flags: nowait postinstall skipifsilent
