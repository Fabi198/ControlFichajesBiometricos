# DevsFingerPrint — Control de Accesos

## Propósito

DevsFingerPrint es un agente de escritorio para Windows que identifica empleados mediante un lector de huellas DigitalPersona U.are.U y registra fichadas de asistencia. La aplicación permanece en segundo plano en el área de notificación de Windows, conserva datos localmente y sincroniza con la API configurada.

Esta documentación describe el código de este repositorio. No sustituye las instrucciones operativas de la API ni del panel web, que pertenecen a otros proyectos.

## A quién está dirigida

- **Operadores:** inicio de sesión, estado del agente, lector, enrolamiento y diagnóstico básico.
- **Desarrollo y QA:** estructura, configuración de compilación y pruebas automatizadas.
- **Instalación y soporte:** requisitos locales, persistencia y límites de la versión.

## Funciones

- Vincula la terminal con credenciales de agente de sucursal.
- Mantiene un catálogo local de empleados y plantillas biométricas.
- Lee y compara huellas con el servicio biométrico elegido al compilar.
- Registra fichadas primero en SQLite y las envía a la API en segundo plano.
- Conserva las fichadas pendientes cuando no se confirma su recepción.
- Permite enrolar una huella para un empleado cuando hay sesión y acceso a la API.
- Muestra el estado, la última sincronización, el estado del lector y la carpeta de logs en el menú de bandeja.

## Límites importantes

- La aplicación no configura los intervalos de comunicación desde la interfaz.
- El tipo Entrada/Salida se calcula localmente. El lote enviado a la API contiene `empleadoId` y `fechaHora`; no se debe asumir que incluye el tipo.
- Debug usa un lector simulado y ofrece funciones de simulación. No debe usarse para registrar asistencia operativa.
- Release usa DigitalPersona. Su captura y enrolamiento reales requieren validación con el lector y el runtime autorizados.
- El repositorio no demuestra que las plantillas biométricas guardadas en SQLite estén cifradas. Deben tratarse como datos sensibles.

## Identificación de versión del repositorio

El ensamblado fuente declara **1.0.6.0**. El script de Inno Setup declara **1.0.7** para el instalador. Son valores distintos: verificar y alinear ambos antes de publicar o distribuir un paquete; no asumir que el instalador corresponde a la versión del ejecutable.

## Mapa de esta documentación

- [[02-Architecture|Arquitectura y flujo de datos]]
- [[03-Components|Componentes del código]]
- [[04-Configuration|Configuración, datos locales e instalación]]
- [[05-Operacion-y-soporte|Guía de operación y soporte]]
- [[06-Desarrollo-y-pruebas|Desarrollo, compilación y pruebas]]

## Fuente de verdad

La descripción se preparó contrastando el código actual, los proyectos de pruebas y los archivos de compilación del repositorio. Si esta guía y la implementación difieren, prevalece el código y se debe actualizar la documentación junto con el cambio.
