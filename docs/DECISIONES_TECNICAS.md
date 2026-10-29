# Decisiones técnicas

Decisiones de la versión 1.0.5. No son propuestas abiertas.

## Intervalos fijos

El agente no ofrece un editor local de comunicación. Heartbeat 60 segundos, catálogo 20 minutos y recuperación de fichadas pendientes 60 segundos. Una fichada nueva se envía al persistirse. El backoff temporal es 60, 120, 240 y 300 segundos, y no pasa de 5 minutos.

Esos valores salen de constantes del programa. `App.config` solo aporta la URL de la API. `device-config.dat` guarda sucursal y serial. `agent-credentials.dat` guarda las credenciales. Ninguno de esos archivos puede cambiar los intervalos.

El menú "Comunicación y sincronización..." y el formulario que lo abría se quitaron en esta versión. También se quitó el servicio que leía `comunicacion-config.json`. Si ese archivo quedó en una PC de prueba, el programa no lo consulta.

Se conserva `PoliticaIntervalo`: cada canal recuerda su intervalo normal, cuenta fallos temporales y vuelve al intervalo normal al recuperarse. El catálogo usa la variante que no baja de sus 20 minutos. `AsignarIntervalo` actualiza un temporizador solo cuando el valor cambió, para no reiniciar la cuenta por un mismo número.

## Contratos

No se modifican la API ni el frontend para esta versión. El heartbeat sigue publicando `{}`. El lote biométrico sigue llevando `empleadoId` y `fechaHora`.

## Un solo ejecutable en la PC

Sigue vigente el mutex de instancia única y la republicación del icono cuando Windows recrea la barra de tareas. Un fallo de autenticación no cierra el proceso: el operador puede volver a vincular credenciales.

## Lector según la configuración de compilación

Debug incluye el simulador y la simulación de fichadas. Release incluye DigitalPersona. No se mezclan en el mismo binario.

## Documentación de continuidad

`AGENTS.md`, las cuatro guías de `docs/`, la regla de Cursor, la regla de Antigravity y la skill `traspaso-computadora` viven en este repositorio. No reemplazan las guías del frontend ni las de la API.

## Configuración remota, más adelante

La administración central de intervalos queda decidida como dirección, no como función de 1.0.5. El diseño de referencia está en `docs/PENDIENTES.md`. Hasta que exista, el panel sigue infiriendo la conexión solo con `UltimoAcceso` y las ventanas de 60 y 120 minutos.
