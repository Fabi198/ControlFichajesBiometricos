# Componentes del código

## Punto de entrada y presentación

| Archivo | Responsabilidad |
|---|---|
| `Program.cs` | Arranque, instancia única, resolución de `ApiBaseUrl`, intento de sesión y ciclo de vida de Windows Forms. |
| `Presentation/FormLogin.cs` | Solicita `Client ID` y `Client Secret`, autentica y guarda las credenciales locales. |
| `Presentation/MainTrayContext.cs` | Coordina el icono y menú de bandeja, lector, estado, sincronización, cola y operaciones del operador. |
| `Presentation/LectorListenerForm.cs` | Ventana oculta que recibe cambios de dispositivo y permite recuperar el icono ante la recreación de la barra de tareas. |
| `Presentation/EnrolarHuellaForm.cs` | Interfaz de enrolamiento: empleado, dedo y progreso de captura. |
| `Presentation/SelectorDedosControl.cs` | Control visual para seleccionar un dedo. |
| `Presentation/SeleccionSucursalForm.cs` | Formulario de selección de sucursal disponible en el código; no es una opción del menú normal de bandeja. |

## Dominio

| Ruta | Responsabilidad |
|---|---|
| `Domain/Models/Empleado.cs` | Datos de empleado utilizados por el catálogo. |
| `Domain/Models/Huella.cs` y `HuellaEnMemoria.cs` | Plantilla persistida y representación preparada para comparación. |
| `Domain/Models/Fichada.cs` | Evento local de asistencia y estado de sincronización. |
| `Domain/Models/Empresa.cs`, `Usuario.cs` | Modelos auxiliares del dominio. |
| `Domain/Interfaces/IBiometricService.cs` | Contrato del servicio de captura e identificación biométrica. |
| `Domain/Interfaces/IFichadaRepository.cs` | Contrato de persistencia de fichadas y catálogos. |
| `Domain/DTO/` | Tipos para las respuestas de catálogo, huella, sucursal y login. |

## Infraestructura

| Archivo | Responsabilidad |
|---|---|
| `Infrastructure/Services/ApiClient.cs` | Autenticación, heartbeat, lecturas de catálogo, envío de lotes y enrolamiento HTTP. |
| `Infrastructure/Data/LocalDatabase.cs` | Inicialización del archivo SQLite y tablas locales. |
| `Infrastructure/Repositories/FichadaRepository.cs` | Persistencia de fichadas, pendientes y catálogo biométrico. |
| `Infrastructure/Services/EnvioFichadasPendientes.cs` | Envío exclusivo, partición en lotes y confirmación de pendientes. |
| `Infrastructure/Services/CatalogoBiometrico.cs` | Validación, compatibilidad y resumen del catálogo cargado. |
| `Infrastructure/Services/FichadaRules.cs` | Regla local de duplicidad y alternancia Entrada/Salida. |
| `Infrastructure/Services/CicloVidaAgente.cs` | Intervalos, estados, reintentos y textos de ciclo de vida. |
| `Infrastructure/Services/CredentialStorage.cs` | Persistencia DPAPI de las credenciales del agente. |
| `Infrastructure/Services/ConfigLocal.cs` | Persistencia DPAPI de sucursal y serial del lector. |
| `Infrastructure/Services/AgentLog.cs` | Registro de eventos operativos en archivos diarios. |
| `Infrastructure/Services/InstanciaUnicaAgente.cs` | Exclusión de instancias simultáneas. |
| `Infrastructure/Services/DigitalPersonaBiometricService.cs` | Adaptador Release para el SDK DigitalPersona U.are.U. |
| `Infrastructure/Services/BiometricService.cs` | Servicio de simulación compilado en Debug. |
| `Infrastructure/Services/SeleccionLector.cs` | Selección y reglas de apertura del lector. |
| `Infrastructure/Services/TemplateBiometricoImporter.cs` | Importación de plantillas biométricas. |

## Selección del servicio biométrico

La selección se hace en compilación:

- **Debug:** `BiometricService`, simulación de captura y herramientas de simulación de fichadas.
- **Release:** `DigitalPersonaBiometricService`, dependiente de las bibliotecas y el runtime autorizados de DigitalPersona.

Los dos servicios no se combinan en un mismo binario.

## Pruebas

`DevsFingerPrint.Tests` verifica reglas de fichadas, persistencia, cliente HTTP con servidor local, almacenamiento de credenciales, coordinación del agente y límites de compilación del lector simulado. Las pruebas no sustituyen la aceptación en un equipo con el lector físico. Véase [[06-Desarrollo-y-pruebas|Desarrollo y pruebas]].
