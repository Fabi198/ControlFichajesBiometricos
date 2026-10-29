# Pendientes

## Publicación

El working tree de `fix/agente-conectividad-produccion` no está commitado ni enviado. La publicación prevista es una rama y un pull request hacia `origin` (`Fabi198/ControlFichajesBiometricos`), sin reescribir historia y sin force push. No está hecha.

## Operación

- Probar fichada, enrolamiento y reconexión con el lector U.are.U 4500 en Release.
- Probar la cola offline contra la API que vaya a recibir las fichadas, sin asumir que `recibidas` signifique lo mismo que en el código local de la API si producción difiere.
- El instalador 1.0.5 se compiló en esta PC con Inno Setup 7 y `UAREU_PACKAGE_DIR` apuntando al SDK local. No se instaló ni se publicó. El ejemplo del README todavía nombra la ruta de Inno Setup 6.

## Mejora futura: configuración remota de intervalos

Hoy los intervalos están fijos en el ejecutable. Más adelante el módulo de agentes del panel web puede editarlos, la API puede persistirlos por agente y el agente puede recibirlos en el heartbeat HTTPS que ya existe.

Para esa mejora, todavía sin implementar:

- No hace falta un formulario local. El archivo de intervalos en el equipo, si vuelve a existir, sería solo la última copia válida recibida del servidor.
- La API no clasifica conectado o desconectado. Guarda `UltimoAcceso` en el login y en el heartbeat.
- El panel considera conectado un último contacto de menos de 60 minutos, sin actividad reciente desde los 60 y desconectado desde los 120. Ese cálculo es fijo y no lee el intervalo configurado.
- Mientras esa ventana siga igual, el heartbeat configurable no debería superar 15 minutos: un latido perdido todavía queda debajo de los 60 minutos.
- Una respuesta de heartbeat que agregue la configuración, sin dejar de incluir el cuerpo actual, es compatible con los agentes que solo comprueban que la respuesta no venga vacía.
- El POST con `{}` tiene que seguir siendo válido para los agentes ya instalados.
- Hace falta un número de versión por agente. El programa aplica una versión mayor, ignora una menor o inválida y no borra la última configuración válida si se corta la red.
- El cambio de intervalo se aplica sin reiniciar el proceso y sin descartar la cola SQLite. El backoff de cada canal se conserva hasta que ese canal se recupere.

No se crean endpoints ni pantallas en esta versión.

## Documentación de los otros repositorios

Las guías del frontend y de la API no se actualizaron. Siguen en sus propios clones. Un hecho que también deba figurar ahí se escribe en esos repositorios solo cuando se pida.
