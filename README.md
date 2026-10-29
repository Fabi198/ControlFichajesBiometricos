# 👆 Control de Fichajes Biométricos (DevsFingerPrint)

![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![Windows Forms](https://img.shields.io/badge/Windows%20Forms-WinForms-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
**DevsFingerPrint** es una aplicación de escritorio cliente desarrollada en C# y Windows Forms diseñada para la gestión, captura e identificación de huellas dactilares para el control de asistencia y fichadas de empleados en tiempo real. 

El sistema está optimizado para funcionar en segundo plano mediante la bandeja del sistema (System Tray), sincronizar datos localmente y comunicarse con APIs REST externas.

---

## 🌟 Características Principales

* 🔒 **Autenticación e Inicio de Sesión:** Pantalla de Login segura con gestión de credenciales guardadas localmente (`CredentialStorage`).
* 🏢 **Selección de Sucursal y Empresa:** Permite configurar la sucursal activa (`SeleccionSucursalForm`) para el registro correcto de marcaciones.
* ✋ **Enrolamiento de Huellas Dactilares:** Interfaz gráfica interactiva con un selector de dedos (`SelectorDedosControl`) para capturar y enrolar plantillas biométricas por empleado.
* 🎧 **Escucha Biométrica en Segundo Plano:** Módulo `LectorListenerForm` que corre minimizado en la barra de tareas (`MainTrayContext`) procesando fichadas al instante.
* ⚡ **Almacenamiento Local (Offline First):** Integración con **SQLite** (`LocalDatabase`) para garantizar que las fichadas no se pierdan ante cortes de conexión.
* 📡 **Sincronización mediante API REST:** Cliente HTTP (`ApiClient`) conserva las fichadas sin conexión y envía lotes biométricos de hasta 500 eventos; la API determina Entrada/Salida usando el día calendario de cada evento.

---

## 🏗️ Arquitectura del Proyecto

El proyecto sigue una estructura limpia basada en principios de **Clean Architecture / DDD (Domain-Driven Design)** para mantener desacoplada la interfaz de usuario, la lógica de negocio y la infraestructura:

## Pruebas automatizadas

La suite de `DevsFingerPrint.Tests` no necesita un lector físico. Usa una base
SQLite temporal por prueba, un servidor HTTP local falso para validar el
contrato de `ApiClient` y el simulador biométrico solo en configuración Debug.
Incluye persistencia y orden de la cola offline, confirmación exacta del lote,
respuestas malformadas, límite de 500 eventos, enrolamiento/cancelación
simulados y una comprobación de que Release no contiene el servicio de mock.

En Windows, con .NET Framework 4.8 Developer Pack, NuGet y las DLL del SDK
DigitalPersona que requiere el proyecto de escritorio:

```powershell
dotnet test .\DevsFingerPrint.Tests\DevsFingerPrint.Tests.csproj --configuration Debug
dotnet test .\DevsFingerPrint.Tests\DevsFingerPrint.Tests.csproj --configuration Release
```

Release **no incluye el simulador** y utiliza `DigitalPersonaBiometricService`
con el SDK del U.are.U. La captura real, la comparación y el enrolamiento deben
validarse con el lector físico antes de usarla para fichadas operativas. Debug
conserva opciones de simulación para pruebas locales; no deben usarse para
fichadas operativas.

### Preparación de aceptación física e instalación

La versión actual es 1.0.5. El heartbeat espera 60 segundos, el catálogo de empleados y huellas 20 minutos, y la recuperación de fichadas pendientes 60 segundos. Una fichada nueva se envía al guardarse. Un error temporal de red alarga el reintento de ese canal hasta 5 minutos. Esos intervalos no se editan en el equipo. El instalador se compila después de crear
`bin\Release` y requiere Inno Setup 7. La compilación verificada usó la versión 7.1.0. Configura `UAREU_PACKAGE_DIR` para que
apunte al paquete oficial autorizado `U.are.U_Windows_3.2.0.89`, que debe contener `Docs\EULA SDK.rtf` y `RTE\x64`, luego compila
`innoDevs.iss` con `ISCC.exe`. Por ejemplo:

```powershell
$env:UAREU_PACKAGE_DIR = 'C:\ruta\autorizada\U.are.U_Windows_3.2.0.89'
& "$env:ProgramFiles\Inno Setup 7\ISCC.exe" .\innoDevs.iss
```

El instalador muestra la EULA del fabricante y, si no detecta U.are.U RTE x64
3.2.0.89, ejecuta su instalador oficial. No instala las herramientas de
desarrollo del SDK. El paquete de DigitalPersona y su EULA permanecen fuera del
repositorio; solo se deben distribuir con la autorización correspondiente.
También requiere .NET Framework 4.8.

La instalación sobrescribe los binarios de la aplicación en su carpeta de
destino, pero no instala ni elimina archivos de `%AppData%\DevsFingerPrint`.
Las credenciales del agente están protegidas con DPAPI para el usuario actual;
instala y ejecuta la aplicación con la misma cuenta de Windows que guardó las
credenciales. La desinstalación también conserva los datos de AppData.

Antes de la prueba con el U.are.U 4500, confirma que la API de aceptación está
disponible en `http://localhost:5052`, que el túnel de base de datos local está
abierto y que las credenciales corresponden al agente y empleado de pruebas
dedicado de `.devs`. No ejecutes pruebas de escritura con empleados operativos.
