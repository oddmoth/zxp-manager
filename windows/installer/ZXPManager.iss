; ZXP Manager — Windows installer (Inno Setup 6)
;
; Installs for the current user only: no administrator password needed.
; Adds a Start menu shortcut, an uninstaller (Settings → Apps) and lets
; .zxp / .ccx files open in ZXP Manager.
;
; Built by the release workflow:
;   ISCC /DAppVersion=1.0.0 /DAppExe=C:\path\to\ZXPManager.exe /Odist ZXPManager.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppExe
  #define AppExe "..\..\out\ZXPManager.exe"
#endif

#define AppName "ZXP Manager"
#define ExeName "ZXPManager.exe"

[Setup]
; Never change AppId: Windows uses it to recognise updates of the same app
AppId={{7687D5DD-80DE-4C2A-98F0-22EFBCA2C83E}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Odd Moth
AppPublisherURL=https://github.com/oddmoth/zxp-manager
AppSupportURL=https://github.com/oddmoth/zxp-manager/issues
AppUpdatesURL=https://github.com/oddmoth/zxp-manager/releases
AppCopyright=© 2026 Odd Moth · MIT License
VersionInfoVersion={#AppVersion}

PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
MinVersion=10.0

OutputBaseFilename=ZXP-Manager-Setup-{#AppVersion}
SetupIconFile=..\AppIcon.ico
UninstallDisplayIcon={app}\{#ExeName}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ChangesAssociations=yes
ShowLanguageDialog=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; The release workflow lists here every translation that comes with Inno Setup
#if FileExists(AddBackslash(SourcePath) + "languages.iss")
  #include "languages.iss"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#ExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Registry]
; .zxp: open with ZXP Manager by default
Root: HKA; Subkey: "Software\Classes\.zxp"; ValueType: string; ValueName: ""; ValueData: "OddMoth.ZXPManager.zxp"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.zxp\OpenWithProgids"; ValueType: string; ValueName: "OddMoth.ZXPManager.zxp"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\OddMoth.ZXPManager.zxp"; ValueType: string; ValueName: ""; ValueData: "Adobe extension (ZXP)"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\OddMoth.ZXPManager.zxp\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#ExeName},0"
Root: HKA; Subkey: "Software\Classes\OddMoth.ZXPManager.zxp\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#ExeName}"" ""%1"""

; .ccx: only listed under "Open with", so the Creative Cloud app keeps opening them by default
Root: HKA; Subkey: "Software\Classes\.ccx\OpenWithProgids"; ValueType: string; ValueName: "OddMoth.ZXPManager.ccx"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\OddMoth.ZXPManager.ccx"; ValueType: string; ValueName: ""; ValueData: "Adobe plugin (CCX)"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\OddMoth.ZXPManager.ccx\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#ExeName},0"
Root: HKA; Subkey: "Software\Classes\OddMoth.ZXPManager.ccx\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#ExeName}"" ""%1"""

[Run]
Filename: "{app}\{#ExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
