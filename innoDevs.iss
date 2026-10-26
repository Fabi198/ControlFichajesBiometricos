#define DesktopDir GetEnv("USERPROFILE") + "\Desktop"
#define UareUPackageDir GetEnv("UAREU_PACKAGE_DIR")

#if UareUPackageDir == ""
  #error "Set UAREU_PACKAGE_DIR to the authorized U.are.U package directory before compiling."
#endif

[Setup]
AppName=DevsFingerPrint - Control de Accesos
AppVersion=1.0.2
DefaultDirName={autopf}\DevsFingerPrint\Control de Accesos
DefaultGroupName=DevsFingerPrint
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
OutputDir={#DesktopDir}\DevsFingerPrint_Instalador
OutputBaseFilename=DevsFingerPrint_Control_de_Accesos_Setup_v1.0.2-r2
LicenseFile="{#UareUPackageDir}\Docs\EULA SDK.rtf"
Compression=lzma
SolidCompression=yes
CloseApplications=yes
Uninstallable=yes

[Files]
; Relative to this script so the installer can be built from any checkout path.
Source: "bin\Release\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs
; Bundle only the authorized x64 Runtime Environment, not the SDK development tools.
Source: "{#UareUPackageDir}\RTE\x64\*"; DestDir: "{tmp}\UareU_RTE"; Flags: ignoreversion recursesubdirs createallsubdirs deleteafterinstall

[Run]
; CurStepChanged ejecuta setup.exe en una PC limpia, espera su codigo de salida
; y vuelve a leer el registro. Esta entrada no lo relanza despues de ese paso.
Filename: "{tmp}\UareU_RTE\setup.exe"; StatusMsg: "Instalando el runtime oficial del lector DigitalPersona..."; Flags: waituntilterminated; Check: DebeInstalarRteIncluido
Filename: "schtasks.exe"; Parameters: "/create /tn ""DevsFingerPrintControldeAccesos"" /tr ""'{app}\DevsFingerPrint.exe'"" /sc ONLOGON /rl HIGHEST /f"; StatusMsg: "Configurando inicio automático persistente..."; Flags: runhidden; Check: ComponenteUareUPresente
Filename: "{app}\DevsFingerPrint.exe"; Description: "Iniciar Control de Accesos"; Flags: nowait postinstall skipifsilent; Check: ComponenteUareUPresente

[UninstallRun]
; Elimina la Tarea Programada limpiamente al desinstalar
Filename: "schtasks.exe"; Parameters: "/delete /tn ""DevsFingerPrintControldeAccesos"" /f"; Flags: runhidden; RunOnceId: "DeleteTask"

[Icons]
Name: "{group}\Control de Accesos"; Filename: "{app}\DevsFingerPrint.exe"; IconFilename: "{app}\DevsFingerPrint.exe"
Name: "{autodesktop}\Control de Accesos"; Filename: "{app}\DevsFingerPrint.exe"; IconFilename: "{app}\DevsFingerPrint.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Code]
var
  DeteccionRegistrada: Boolean;
  SdkDetectado: Boolean;
  RuntimeDetectado: Boolean;
  RteEjecutado: Boolean;
  ReconsultaPendiente: Boolean;
  RteCodigo: Integer;

function EsLetra(C: Char): Boolean;
begin
  Result := (C >= 'A') and (C <= 'Z');
end;

function TieneToken(const Nombre, Token: String): Boolean;
var
  Resto: String;
  P, N: Integer;
  Anterior, Siguiente: Char;
begin
  Result := False;
  Resto := Nombre;
  N := Length(Token);
  P := Pos(Token, Resto);
  while P > 0 do
  begin
    if P = 1 then
      Anterior := ' '
    else
      Anterior := Resto[P - 1];
    if P + N > Length(Resto) then
      Siguiente := ' '
    else
      Siguiente := Resto[P + N];
    if (not EsLetra(Anterior)) and (not EsLetra(Siguiente)) then
    begin
      Result := True;
      Exit;
    end;
    Resto := Copy(Resto, P + 1, Length(Resto) - P);
    P := Pos(Token, Resto);
  end;
end;

function ClasificarDisplayName(const DisplayName: String; var EsSdk, EsRuntime: Boolean): Boolean;
var
  Nombre: String;
  EsMarca: Boolean;
begin
  Nombre := Uppercase(DisplayName);
  EsMarca := (Pos('DIGITALPERSONA', Nombre) > 0) or (Pos('U.ARE.U', Nombre) > 0) or TieneToken(Nombre, 'UAREU');
  EsSdk := EsMarca and TieneToken(Nombre, 'SDK');
  EsRuntime := EsMarca and (TieneToken(Nombre, 'RUNTIME') or TieneToken(Nombre, 'RTE'));
  Result := EsSdk or EsRuntime;
end;

function ProductCodeInstalado(const ProductCode: String): Boolean;
var
  Ruta: String;
begin
  Ruta := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + ProductCode;
  Result := RegKeyExists(HKLM64, Ruta) or RegKeyExists(HKLM32, Ruta);
end;

procedure BuscarDisplayNameEnVista(RootKey: Integer; var EncontroSdk, EncontroRuntime: Boolean);
var
  Nombres: TArrayOfString;
  I: Integer;
  DisplayName, Clave: String;
  EsSdk, EsRuntime: Boolean;
begin
  Clave := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall';
  if not RegGetSubkeyNames(RootKey, Clave, Nombres) then
    Exit;
  for I := 0 to GetArrayLength(Nombres) - 1 do
  begin
    DisplayName := '';
    if RegQueryStringValue(RootKey, Clave + '\' + Nombres[I], 'DisplayName', DisplayName) then
    begin
      if ClasificarDisplayName(DisplayName, EsSdk, EsRuntime) then
      begin
        if EsSdk then
          EncontroSdk := True;
        if EsRuntime then
          EncontroRuntime := True;
      end;
    end;
  end;
end;

procedure ActualizarDeteccion(var HaySdk, HayRuntime: Boolean);
begin
  HaySdk := ProductCodeInstalado('{77BDC578-CFA7-446D-AD36-4E75D7E463D2}');
  HayRuntime := ProductCodeInstalado('{410EB00B-D5D8-49E8-AAB8-05A0C553DBA0}');
  BuscarDisplayNameEnVista(HKLM64, HaySdk, HayRuntime);
  BuscarDisplayNameEnVista(HKLM32, HaySdk, HayRuntime);
end;

procedure RegistrarDeteccion;
begin
  if DeteccionRegistrada then
    Exit;
  ActualizarDeteccion(SdkDetectado, RuntimeDetectado);
  DeteccionRegistrada := True;
  if SdkDetectado then
    Log('Se detecto el SDK DigitalPersona U.are.U. No se desinstala ni se reemplaza.');
  if RuntimeDetectado then
    Log('Se detecto un runtime DigitalPersona U.are.U compatible.');
  if SdkDetectado or RuntimeDetectado then
    Log('Se omitira el runtime incluido porque ya existe un componente compatible.')
  else
    Log('No se detecto SDK ni runtime compatible. Se instalara el runtime x64 incluido.');
end;

function ComponenteUareUPresente: Boolean;
var
  HaySdk, HayRuntime: Boolean;
begin
  if ReconsultaPendiente then
  begin
    Log('Se volvio a consultar el registro despues del runtime incluido.');
    ActualizarDeteccion(HaySdk, HayRuntime);
    if HaySdk and not SdkDetectado then
      Log('Se detecto el SDK DigitalPersona U.are.U. No se desinstala ni se reemplaza.');
    if HayRuntime and not RuntimeDetectado then
      Log('Se detecto un runtime DigitalPersona U.are.U compatible.');
    SdkDetectado := HaySdk;
    RuntimeDetectado := HayRuntime;
    DeteccionRegistrada := True;
    ReconsultaPendiente := False;
  end
  else
    RegistrarDeteccion;
  Result := SdkDetectado or RuntimeDetectado;
end;

function DebeInstalarRteIncluido: Boolean;
begin
  if RteEjecutado then
  begin
    Result := False;
    Exit;
  end;
  Result := not ComponenteUareUPresente;
end;

function CodigoRteAceptado(const Codigo: Integer): Boolean;
begin
  Result := (Codigo = 0) or (Codigo = 3010) or (Codigo = 1641);
end;

function EjecutarRteIncluido: Boolean;
var
  Codigo: Integer;
begin
  RteEjecutado := True;
  RteCodigo := -1;
  WizardForm.StatusLabel.Caption := 'Instalando el runtime oficial del lector DigitalPersona...';
  if not Exec(
    ExpandConstant('{tmp}\UareU_RTE\setup.exe'),
    '',
    ExpandConstant('{tmp}\UareU_RTE'),
    SW_SHOW,
    ewWaitUntilTerminated,
    Codigo) then
  begin
    Log('No se pudo iniciar el runtime incluido.');
    ReconsultaPendiente := True;
    Result := False;
    Exit;
  end;
  RteCodigo := Codigo;
  Log(Format('El runtime incluido finalizo con codigo %d.', [Codigo]));
  if (Codigo = 3010) or (Codigo = 1641) then
    Log('El runtime incluido solicito reiniciar el equipo.');
  ReconsultaPendiente := True;
  Result := CodigoRteAceptado(Codigo);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  CodigoAceptado, Detectado: Boolean;
begin
  if CurStep <> ssPostInstall then
    Exit;
  if not DebeInstalarRteIncluido then
    Exit;
  CodigoAceptado := EjecutarRteIncluido;
  Detectado := ComponenteUareUPresente;
  if CodigoAceptado and Detectado then
    Exit;
  Log('El runtime incluido no quedo disponible. No se creara la tarea ni se iniciara el agente.');
  if not CodigoAceptado then
    RaiseException('El runtime de DigitalPersona U.are.U no pudo instalarse. La instalacion del agente no se completo.')
  else
    RaiseException('El runtime de DigitalPersona U.are.U finalizo, pero no quedo detectado. La instalacion del agente no se completo.');
end;
