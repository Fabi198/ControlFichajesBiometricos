# Estado actual

Hechos de la versión 1.0.5 en `fix/agente-conectividad-produccion`. La rama está publicada en el fork `centralmat12/ControlFichajesBiometricos`. El pull request #2 hacia `Fabi198/ControlFichajesBiometricos` está abierto y todavía no tiene merge.

## Comunicación

- La fichada se persiste en SQLite antes de intentar el envío.
- El envío de una fichada nueva se encola al registrarla. No espera al temporizador de 60 segundos.
- Hay un solo envío de fichadas a la vez. El orden y el tope de 500 se mantienen.
- Una cola vacía no hace POST.
- Heartbeat, catálogo y recuperación de fichadas tienen backoff propio, con tope de 5 minutos.
- Un fallo de heartbeat no acorta el catálogo.
- Un fallo de envío no bloquea el lector ni la interfaz de Windows.
- Los intervalos normales son 60 segundos, 20 minutos y 60 segundos. No hay menú para cambiarlos.

## Menú de bandeja

El icono muestra estado, última sincronización, reintentar conexión, vincular credenciales, carpeta de logs, versión, enrolar, estado del lector y salir. Debug agrega la simulación de fichada. No existe la entrada "Comunicación y sincronización...".

## Versión

`AssemblyVersion` y `AssemblyFileVersion` son `1.0.5.0`. El script `innoDevs.iss` declara `AppVersion` 1.0.5 y el nombre de salida `DevsFingerPrint_Control_de_Accesos_Setup_v1.0.5`. Inno Setup 7.1.0 compiló ese instalador en el escritorio de esta PC. No se instaló ni se distribuyó, y el `.exe` no entra al repositorio.

## Remotos

- `origin`: `https://github.com/Fabi198/ControlFichajesBiometricos.git`
- `fork`: `https://github.com/centralmat12/ControlFichajesBiometricos.git`

La rama `fix/agente-conectividad-produccion` está en el fork. El pull request es https://github.com/Fabi198/ControlFichajesBiometricos/pull/2, de `centralmat12:fix/agente-conectividad-produccion` hacia `main` de Fabi. No se hizo merge ni deploy.

## Verificación de esta etapa

`dotnet test` en Debug: 93 correctas, 0 fallidas. `dotnet build` Debug y Release: 0 errores y 0 advertencias. Release generó `bin\Release\DevsFingerPrint.exe`.

## Lo que no se verificó en ejecución real

No se probó el lector U.are.U ni la API productiva. La suite automatizada corre en Debug. Release se compiló como ejecutable de escritorio, sin la suite del simulador.
