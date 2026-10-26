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
OutputBaseFilename=DevsFingerPrint_Control_de_Accesos_Setup_v1.0.2
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
Filename: "{tmp}\UareU_RTE\setup.exe"; StatusMsg: "Instalando el runtime oficial del lector DigitalPersona..."; Flags: waituntilterminated; Check: not IsUareURTEInstalled
Filename: "schtasks.exe"; Parameters: "/create /tn ""DevsFingerPrintControldeAccesos"" /tr ""'{app}\DevsFingerPrint.exe'"" /sc ONLOGON /rl HIGHEST /f"; StatusMsg: "Configurando inicio automático persistente..."; Flags: runhidden; Check: IsUareURTEInstalled
Filename: "{app}\DevsFingerPrint.exe"; Description: "Iniciar Control de Accesos"; Flags: nowait postinstall skipifsilent; Check: IsUareURTEInstalled

[UninstallRun]
; Elimina la Tarea Programada limpiamente al desinstalar
Filename: "schtasks.exe"; Parameters: "/delete /tn ""DevsFingerPrintControldeAccesos"" /f"; Flags: runhidden; RunOnceId: "DeleteTask"

[Icons]
Name: "{group}\Control de Accesos"; Filename: "{app}\DevsFingerPrint.exe"; IconFilename: "{app}\DevsFingerPrint.exe"
Name: "{autodesktop}\Control de Accesos"; Filename: "{app}\DevsFingerPrint.exe"; IconFilename: "{app}\DevsFingerPrint.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Code]
function IsUareURTEInstalled: Boolean;
begin
  Result := RegKeyExists(
    HKLM64,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{410EB00B-D5D8-49E8-AAB8-05A0C553DBA0}');
end;
