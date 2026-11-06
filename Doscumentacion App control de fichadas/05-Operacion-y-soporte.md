# Guía de operación y soporte

## Inicio y vinculación

1. Iniciar **Control de Accesos** desde el acceso directo instalado.
2. Si solicita acceso, ingresar el `Client ID` y `Client Secret` provistos para el agente de la terminal y seleccionar **Vincular Terminal**.
3. Al autenticarse, la aplicación guarda las credenciales protegidas para el usuario actual de Windows, intenta sincronizar la sucursal y pasa a la bandeja del sistema.
4. Dejar el proceso activo. Cerrar la ventana inicial no equivale a salir del agente; las operaciones se administran desde el icono de bandeja.

Si el acceso no está disponible, el agente puede permanecer abierto sin sesión. La lectura local puede continuar si ya existe un catálogo válido, pero las operaciones que requieren API —por ejemplo, enrolar— necesitan conectividad y una sesión válida.

## Menú de bandeja

El menú presenta el estado, la última sincronización y estas acciones:

- **Reintentar conexión:** solicita un intento de recuperación de conexión.
- **Vincular o cambiar credenciales:** vuelve a abrir el acceso. Al completar la vinculación, se reanudan heartbeat, catálogo y envío pendiente.
- **Abrir carpeta de logs:** abre los logs diarios del usuario.
- **Versión:** muestra la versión del ensamblado en ejecución.
- **Enrolar nueva huella:** abre el flujo de enrolamiento con empleados obtenidos de la API.
- **Estado del lector biométrico:** muestra información del lector, catálogo, fichadas pendientes y estado de API.
- **Salir:** cierra el agente.

Debug agrega opciones marcadas como simulación/mock. No usar esas opciones para asistencia real.

## Estados visibles

| Estado | Interpretación práctica |
|---|---|
| Conectado | API y lector disponibles. |
| Sin conexión | La API no está disponible o no hay una sesión activa utilizable. |
| Credenciales no válidas | La autenticación fue rechazada; volver a vincular credenciales válidas. |
| Lector desconectado | No hay un lector disponible. El agente seguirá intentando recuperarlo. |
| Sincronizando | Hay una operación de sincronización en curso. |

El estado de bandeja es una indicación operativa, no una confirmación de que una ficha concreta fue recibida por el servidor. Consultar el contador de pendientes desde **Estado del lector biométrico**.

## Enrolamiento

1. Confirmar que el agente tiene sesión y conectividad.
2. Abrir **Enrolar nueva huella**.
3. Seleccionar el empleado y el dedo solicitado en el formulario.
4. Seguir las indicaciones y completar las capturas requeridas.
5. El agente envía la plantilla a la API; si la operación se confirma, actualiza el catálogo local.

La interfaz requiere cuatro capturas para completar la secuencia. La cantidad de empleados disponibles depende del resultado de la API; el enrolamiento no es una operación offline.

## Fichadas y pérdida de conectividad

- Una coincidencia reconocida se guarda primero en SQLite.
- Una marcación reciente del mismo empleado, dentro de cinco minutos, se rechaza como duplicada.
- El agente intenta enviar una ficha nueva inmediatamente, sin esperar al próximo ciclo de recuperación.
- Si no hay conectividad o la API no confirma todo el lote, las fichadas siguen pendientes y se reintentan según la política del canal.
- No borrar la base ni volver a enrolar para corregir fichadas pendientes. Esperar la recuperación de red o escalar a soporte.

Los intervalos normales son heartbeat cada 60 segundos, catálogo cada 20 minutos y recuperación de pendientes cada 60 segundos. El operador no puede cambiarlos desde la aplicación.

## Problemas frecuentes

### El agente muestra credenciales no válidas

Seleccionar **Vincular o cambiar credenciales** y usar credenciales vigentes emitidas para esa terminal. Si continúa el rechazo, solicitar revisión a quien administra los agentes; no editar archivos locales.

### El lector aparece desconectado

Comprobar físicamente el USB y que Windows reconozca el dispositivo. Para Release, confirmar el runtime/driver DigitalPersona aprobado y volver a revisar el estado. El agente intenta reconectarse automáticamente; si no se recupera, conservar los logs y escalar.

### Hay fichadas pendientes

Verificar conectividad y estado de la API. El agente mantiene las fichadas localmente mientras no se confirme su recepción. No asumir que una respuesta parcial fue aceptada: el lote solo se considera confirmado si `recibidas` coincide con su tamaño.

### El catálogo no se actualiza

Revisar el estado de conexión y la última sincronización. El reemplazo local requiere respuestas válidas tanto de empleados como de huellas; ante un fallo se conserva el catálogo existente.

### La aplicación no muestra icono o no inicia correctamente

Confirmar que no haya otra instancia activa, revisar la carpeta de logs desde el menú si está disponible y contactar a soporte con el mensaje observado. No enviar credenciales, plantillas biométricas ni la base de datos.

## Escalamiento seguro

Al escalar, informar versión mostrada, fecha y hora, estado del menú, modelo/estado del lector y pasos realizados. Adjuntar solo fragmentos de log necesarios después de inspeccionarlos y retirar datos personales y secretos según la política interna.
