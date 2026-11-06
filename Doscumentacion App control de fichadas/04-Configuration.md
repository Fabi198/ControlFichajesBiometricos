# Configuración, datos locales e instalación

## Requisitos

- Windows con .NET Framework 4.8.
- Conectividad HTTPS con la API configurada para vincular la terminal y sincronizar.
- Para uso biométrico operativo: lector DigitalPersona U.are.U compatible, controladores/runtime de DigitalPersona y binario Release.
- Para compilar Release: .NET Framework 4.8 Developer Pack y las bibliotecas administradas del SDK DigitalPersona que referencia el proyecto.

El proyecto apunta a `AnyCPU` y no prefiere 32 bits. El instalador está limitado a Windows x64 y solicita privilegios de administrador.

## Configuración de la API

`App.config` contiene la clave `ApiBaseUrl`. `Program.ResolverApiBaseUrl()` elimina espacios y barras finales; si el valor falta o está vacío, el ejecutable usa `https://controldeaccesos.online`.

```xml
<appSettings>
  <add key="ApiBaseUrl" value="https://controldeaccesos.online" />
</appSettings>
```

Usar únicamente una URL de entorno autorizada. No cambiar contratos HTTP ni la URL productiva como parte de una configuración de desarrollo.

No hay una opción soportada para editar el intervalo de comunicación. Tampoco se lee `FICHADAS_DB`, `DevicePort` ni `LogLevel`; no agregar esas claves como si fueran funciones activas.

## Datos guardados en el equipo

| Dato | Ubicación |
|---|---|
| SQLite: empleados, huellas, fichadas y estado de sincronización | `%ProgramData%\ControlFichajes\fichajes_local.db` |
| Credenciales del agente | `%AppData%\DevsFingerPrint\agent-credentials.dat` |
| Sucursal y serial del lector | `%AppData%\DevsFingerPrint\device-config.dat` |
| Logs operativos diarios | `%LocalAppData%\ControlFichajes\Logs\agente-aaaa-mm-dd.log` |

Los dos archivos de configuración bajo `%AppData%` se protegen con DPAPI y el ámbito `CurrentUser`. Las credenciales no se pueden trasladar a otro usuario de Windows como si fueran texto plano. El catálogo biométrico y las fichadas se guardan en la base SQLite del equipo; el código no demuestra cifrado de esa base. Tratar las plantillas como datos sensibles y restringir el acceso al equipo y a sus copias de seguridad.

No borrar, copiar ni modificar directamente estos archivos para resolver incidencias. Para diagnosticar, utilizar el menú de la aplicación y los procedimientos autorizados de soporte.

## Instalación

`innoDevs.iss` configura un instalador x64 que solicita elevación. Incluye los archivos Release y el runtime x64 del paquete U.are.U autorizado, y puede omitir su ejecución si detecta un SDK o runtime compatible ya instalado. El script presenta la EULA indicada por el paquete.

El instalador conserva los datos del perfil de usuario durante la instalación y desinstalación. Antes de reinstalar o cambiar la cuenta de Windows, coordinar la migración o recuperación de datos con soporte: DPAPI está ligado al usuario actual.

### Compilación del instalador

El script requiere que `UAREU_PACKAGE_DIR` señale al paquete oficial autorizado con `Docs\EULA SDK.rtf` y `RTE\x64`, que exista una salida Release válida en `bin\Release`, y ejecutar `ISCC.exe` de Inno Setup:

```powershell
$env:UAREU_PACKAGE_DIR = 'C:\ruta\autorizada\U.are.U_Windows'
& "$env:ProgramFiles\Inno Setup 7\ISCC.exe" .\innoDevs.iss
```

La versión y licencia del paquete de DigitalPersona deben verificarse con el proveedor antes de distribuir. El SDK de desarrollo no se debe redistribuir como parte de este instalador.

> [!warning] Discrepancia de versión
> `Properties/AssemblyInfo.cs` declara `1.0.6.0`, mientras que `innoDevs.iss` declara `AppVersion=1.0.7` y nombra el instalador como `v1.0.7`. Alinear y verificar esos valores antes de generar o publicar una instalación.

## Diagnóstico sin datos sensibles

El menú **Abrir carpeta de logs** permite localizar registros de operación. Antes de compartirlos, inspeccionarlos y retirar información sensible conforme al procedimiento de la organización. La guía de problemas frecuentes está en [[05-Operacion-y-soporte|Operación y soporte]].
