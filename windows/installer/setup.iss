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
OutputDir=.
OutputBaseFilename=LANType-Setup
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
Source: "..\desktop-app\bin\Release\net9.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
; Background Service
Source: "..\background-service\bin\Release\net9.0\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs
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

[Run]
; Register Windows Service
Filename: "{sys}\sc.exe"; Parameters: "create LANTypeService binPath= ""{app}\Service\LANType.Service.exe"" start= auto DisplayName= ""LANType Background Service"" depend= Tcpip"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "description LANTypeService ""LANType background typing engine and authenticated WebSocket server."""; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start LANTypeService"; Flags: runhidden
; Run Desktop App after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop and delete service before removing files
Filename: "{sys}\sc.exe"; Parameters: "stop LANTypeService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete LANTypeService"; Flags: runhidden
