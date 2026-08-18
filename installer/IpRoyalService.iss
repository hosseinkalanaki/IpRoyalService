#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef StageDir
  #error StageDir must be supplied by Build-Installer.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by Build-Installer.ps1
#endif

#define MyAppName "IPRoyal SOCKS5 Enforcement"
#define MyServiceName "IpRoyalProxyEnforcement"
#define MyAppExeName "IpRoyalService.exe"

[Setup]
AppId={{B8A75DA4-A68B-46C6-AE2B-E4E785C058E0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=IPRoyal Service
DefaultDirName={autopf}\IpRoyalService
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
OutputDir={#OutputDir}
OutputBaseFilename=IpRoyalService-v{#MyAppVersion}-win-x64-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes
CloseApplications=no
RestartApplications=no
ChangesAssociations=no
ChangesEnvironment=no

[Files]
Source: "{#StageDir}\IpRoyalService.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\Manage-Service.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\USER-GUIDE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\SING-BOX-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#StageDir}\engine\sing-box.exe"; DestDir: "{app}\engine"; Flags: ignoreversion

[Icons]
Name: "{group}\Manage Service"; Filename: "{app}\Manage-Service.cmd"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[UninstallDelete]
Type: files; Name: "{commonappdata}\IpRoyalService\engine.json"
Type: files; Name: "{commonappdata}\IpRoyalService\service.log"
Type: dirifempty; Name: "{commonappdata}\IpRoyalService"

[Code]
var
  ConfigPage: TInputQueryWizardPage;
  ExistingPage: TInputOptionWizardPage;
  ConfigurationCreated: Boolean;
  ServicePreExisting: Boolean;
  ConfigurationExisted: Boolean;
  PreviousConfiguration: AnsiString;

function ConfigPath: String;
begin
  Result := AddBackslash(WizardDirValue) + 'config.json';
end;

function JsonEscape(Value: String): String;
begin
  Result := Value;
  StringChangeEx(Result, '\', '\\', True);
  StringChangeEx(Result, '"', '\"', True);
  StringChangeEx(Result, Chr(13), '\r', True);
  StringChangeEx(Result, Chr(10), '\n', True);
  StringChangeEx(Result, Chr(9), '\t', True);
end;

function ExistingConfigurationWillBeReplaced: Boolean;
begin
  Result := Assigned(ExistingPage) and ExistingPage.Values[0];
end;

function ShouldCreateConfiguration: Boolean;
begin
  Result := (not FileExists(ConfigPath)) or ExistingConfigurationWillBeReplaced;
end;

procedure InitializeWizard;
begin
  ExistingPage := CreateInputOptionPage(wpSelectDir,
    'Existing configuration',
    'Choose how an existing proxy configuration is handled.',
    'An upgrade preserves the installed config.json by default. Select the option below only if you want to replace it with values entered in this installer.',
    True, False);
  ExistingPage.Add('Replace my existing config.json');
  ExistingPage.Values[0] := False;

  ConfigPage := CreateInputQueryPage(ExistingPage.ID,
    'Proxy configuration',
    'Enter the SOCKS5 proxy used by the service.',
    'The server and ports are required. Username and password are optional, but must either both be filled or both be empty. The password is not written to the installer log.');
  ConfigPage.Add('Proxy type:', False);
  ConfigPage.Add('Proxy version:', False);
  ConfigPage.Add('Proxy server hostname or IP address:', False);
  ConfigPage.Add('Proxy server port:', False);
  ConfigPage.Add('Reserved/local port:', False);
  ConfigPage.Add('Username (optional):', False);
  ConfigPage.Add('Password (optional):', True);
  ConfigPage.Values[0] := 'socks';
  ConfigPage.Values[1] := '5';
  ConfigPage.Values[2] := '';
  ConfigPage.Values[3] := '1080';
  ConfigPage.Values[4] := '11200';
  ConfigPage.Values[5] := '';
  ConfigPage.Values[6] := '';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = ExistingPage.ID then
    Result := not FileExists(ConfigPath)
  else if PageID = ConfigPage.ID then
    Result := not ShouldCreateConfiguration;
end;

function ValidPort(Value: String): Boolean;
var
  Port: Integer;
begin
  Port := StrToIntDef(Trim(Value), 0);
  Result := (Port >= 1) and (Port <= 65535);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID <> ConfigPage.ID then Exit;
  if CompareText(Trim(ConfigPage.Values[0]), 'socks') <> 0 then begin
    MsgBox('Proxy type must be socks.', mbError, MB_OK); Result := False; Exit;
  end;
  if Trim(ConfigPage.Values[1]) <> '5' then begin
    MsgBox('Proxy version must be 5.', mbError, MB_OK); Result := False; Exit;
  end;
  if Trim(ConfigPage.Values[2]) = '' then begin
    MsgBox('Enter a proxy server hostname or IP address.', mbError, MB_OK); Result := False; Exit;
  end;
  if not ValidPort(ConfigPage.Values[3]) then begin
    MsgBox('Proxy server port must be a number from 1 to 65535.', mbError, MB_OK); Result := False; Exit;
  end;
  if not ValidPort(ConfigPage.Values[4]) then begin
    MsgBox('Reserved/local port must be a number from 1 to 65535.', mbError, MB_OK); Result := False; Exit;
  end;
  if StrToIntDef(ConfigPage.Values[3], 0) = StrToIntDef(ConfigPage.Values[4], 0) then begin
    MsgBox('Reserved/local port must differ from the proxy server port.', mbError, MB_OK); Result := False; Exit;
  end;
  if (Trim(ConfigPage.Values[5]) = '') <> (ConfigPage.Values[6] = '') then begin
    MsgBox('Username and password must either both be filled or both be empty.', mbError, MB_OK); Result := False; Exit;
  end;
end;

procedure WriteConfiguration;
var
  Content: String;
begin
  if not ShouldCreateConfiguration then Exit;
  ConfigurationExisted := FileExists(ConfigPath);
  if ConfigurationExisted and (not LoadStringFromFile(ConfigPath, PreviousConfiguration)) then
    RaiseException('The existing config.json could not be read, so it was not replaced.');
  Content := '{' + #13#10 +
    '  "type": "' + JsonEscape(Trim(ConfigPage.Values[0])) + '",' + #13#10 +
    '  "version": "' + JsonEscape(Trim(ConfigPage.Values[1])) + '",' + #13#10 +
    '  "server": "' + JsonEscape(Trim(ConfigPage.Values[2])) + '",' + #13#10 +
    '  "server_port": ' + Trim(ConfigPage.Values[3]) + ',' + #13#10 +
    '  "reserve_port": ' + Trim(ConfigPage.Values[4]) + ',' + #13#10 +
    '  "username": "' + JsonEscape(ConfigPage.Values[5]) + '",' + #13#10 +
    '  "password": "' + JsonEscape(ConfigPage.Values[6]) + '"' + #13#10 + '}' + #13#10;
  if not SaveStringToFile(ConfigPath, Content, False) then
    RaiseException('Windows could not create config.json in the installation directory.');
  ConfigurationCreated := True;
end;

procedure RestoreConfigurationAfterFailure;
begin
  if not ConfigurationCreated then Exit;
  if ConfigurationExisted then
    SaveStringToFile(ConfigPath, PreviousConfiguration, False)
  else
    DeleteFile(ConfigPath);
end;

function RunSc(Parameters: String; var ResultCode: Integer): Boolean;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function ServiceExists: Boolean;
var
  ResultCode: Integer;
begin
  Result := RunSc('query {#MyServiceName}', ResultCode) and (ResultCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  ServicePreExisting := ServiceExists;
  if ServicePreExisting then begin
    if (not RunSc('stop {#MyServiceName}', ResultCode)) or ((ResultCode <> 0) and (ResultCode <> 1062)) then
      Result := 'The existing IPRoyal service could not be stopped. Close service-management tools and try again.'
    else
      Sleep(12000);
  end;
end;

procedure SecureConfiguration;
var
  ResultCode: Integer;
  Parameters: String;
begin
  Parameters := '"' + ConfigPath + '" /inheritance:r /grant:r *S-1-5-18:F *S-1-5-32-544:F';
  if (not Exec(ExpandConstant('{sys}\icacls.exe'), Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode)) or (ResultCode <> 0) then
    RaiseException('Windows could not secure config.json for Administrators and SYSTEM.');
end;

procedure RegisterAndStartService;
var
  ResultCode: Integer;
  Parameters: String;
begin
  if ServiceExists then
    Parameters := 'config {#MyServiceName} binPath= "' + ExpandConstant('{app}\{#MyAppExeName}') + '" start= auto DisplayName= "{#MyAppName}"'
  else
    Parameters := 'create {#MyServiceName} binPath= "' + ExpandConstant('{app}\{#MyAppExeName}') + '" start= auto DisplayName= "{#MyAppName}"';
  if (not RunSc(Parameters, ResultCode)) or (ResultCode <> 0) then
    RaiseException('Windows could not register the service.');
  RunSc('description {#MyServiceName} "System-wide SOCKS5 enforcement with strict TUN routing and RDP exemption."', ResultCode);
  RunSc('failure {#MyServiceName} reset= 86400 actions= restart/5000/restart/15000/restart/60000', ResultCode);
  RunSc('failureflag {#MyServiceName} 1', ResultCode);
  if (not RunSc('start {#MyServiceName}', ResultCode)) or (ResultCode <> 0) then begin
    if not ServicePreExisting then RunSc('delete {#MyServiceName}', ResultCode);
    RaiseException('The service was installed but could not start. Check the proxy values and try the installer again.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorMessage: String;
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then begin
    try
      WriteConfiguration;
      SecureConfiguration;
      RegisterAndStartService;
    except
      ErrorMessage := GetExceptionMessage;
      RestoreConfigurationAfterFailure;
      if ServicePreExisting then RunSc('start {#MyServiceName}', ResultCode);
      RaiseException(ErrorMessage);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then begin
    if ServiceExists then begin
      RunSc('stop {#MyServiceName}', ResultCode);
      Sleep(12000);
      RunSc('delete {#MyServiceName}', ResultCode);
    end;
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
end;
