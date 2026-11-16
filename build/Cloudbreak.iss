; Cloudbreak installer (Inno Setup 6).
; Default: install for the current user, no admin rights. The user can pick the folder,
; or choose "all users" to install under Program Files.
; Build: build\build.ps1  (output goes to the Setup folder)

#define AppName "Cloudbreak"
#define AppVersion "1.0.0"
#define AppExe "Cloudbreak.exe"
#define SrcDir "..\publish"

[Setup]
AppId={{7C2E4B1A-5D3F-4E8B-9A61-CB0D2F7E4A11}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
DefaultDirName={autopf}\{#AppName}
DisableDirPage=no
; Browsing to a folder appends "\Cloudbreak", and picking an existing folder shows a warning
AppendDefaultDirName=yes
DirExistsWarning=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
UsedUserAreasWarning=no
OutputDir=..\Setup
OutputBaseFilename=Cloudbreak-Setup-{#AppVersion}
SetupIconFile=..\assets\cloudbreak.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"

[CustomMessages]
en.NeedRuntime=Cloudbreak needs the free .NET 10 Desktop Runtime from Microsoft, which is not on this PC.%n%nOpen the download page now? Run this setup again after installing it.
it.NeedRuntime=Cloudbreak richiede .NET 10 Desktop Runtime di Microsoft (gratuito), che non è presente su questo PC.%n%nVuoi aprire la pagina per scaricarlo? Dopo averlo installato, riavvia questo setup.
en.ShortcutComment=Pomodoro timer for vaping less
it.ShortcutComment=Timer pomodoro per svapare meno
en.StartWithWindows=Start Cloudbreak with Windows
it.StartWithWindows=Avvia Cloudbreak con Windows

[Tasks]
Name: "autostart"; Description: "{cm:StartWithWindows}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SrcDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:ShortcutComment}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:ShortcutComment}"; Tasks: desktopicon

[Registry]
; The app keeps this value in sync with its own "Start with Windows" switch.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Cloudbreak"; ValueData: """{app}\{#AppExe}"" --startup"; Tasks: autostart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Cloudbreak"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Ask the running copy to close cleanly (removes its tray icon), then force it only if still running
Filename: "{app}\{#AppExe}"; Parameters: "--quit"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "QuitCloudbreak"
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "StopCloudbreak"

[UninstallDelete]
; Settings, today's log and error log (a folder only Cloudbreak uses)
Type: filesandordirs; Name: "{userappdata}\Cloudbreak"
; The install folder is removed only if it is empty. Never wipe it recursively: the user picks it,
; and it could be a folder that holds their own files. The app writes nothing there at runtime,
; so the uninstaller's own file list already removes everything Cloudbreak put in it.
Type: dirifempty; Name: "{app}"

[Code]
function HasDesktopRuntime10(): Boolean;
var
  Rec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), Rec);
  if Result then FindClose(Rec);
end;

function InitializeSetup(): Boolean;
var
  Code: Integer;
begin
  Result := True;
  if not HasDesktopRuntime10() then
  begin
    if MsgBox(CustomMessage('NeedRuntime'), mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, Code);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  // Close a running copy so its files can be replaced: cleanly first, by force only as a fallback.
  if FileExists(ExpandConstant('{app}\{#AppExe}')) then
    Exec(ExpandConstant('{app}\{#AppExe}'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := '';
end;

{ Windows 11 remembers every tray icon under HKCU\Control Panel\NotifyIconSettings.
  Remove the entries that point to our exe so nothing is left behind. }
// Windows may store the path literally or with a known-folder GUID in place of the folder
// (for %LOCALAPPDATA%\Programs the GUID is 5CD7AEE2-2219-4A67-B85D-6C9CE15660CB). Match every form.
function WithKnownFolder(const Exe, Folder, Guid: String): String;
begin
  Result := '';
  if Pos(Lowercase(Folder) + '\', Exe) = 1 then
    Result := Lowercase(Guid) + Copy(Exe, Length(Folder) + 1, Length(Exe));
end;

procedure RemoveTrayIconSettings();
var
  Keys: TArrayOfString;
  I: Integer;
  Path, Exe, P1, P2, P3: String;
begin
  Exe := Lowercase(ExpandConstant('{app}\{#AppExe}'));
  P1 := WithKnownFolder(Exe, ExpandConstant('{localappdata}\Programs'), '{5CD7AEE2-2219-4A67-B85D-6C9CE15660CB}');
  P2 := WithKnownFolder(Exe, ExpandConstant('{localappdata}'), '{F1B32785-6FBA-4FCF-9D55-7B8E7F157091}');
  P3 := WithKnownFolder(Exe, ExpandConstant('{commonpf64}'), '{6D809377-6AF0-444B-8957-A3773F02200E}');
  if RegGetSubkeyNames(HKCU, 'Control Panel\NotifyIconSettings', Keys) then
    for I := 0 to GetArrayLength(Keys) - 1 do
      if RegQueryStringValue(HKCU, 'Control Panel\NotifyIconSettings\' + Keys[I], 'ExecutablePath', Path) then
      begin
        Path := Lowercase(Path);
        if (Path = Exe) or ((P1 <> '') and (Path = P1)) or ((P2 <> '') and (Path = P2)) or ((P3 <> '') and (Path = P3)) then
          RegDeleteKeyIncludingSubkeys(HKCU, 'Control Panel\NotifyIconSettings\' + Keys[I]);
      end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveTrayIconSettings();
end;
