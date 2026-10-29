#define DesktopDir GetEnv("USERPROFILE") + "\Desktop"
#define UareUPackageDir GetEnv("UAREU_PACKAGE_DIR")

#if UareUPackageDir == ""
  #error "Set UAREU_PACKAGE_DIR to the authorized U.are.U package directory before compiling."
#endif

[Setup]
AppName=DevsFingerPrint - Control de Accesos
AppVersion=1.0.5
DefaultDirName={autopf}\DevsFingerPrint\Control de Accesos
DefaultGroupName=DevsFingerPrint
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
OutputDir={#DesktopDir}\DevsFingerPrint_Instalador
OutputBaseFilename=DevsFingerPrint_Control_de_Accesos_Setup_v1.0.5
LicenseFile="{#UareUPackageDir}\Docs\EULA SDK.rtf"
Compression=lzma
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
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
; La tarea y la unica instancia del agente se crean en CurStepChanged(ssPostInstall).

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
  DesvincularConfirmado: Boolean;
  AgentePreparado: Boolean;
  TareaConsultada: Boolean;
  TareaExistia: Boolean;
  TareaDeshabilitada: Boolean;
  AgenteEstabaEnEjecucion: Boolean;
  CopiaIniciada: Boolean;
  CopiaFinalizada: Boolean;
  InstalacionExitosa: Boolean;
  AgenteIniciado: Boolean;

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

function EjecutarCodigo(const Archivo, Parametros, Descripcion: String; var Codigo: Integer): Boolean;
begin
  Codigo := -1;
  Result := Exec(Archivo, Parametros, '', SW_HIDE, ewWaitUntilTerminated, Codigo);
  if Result then
    Log(Format('%s finalizo con codigo %d.', [Descripcion, Codigo]))
  else
    Log('No se pudo iniciar ' + Descripcion + '.');
end;

function AgenteEnEjecucion: Boolean;
var
  Ruta: String;
  Texto: AnsiString;
  Codigo: Integer;
begin
  Result := False;
  Ruta := ExpandConstant('{tmp}\control-fichajes-proceso.txt');
  if not EjecutarCodigo(
    ExpandConstant('{sys}\cmd.exe'),
    '/C tasklist /FI "IMAGENAME eq DevsFingerPrint.exe" /NH /FO CSV > "' + Ruta + '"',
    'tasklist DevsFingerPrint.exe',
    Codigo) then
  begin
    Log('No se pudo verificar el proceso. Se considera que sigue activo.');
    Result := True;
    Exit;
  end;
  if not LoadStringFromFile(Ruta, Texto) then
  begin
    DeleteFile(Ruta);
    Log('No se pudo leer la verificacion del proceso. Se considera que sigue activo.');
    Result := True;
    Exit;
  end;
  DeleteFile(Ruta);
  Result := Pos('DevsFingerPrint.exe', Texto) > 0;
  if Result then
    Log('DevsFingerPrint.exe sigue en ejecucion.')
  else
    Log('DevsFingerPrint.exe no esta en ejecucion.');
end;

function EsperarCierreAgente: Boolean;
var
  Intento: Integer;
begin
  for Intento := 0 to 20 do
  begin
    if Intento > 0 then
      Sleep(500);
    if not AgenteEnEjecucion then
    begin
      Result := True;
      Exit;
    end;
  end;
  Result := False;
  Log('DevsFingerPrint.exe sigue activo despues de 10 segundos.');
end;

procedure MostrarFalloCierreAgente;
var
  Form: TSetupForm;
  Texto: TNewStaticText;
  Aceptar: TNewButton;
begin
  Form := CreateCustomForm(ScaleX(520), ScaleY(210), False, True);
  try
    Form.Caption := 'No se pudo cerrar el agente';
    Texto := TNewStaticText.Create(Form);
    Texto.Parent := Form;
    Texto.Caption :=
      'No se pudo detener Control de Accesos para completar la actualización.' + #13#10 + #13#10 +
      'Cerrá el agente o reiniciá el equipo e intentá nuevamente.' + #13#10 + #13#10 +
      'No se modificaron las credenciales ni las fichadas locales.';
    Texto.Left := ScaleX(16);
    Texto.Top := ScaleY(16);
    Texto.Width := Form.ClientWidth - ScaleX(32);
    Texto.Height := ScaleY(120);
    Texto.AutoSize := False;
    Texto.WordWrap := True;
    Aceptar := TNewButton.Create(Form);
    Aceptar.Parent := Form;
    Aceptar.Caption := 'Aceptar';
    Aceptar.ModalResult := mrOk;
    Aceptar.Default := True;
    Aceptar.Cancel := True;
    Aceptar.Height := ScaleY(23);
    Aceptar.Width := Form.CalculateButtonWidth([Aceptar.Caption]);
    Aceptar.Left := Form.ClientWidth - Aceptar.Width - ScaleX(16);
    Aceptar.Top := Form.ClientHeight - Aceptar.Height - ScaleY(16);
    Form.ActiveControl := Aceptar;
    Form.ShowModal;
  finally
    Form.Free;
  end;
end;

function AsegurarIgnoreNew(const Ruta: String; var Texto: AnsiString): Boolean;
var
  Cuerpo: String;
  Codigo: Integer;
begin
  Result := False;
  Cuerpo := Texto;
  if Pos('<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>', Cuerpo) > 0 then
  begin
    Log('La tarea usa IgnoreNew.');
    Result := True;
    Exit;
  end;
  if Pos('<MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>', Cuerpo) > 0 then
    StringChangeEx(Cuerpo, '<MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>', '<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>', True)
  else if Pos('<MultipleInstancesPolicy>Queue</MultipleInstancesPolicy>', Cuerpo) > 0 then
    StringChangeEx(Cuerpo, '<MultipleInstancesPolicy>Queue</MultipleInstancesPolicy>', '<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>', True)
  else if Pos('<MultipleInstancesPolicy>StopExisting</MultipleInstancesPolicy>', Cuerpo) > 0 then
    StringChangeEx(Cuerpo, '<MultipleInstancesPolicy>StopExisting</MultipleInstancesPolicy>', '<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>', True)
  else
  begin
    Log('La tarea no declara MultipleInstancesPolicy.');
    Exit;
  end;
  Texto := Cuerpo;
  if not SaveStringToFile(Ruta, Texto, False) then
  begin
    Log('No se pudo guardar la tarea con IgnoreNew.');
    Exit;
  end;
  if (not EjecutarCodigo(
    ExpandConstant('{sys}\schtasks.exe'),
    '/Create /F /TN "DevsFingerPrintControldeAccesos" /XML "' + Ruta + '"',
    'schtasks /Create /XML',
    Codigo)) or (Codigo <> 0) then
  begin
    Log('No se pudo reimportar la tarea con IgnoreNew.');
    Exit;
  end;
  Log('La tarea quedo con IgnoreNew.');
  Result := True;
end;

function CrearTareaActual: Boolean;
var
  Codigo: Integer;
  Ruta: String;
  Texto: AnsiString;
begin
  Result := False;
  if not EjecutarCodigo(
    ExpandConstant('{sys}\schtasks.exe'),
    ExpandConstant('/Create /F /TN "DevsFingerPrintControldeAccesos" /TR "''{app}\DevsFingerPrint.exe'' --origen=tarea" /SC ONLOGON /RL HIGHEST'),
    'schtasks /Create',
    Codigo) then
    Exit;
  if Codigo <> 0 then
    Exit;
  Ruta := ExpandConstant('{tmp}\control-fichajes-tarea.xml');
  if not EjecutarCodigo(
    ExpandConstant('{sys}\cmd.exe'),
    '/C schtasks /Query /TN "DevsFingerPrintControldeAccesos" /XML > "' + Ruta + '"',
    'schtasks /Query /XML',
    Codigo) then
    Exit;
  if not LoadStringFromFile(Ruta, Texto) then
  begin
    DeleteFile(Ruta);
    Exit;
  end;
  if not AsegurarIgnoreNew(Ruta, Texto) then
  begin
    DeleteFile(Ruta);
    Exit;
  end;
  DeleteFile(Ruta);
  if Pos('<Enabled>true</Enabled>', Texto) = 0 then
  begin
    Log('La tarea quedo deshabilitada. Se intentara habilitarla.');
    if (not EjecutarCodigo(
      ExpandConstant('{sys}\schtasks.exe'),
      '/Change /TN "DevsFingerPrintControldeAccesos" /ENABLE',
      'schtasks /Change /ENABLE',
      Codigo)) or (Codigo <> 0) then
      Exit;
  end;
  Log('La tarea DevsFingerPrintControldeAccesos quedo creada y habilitada.');
  Result := True;
end;

procedure HabilitarTareaAnterior;
var
  Codigo: Integer;
begin
  if not TareaDeshabilitada then
    Exit;
  if EjecutarCodigo(
    ExpandConstant('{sys}\schtasks.exe'),
    '/Change /TN "DevsFingerPrintControldeAccesos" /ENABLE',
    'schtasks /Change /ENABLE',
    Codigo) and (Codigo = 0) then
  begin
    TareaDeshabilitada := False;
    Log('Se rehabilito la tarea anterior.');
  end
  else
    Log('No se pudo rehabilitar la tarea anterior.');
end;

procedure IniciarAgenteUnaVez;
var
  Codigo: Integer;
  Exe: String;
begin
  if AgenteIniciado then
  begin
    Log('El agente ya fue iniciado. No se abre otra instancia.');
    Exit;
  end;
  if not ComponenteUareUPresente then
  begin
    Log('No hay SDK ni runtime compatible. No se inicia el agente.');
    Exit;
  end;
  Exe := ExpandConstant('{app}\DevsFingerPrint.exe');
  if not FileExists(Exe) then
  begin
    Log('No se inicia el agente porque el ejecutable no existe.');
    Exit;
  end;
  Log('Inicio unico por el ejecutable. La tarea queda para el proximo inicio de sesion y no se dispara ahora.');
  if Exec(Exe, '--origen=instalador', ExpandConstant('{app}'), SW_SHOWNORMAL, ewNoWait, Codigo) then
  begin
    AgenteIniciado := True;
    Log('Se inicio una unica instancia del agente.');
  end
  else
    Log('No se pudo iniciar el agente.');
end;

function PrepararAgenteParaInstalacion: Boolean;
var
  Codigo: Integer;
begin
  if AgentePreparado then
  begin
    Log('El agente ya estaba preparado. No se repite el cierre.');
    Result := True;
    Exit;
  end;

  if not TareaConsultada then
  begin
    TareaConsultada := True;
    if EjecutarCodigo(
      ExpandConstant('{sys}\schtasks.exe'),
      '/Query /TN "DevsFingerPrintControldeAccesos"',
      'schtasks /Query',
      Codigo) and (Codigo = 0) then
      TareaExistia := True
    else
    begin
      TareaExistia := False;
      Log('La tarea no existe o no pudo consultarse. No se considera un error fatal.');
    end;
    if TareaExistia then
      Log('La tarea DevsFingerPrintControldeAccesos existe.')
    else
      Log('La tarea DevsFingerPrintControldeAccesos no existe.');
  end;

  AgenteEstabaEnEjecucion := AgenteEnEjecucion;

  if TareaExistia and (not TareaDeshabilitada) then
  begin
    EjecutarCodigo(
      ExpandConstant('{sys}\schtasks.exe'),
      '/End /TN "DevsFingerPrintControldeAccesos"',
      'schtasks /End',
      Codigo);
    if EjecutarCodigo(
      ExpandConstant('{sys}\schtasks.exe'),
      '/Change /TN "DevsFingerPrintControldeAccesos" /DISABLE',
      'schtasks /Change /DISABLE',
      Codigo) and (Codigo = 0) then
    begin
      TareaDeshabilitada := True;
      Log('La tarea quedo deshabilitada temporalmente.');
    end
    else
      Log('No se pudo deshabilitar la tarea. Se intentara cerrar el proceso igualmente.');
  end;

  EjecutarCodigo(
    ExpandConstant('{sys}\taskkill.exe'),
    '/F /T /IM DevsFingerPrint.exe',
    'taskkill DevsFingerPrint.exe',
    Codigo);

  if not EsperarCierreAgente then
  begin
    HabilitarTareaAnterior;
    Log('No se pudo detener el agente. La instalacion no continua.');
    if not WizardSilent then
      MostrarFalloCierreAgente;
    Result := False;
    Exit;
  end;

  AgentePreparado := True;
  Result := True;
end;

procedure RestaurarSiNoHuboCopia;
begin
  if InstalacionExitosa or (not AgentePreparado) then
    Exit;
  if CopiaIniciada and (not CopiaFinalizada) then
  begin
    Log('La copia quedo incompleta. No se relanza el agente ni se rehabilita la tarea.');
    Exit;
  end;
  if TareaDeshabilitada then
    HabilitarTareaAnterior;
  if (not CopiaIniciada) and AgenteEstabaEnEjecucion then
    IniciarAgenteUnaVez
  else if CopiaFinalizada then
    Log('La copia termino, pero la instalacion no fue exitosa. No se inicia el agente.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  CodigoAceptado, Detectado: Boolean;
begin
  if CurStep = ssInstall then
  begin
    CopiaIniciada := True;
    Log('Comenzo la copia de archivos.');
    Exit;
  end;
  if CurStep <> ssPostInstall then
    Exit;

  CopiaFinalizada := True;
  if DebeInstalarRteIncluido then
  begin
    CodigoAceptado := EjecutarRteIncluido;
    Detectado := ComponenteUareUPresente;
    if not (CodigoAceptado and Detectado) then
    begin
      Log('El runtime incluido no quedo disponible. No se creara la tarea ni se iniciara el agente.');
      if not CodigoAceptado then
        RaiseException('El runtime de DigitalPersona U.are.U no pudo instalarse. La instalacion del agente no se completo.')
      else
        RaiseException('El runtime de DigitalPersona U.are.U finalizo, pero no quedo detectado. La instalacion del agente no se completo.');
    end;
  end;

  if ComponenteUareUPresente then
  begin
    if not CrearTareaActual then
      RaiseException('No se pudo crear la tarea del agente. La instalacion no se completo.');
    IniciarAgenteUnaVez;
  end
  else
    Log('No hay SDK ni runtime compatible. No se crea la tarea ni se inicia el agente.');
  InstalacionExitosa := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpReady then
    Result := PrepararAgenteParaInstalacion;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if PrepararAgenteParaInstalacion then
    Result := ''
  else
    Result :=
      'No se pudo detener Control de Accesos para completar la actualización.' + #13#10 + #13#10 +
      'Cerrá el agente o reiniciá el equipo e intentá nuevamente.' + #13#10 + #13#10 +
      'No se modificaron las credenciales ni las fichadas locales.';
end;

procedure DeinitializeSetup;
begin
  RestaurarSiNoHuboCopia;
end;

function ConsultarDesvinculacion: Boolean;
var
  Form: TSetupForm;
  Opcion: TNewCheckBox;
  Explicacion: TNewStaticText;
  Continuar: TNewButton;
begin
  Result := False;
  Form := CreateCustomForm(ScaleX(540), ScaleY(220), False, True);
  try
    Form.Caption := 'Desinstalar Control de Accesos';

    Opcion := TNewCheckBox.Create(Form);
    Opcion.Parent := Form;
    Opcion.Caption := 'Eliminar la vinculación y las credenciales guardadas de este equipo';
    Opcion.Checked := False;
    Opcion.Left := ScaleX(16);
    Opcion.Top := ScaleY(18);
    Opcion.Width := Form.ClientWidth - ScaleX(32);
    Opcion.Height := ScaleY(32);

    Explicacion := TNewStaticText.Create(Form);
    Explicacion.Parent := Form;
    Explicacion.Caption := 'Si seleccionás esta opción, se eliminarán las credenciales locales del puesto. Para volver a utilizar el agente será necesario ingresar nuevamente el Client ID y el Client Secret suministrados para este equipo.';
    Explicacion.Left := ScaleX(16);
    Explicacion.Top := ScaleY(58);
    Explicacion.Width := Form.ClientWidth - ScaleX(32);
    Explicacion.Height := ScaleY(96);
    Explicacion.AutoSize := False;
    Explicacion.WordWrap := True;

    Continuar := TNewButton.Create(Form);
    Continuar.Parent := Form;
    Continuar.Caption := 'Continuar';
    Continuar.ModalResult := mrOk;
    Continuar.Default := True;
    Continuar.Height := ScaleY(23);
    Continuar.Width := Form.CalculateButtonWidth([Continuar.Caption]);
    Continuar.Left := Form.ClientWidth - Continuar.Width - ScaleX(16);
    Continuar.Top := Form.ClientHeight - Continuar.Height - ScaleY(16);
    Form.ActiveControl := Continuar;

    if Form.ShowModal = mrOk then
      Result := Opcion.Checked;
  finally
    Form.Free;
  end;
end;

function ConfirmarDesvinculacion: Boolean;
var
  Form: TSetupForm;
  Texto: TNewStaticText;
  Confirmar, Volver: TNewButton;
  Ancho: Integer;
begin
  Result := False;
  Form := CreateCustomForm(ScaleX(640), ScaleY(250), False, True);
  try
    Form.Caption := 'Desvincular este equipo';

    Texto := TNewStaticText.Create(Form);
    Texto.Parent := Form;
    Texto.Caption :=
      'Se eliminarán las credenciales guardadas para este puesto.' + #13#10 + #13#10 +
      'Si volvés a instalar el agente, tendrás que ingresar nuevamente el Client ID y el Client Secret.' + #13#10 + #13#10 +
      'Las fichadas y los registros operativos locales no se eliminarán.' + #13#10 + #13#10 +
      '¿Querés continuar?';
    Texto.Left := ScaleX(16);
    Texto.Top := ScaleY(16);
    Texto.Width := Form.ClientWidth - ScaleX(32);
    Texto.Height := ScaleY(160);
    Texto.AutoSize := False;
    Texto.WordWrap := True;

    Confirmar := TNewButton.Create(Form);
    Confirmar.Parent := Form;
    Confirmar.Caption := 'Desvincular y desinstalar';
    Confirmar.ModalResult := mrOk;
    Confirmar.Height := ScaleY(23);

    Volver := TNewButton.Create(Form);
    Volver.Parent := Form;
    Volver.Caption := 'Volver';
    Volver.ModalResult := mrCancel;
    Volver.Cancel := True;
    Volver.Default := True;
    Volver.Height := ScaleY(23);

    Ancho := Form.CalculateButtonWidth([Confirmar.Caption, Volver.Caption]);
    Confirmar.Width := Ancho;
    Volver.Width := Ancho;
    Volver.Left := Form.ClientWidth - Ancho - ScaleX(16);
    Confirmar.Left := Volver.Left - Ancho - ScaleX(8);
    Confirmar.Top := Form.ClientHeight - Confirmar.Height - ScaleY(16);
    Volver.Top := Confirmar.Top;
    Form.ActiveControl := Volver;

    Result := Form.ShowModal = mrOk;
  finally
    Form.Free;
  end;
end;

procedure ResolverDesvinculacion;
begin
  DesvincularConfirmado := False;
  if UninstallSilent then
  begin
    Log('Desinstalacion silenciosa: se conserva config.dat.');
    Exit;
  end;
  while True do
  begin
    if not ConsultarDesvinculacion then
    begin
      Log('El usuario conservo la vinculacion. No se eliminara config.dat.');
      Exit;
    end;
    if ConfirmarDesvinculacion then
    begin
      DesvincularConfirmado := True;
      Log('El usuario solicito eliminar la vinculacion.');
      Exit;
    end;
    Log('El usuario volvio a la decision de desvinculacion.');
  end;
end;

procedure EjecutarHerramienta(const Archivo, Parametros, Descripcion: String);
var
  Codigo: Integer;
begin
  if Exec(Archivo, Parametros, '', SW_HIDE, ewWaitUntilTerminated, Codigo) then
    Log(Format('%s finalizo con codigo %d.', [Descripcion, Codigo]))
  else
    Log('No se pudo iniciar ' + Descripcion + '. Se continua.');
end;

procedure DetenerAgente;
begin
  Log('No se desinstala DigitalPersona U.are.U SDK ni el runtime.');
  EjecutarHerramienta(
    ExpandConstant('{sys}\schtasks.exe'),
    '/End /TN "DevsFingerPrintControldeAccesos"',
    'schtasks /End');
  EjecutarHerramienta(
    ExpandConstant('{sys}\schtasks.exe'),
    '/Delete /TN "DevsFingerPrintControldeAccesos" /F',
    'schtasks /Delete');
  EjecutarHerramienta(
    ExpandConstant('{sys}\taskkill.exe'),
    '/F /IM DevsFingerPrint.exe /T',
    'taskkill DevsFingerPrint.exe');
end;

procedure EliminarVinculacion;
var
  Ruta: String;
begin
  Ruta := ExpandConstant('{userappdata}\ControlFichajes\config.dat');
  if not FileExists(Ruta) then
  begin
    Log('config.dat no existia: ' + Ruta);
    Exit;
  end;
  Log('config.dat existia.');
  if DeleteFile(Ruta) then
    Log('config.dat se elimino correctamente.')
  else
  begin
    Log('Fallo la eliminacion de config.dat: ' + Ruta);
    SuppressibleMsgBox(
      'El programa fue desinstalado, pero las credenciales locales no pudieron eliminarse.' + #13#10 + #13#10 +
      'Revisá esta ruta:' + #13#10 + Ruta,
      mbError, MB_OK, IDOK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    usUninstall:
      begin
        ResolverDesvinculacion;
        DetenerAgente;
      end;
    usPostUninstall:
      begin
        if DesvincularConfirmado then
          EliminarVinculacion
        else
          Log('Se conservan config.dat, fichajes_local.db y los logs.');
      end;
  end;
end;
