; ============================================================================
; Nightflow - Windows Installer Script (Inno Setup 6)
; Creates professional installer for the game
; ============================================================================

#define MyAppName "Nightflow"
; Version can be overridden from command line: ISCC /DMyAppVersion=x.x.x installer.iss
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "Kase Branham"
#define MyAppURL "https://github.com/kase1111-hash/Midnight-pulse"
#define MyAppExeName "Nightflow.exe"

[Setup]
; Application identity
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation settings
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=yes

; Installer appearance
WizardStyle=modern
WizardImageFile=compiler:WizModernImage.bmp
WizardSmallImageFile=compiler:WizModernSmallImage.bmp

; Output settings
OutputDir=..\Installer
OutputBaseFilename=Nightflow_Setup_{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=4

; Compatibility
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Privileges
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Uninstall settings
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main game files
Source: "..\Build\Windows\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Build\Windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Build\Windows\{#MyAppName}_Data\*"; DestDir: "{app}\{#MyAppName}_Data"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\Build\Windows\MonoBleedingEdge\*"; DestDir: "{app}\MonoBleedingEdge"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: DirExists(ExpandConstant('{src}\..\Build\Windows\MonoBleedingEdge'))

; Documentation (optional)
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\copyright.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Store installation path for future updates
Root: HKCU; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey

[Code]
// Check if DirectX is installed (optional, for error reporting)
function InitializeSetup(): Boolean;
begin
  Result := True;

  // Check Windows version
  if not IsWin64 then
  begin
    MsgBox('Nightflow requires a 64-bit version of Windows 10 or later.', mbError, MB_OK);
    Result := False;
  end;
end;

// Custom installation progress messages
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    WizardForm.StatusLabel.Caption := 'Installing Nightflow game files...';
  end
  else if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := 'Finalizing installation...';
  end;
end;

// Clean up settings on uninstall
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SavePath: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Optionally ask to remove save data
    SavePath := ExpandConstant('{localappdata}\{#MyAppPublisher}\{#MyAppName}');
    if DirExists(SavePath) then
    begin
      if MsgBox('Do you want to remove saved game data and settings?', mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(SavePath, True, True, True);
      end;
    end;
  end;
end;

