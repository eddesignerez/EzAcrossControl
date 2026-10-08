#ifndef Version
  #define Version "1.1.0"
#endif
#ifndef Bundle
  #define Bundle "..\.publish-staging\windows"
#endif
#ifndef Output
  #define Output "..\release-output"
#endif
#ifndef Port
  #define Port "8765"
#endif
[Setup]
AppId={{BEDB5ECD-E011-496D-9D70-2167B51103EF}
AppName=EZ Across Control
AppVersion={#Version}
AppPublisher=ElementZero
AppPublisherURL=https://github.com/eddesignerez/EzAcrossControl
DefaultDirName={autopf}\EZ Across Control
DefaultGroupName=EZ Across Control
UninstallDisplayIcon={app}\WindowsHost.exe
SetupIconFile=..\Windows-host\Assets\EzAcrossControl152D4D.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=admin
OutputDir={#Output}
OutputBaseFilename=EZAcrossControl-{#Version}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
WizardStyle=modern
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Files]
Source: "{#Bundle}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\EZ Across Control"; Filename: "{app}\WindowsHost.exe"
Name: "{autodesktop}\EZ Across Control"; Filename: "{app}\WindowsHost.exe"; Tasks: desktopicon
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
[Run]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""EZ Across Control LAN"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""EZ Across Control LAN"" dir=in action=allow program=""{app}\WindowsHost.exe"" profile=private remoteip=localsubnet protocol=TCP localport={#Port}"; Flags: runhidden
Filename: "{app}\WindowsHost.exe"; Description: "Open EZ Across Control"; Flags: nowait postinstall skipifsilent runasoriginaluser
[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""EZ Across Control LAN"""; Flags: runhidden; RunOnceId: RemoveLanFirewallRule
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    { A preexisting reservation belongs to its owner and is left intact. }
    if Exec(ExpandConstant('{sys}\netsh.exe'), 'http add urlacl url=http://+:{#Port}/ sddl=D:(A;;GX;;;IU)', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
      RegWriteDWordValue(HKLM, 'Software\ElementZero\EZAcrossControl', 'OwnsUrlAcl{#Port}', 1);
  end;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Owned: Cardinal; ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
    if RegQueryDWordValue(HKLM, 'Software\ElementZero\EZAcrossControl', 'OwnsUrlAcl{#Port}', Owned) and (Owned = 1) then
    begin
      Exec(ExpandConstant('{sys}\netsh.exe'), 'http delete urlacl url=http://+:{#Port}/', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      RegDeleteValue(HKLM, 'Software\ElementZero\EZAcrossControl', 'OwnsUrlAcl{#Port}');
    end;
end;
