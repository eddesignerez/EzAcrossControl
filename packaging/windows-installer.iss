#ifndef Version
  #define Version "1.1.3"
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
UninstallDisplayIcon={app}\EzAcrossControl.exe
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
[InstallDelete]
Type: files; Name: "{app}\WindowsHost.exe"
Type: files; Name: "{app}\WindowsHost.dll"
Type: files; Name: "{app}\WindowsHost.deps.json"
Type: files; Name: "{app}\WindowsHost.runtimeconfig.json"
[Icons]
Name: "{group}\EZ Across Control"; Filename: "{app}\EzAcrossControl.exe"
Name: "{autodesktop}\EZ Across Control"; Filename: "{app}\EzAcrossControl.exe"; Tasks: desktopicon
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
[Run]
Filename: "{app}\EzAcrossControl.exe"; Description: "Open EZ Across Control"; Flags: nowait postinstall skipifsilent runasoriginaluser
[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""EZ Across Control LAN"""; Flags: runhidden; RunOnceId: RemoveLanFirewallRule
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var ResultCode: Integer; Policy, Rule: Variant; Existing: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
    Policy := CreateOleObject('HNetCfg.FwPolicy2');
    Existing := True;
    try Rule := Policy.Rules.Item('EZ Across Control LAN');
    except
      Existing := False;
      Rule := CreateOleObject('HNetCfg.FWRule');
      Rule.Name := 'EZ Across Control LAN';
      Rule.Protocol := 6;
      Rule.LocalPorts := '{#Port}';
    end;
    { Keep the selected port when upgrading, including migration of the old executable name. }
    Rule.ApplicationName := ExpandConstant('{app}\EzAcrossControl.exe');
    Rule.Direction := 1;
    Rule.Action := 1;
    Rule.Protocol := 6;
    Rule.RemoteAddresses := 'LocalSubnet';
    Rule.Profiles := 2;
    Rule.Enabled := True;
    if not Existing then Policy.Rules.Add(Rule);
    { A preexisting reservation belongs to its owner and is left intact. }
    if Exec(ExpandConstant('{sys}\netsh.exe'), 'http add urlacl url=http://+:{#Port}/ sddl=D:(A;;GX;;;IU)', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
      RegWriteDWordValue(HKLM, 'Software\ElementZero\EZAcrossControl', 'OwnsUrlAcl{#Port}', 1);
  end;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Owned: Cardinal; ResultCode, I, PortNumber: Integer; Names: TArrayOfString; Name, PortText: String;
begin
  if CurUninstallStep = usPostUninstall then
    if RegGetValueNames(HKLM, 'Software\ElementZero\EZAcrossControl', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      Name := Names[I];
      if Copy(Name, 1, 10) = 'OwnsUrlAcl' then
      begin
        PortText := Copy(Name, 11, Length(Name));
        PortNumber := StrToIntDef(PortText, 0);
        if (PortNumber >= 1) and (PortNumber <= 65535) and
          RegQueryDWordValue(HKLM, 'Software\ElementZero\EZAcrossControl', Name, Owned) and (Owned = 1) then
        begin
          Exec(ExpandConstant('{sys}\netsh.exe'), 'http delete urlacl url=http://+:' + IntToStr(PortNumber) + '/', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
          if ResultCode = 0 then RegDeleteValue(HKLM, 'Software\ElementZero\EZAcrossControl', Name);
        end;
      end;
    end;
end;
