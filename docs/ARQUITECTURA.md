# Arquitectura del agente biométrico

Aplicación de bandeja para Windows. Ensamblado `DevsFingerPrint`, .NET Framework 4.8, AnyCPU con `Prefer32Bit` en false. La versión de esta línea es 1.0.5.

El proceso arranca en `Program`, resuelve `ApiBaseUrl` desde `App.config` y abre el login. Con sesión válida queda una sola instancia, con mutex, y el icono lo administra `MainTrayContext`.

## Canales con la API

La URL configurada en el repositorio es `https://controldeaccesos.online`. TLS 1.2. El agente no cambia contratos ni el cuerpo de las fichadas.

| Uso | Método y ruta |
| --- | --- |
| Login del agente | `POST /api/auth/agente` |
| Heartbeat | `POST /api/agentes/{id}/heartbeat` con cuerpo `{}` |
| Empleados | `GET /api/empleados` |
| Huellas de la empresa | `GET /api/huellas/empresa/{empresaId}` |
| Fichadas | `POST /api/fichadas/biometricas/bulk` |
| Enrolamiento | `POST /api/empleados/enrolar` |
| Sucursal, solo al vincular | `GET /api/agentes/sucursal` |

Cada fichada enviada lleva `empleadoId` y `fechaHora`. El tipo entrada o salida se calcula en el equipo para la notificación local y no viaja en el POST. Un lote tiene como máximo 500 fichadas, ordenadas por fecha y después por id. Si `recibidas` no coincide con el tamaño del lote, esas filas siguen pendientes.

## Intervalos de la versión 1.0.5

Están fijos en `CicloVidaAgente`. Ningún archivo local los modifica.

- Heartbeat: 60 segundos.
- Catálogo de empleados y huellas: 20 minutos. Un fallo de este canal no lo acorta.
- Recuperación de fichadas pendientes: 60 segundos.
- Fichada nueva: se guarda en SQLite y el envío se encola en ese momento.
- Error temporal de red, timeout, HTTP 429 o HTTP 5xx: el canal afectado pasa a 60, 120, 240 y 300 segundos. El máximo queda en 5 minutos.
- HTTP 401 no es un fallo temporal. Si las credenciales quedan rechazadas, los canales posteriores no vuelven a llamar a la API hasta un vínculo nuevo.

Heartbeat, catálogo y fichadas tienen políticas separadas. El catálogo conserva su intervalo de 20 minutos aunque el reintento propuesto sea menor. El trabajo de red entra por el pool de hilos. El lector y la interfaz no esperan el POST.

Si ya hay un envío de fichadas en curso, el siguiente no abre otra solicitud. Al terminar, el envío activo vuelve a leer la cola. Si el intento falló por red, no reintenta en el mismo ciclo: la ficha sigue en SQLite y la toma el temporizador.

## Datos locales

- SQLite de fichadas, empleados y huellas: `%ProgramData%\ControlFichajes\fichajes_local.db`. Las plantillas quedan en esa base.
- Credenciales del agente: `%AppData%\DevsFingerPrint\agent-credentials.dat`, cifradas con DPAPI del usuario actual.
- Sucursal y serial del lector: `%AppData%\DevsFingerPrint\device-config.dat`. No guarda intervalos.
- Logs: `%LocalAppData%\ControlFichajes\Logs\agente-aaaa-mm-dd.log`. No incluyen el secreto ni el token.

## Lector

Debug compila el simulador `BiometricService`. Release compila `DigitalPersonaBiometricService` y el SDK U.are.U. El instalador Inno Setup es x64, pide administrador y declara `AppVersion` 1.0.5. El paquete del runtime no está en el repositorio.
