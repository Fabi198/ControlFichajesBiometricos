[Setup]
AppName=DevsFingerPrint - Control de Accesos
AppVersion=1.0.0
DefaultDirName={autopf}\DevsFingerPrint\Control de Accesos
DefaultGroupName=DevsFingerPrint
PrivilegesRequired=admin
OutputDir=userdocs:\DevsFingerPrintOutput
OutputBaseFilename=DevsFingerPrint_Control_de_Accesos_Setup_v1.0.0
Compression=lzma
SolidCompression=yes
CloseApplications=yes

[Files]
; Copia tu app compilada y las DLLs necesarias
Source: "C:\Users\ferre\OneDrive\Documentos\.Devs\App\DevsFingerPrint\bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
; 1. Asegura la habilitación de .NET 3.5 en Windows con DISM
Filename: "dism.exe"; Parameters: "/online /enable-feature /featurename:NetFx3 /all /norestart"; StatusMsg: "Verificando el entorno de ejecución de .NET Framework 3.5..."; Flags: runhidden waituntilterminated

; 2. Crea una Tarea Programada para que la app inicie automáticamente al hacer logon con privilegios altos
Filename: "schtasks.exe"; Parameters: "/create /tn ""DevsFingerPrintControldeAccesos"" /tr ""'{app}\DevsFingerPrint.exe'"" /sc ONLOGON /rl HIGHEST /f"; StatusMsg: "Configurando inicio automático persistente..."; Flags: runhidden

; 3. Inicia la aplicación de inmediato al terminar la instalación
Filename: "{app}\DevsFingerPrint.exe"; Description: "Iniciar Control de Accesos"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Elimina la Tarea Programada limpiamente al desinstalar
Filename: "schtasks.exe"; Parameters: "/delete /tn ""DevsFingerPrintControldeAccesos"" /f"; Flags: runhidden; RunOnceId: "DeleteTask"

[Icons]
Name: "{group}\Control de Accesos"; Filename: "{app}\DevsFingerPrint.exe"; IconFilename: "{app}\DevsFingerPrint.exe"
Name: "{autodesktop}\Control de Accesos"; Filename: "{app}\DevsFingerPrint.exe"; IconFilename: "{app}\DevsFingerPrint.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[UninstallDelete]
; Borra la carpeta de configuración y credenciales de AppData al desinstalar
Type: filesandordirs; Name: "{userappdata}\DevsFingerPrint"