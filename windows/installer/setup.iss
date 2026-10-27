; LANType Windows Installer Script (Inno Setup 6+)
; Conforming to LANType project specifications (Section 8)

#define MyAppName "LANType"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "LANType Project"
#define MyAppURL "https://github.com/lantype/lantype"
#define MyAppExeName "LANType.Desktop.exe"

[Setup]
AppId={{99DC2C96-CE61-40CA-920C-587A3C5688EE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName=C:\Program Files\{#MyAppName}
DisableDirPage=no
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=dist
OutputBaseFilename=LANType-Setup-v1.0.0
SetupIconFile=app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Desktop Application
Source: "publish\Desktop\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
; Background Service
Source: "publish\Service\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs
; Driver Package
Source: "..\hid-driver\LANTypeVhid.inf"; DestDir: "{app}\Driver"; Flags: ignoreversion
Source: "..\hid-driver\bin\Release\LANTypeVhid.sys"; DestDir: "{app}\Driver"; Flags: ignoreversion skipifsourcedoesntexist

[Dirs]
Name: "C:\ProgramData\LANType\logs"; Permissions: users-full

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "LANTypeService"; Flags: deletevalue uninsdeletevalue

[Run]
; Stop and delete any legacy Session 0 service to avoid Session 0 Isolation blocking SendInput
Filename: "{sys}\sc.exe"; Parameters: "stop LANTypeService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete LANTypeService"; Flags: runhidden

; Configure Windows Firewall rules
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""LANType WebSocket Port"" dir=in action=allow protocol=TCP localport=52443 profile=any"; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""LANType Discovery Port"" dir=in action=allow protocol=UDP localport=52442 profile=any"; Flags: runhidden

; Run Desktop App after install (shellexec required for requireAdministrator manifest)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallRun]
; Stop running processes before removing files
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM LANType.Service.exe"; Flags: runhidden
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM LANType.Desktop.exe"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "stop LANTypeService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete LANTypeService"; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""LANType WebSocket Port"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""LANType Discovery Port"""; Flags: runhidden
