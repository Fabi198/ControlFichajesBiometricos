---
name: traspaso-computadora
description: Recupera o deja listo el contexto del agente biométrico DevsFingerPrint al cambiar de computadora. Usar al finalizar una sesión, al empezar en otro equipo, o cuando se pide retomar el desarrollo, revisar pendientes o comprobar rama, commit y cambios sin commit. No hace commit, push, pull, merge, reset ni deploy por su cuenta.
---

# Traspaso entre computadoras

Este procedimiento es del repositorio del agente biométrico. El frontend y la API, si están al lado, son otros clones y no se modifican desde aquí.

Hay dos momentos. Elegir uno y no mezclar sus resultados.

## Al finalizar una sesión

1. Leer `AGENTS.md`, `docs/ESTADO_ACTUAL.md` y `docs/PENDIENTES.md`.
2. Registrar solo hechos comprobados en esa sesión: comandos ejecutados, archivos vistos y resultados. No marcar un problema como resuelto sin esa evidencia.
3. Actualizar la guía de este repositorio que corresponda. No propagar el cambio al frontend ni a la API salvo pedido explícito.
4. Correr la verificación de Git de abajo. El informe final lista los archivos sin commit, incluidos los de documentación.
5. Parar antes de commit, push, pull, merge, reset o deploy.

## Al iniciar en otra computadora

1. Confirmar que este clon existe. Si falta, decirlo y no inventar su estado.
2. Leer `AGENTS.md` y, en este orden, `docs/ARQUITECTURA.md`, `docs/ESTADO_ACTUAL.md`, `docs/PENDIENTES.md`, `docs/DECISIONES_TECNICAS.md`.
3. Correr la verificación de Git antes de modificar código.
4. Resumir rama, último commit, diferencia con el remoto, cambios sin commit y pendientes abiertos. Después seguir el pedido del usuario.

## Verificación de Git

Dentro de este clon, sin cambiar el repositorio:

```text
git status -sb
git branch --show-current
git log -1 --format="%H%n%s"
git rev-parse HEAD
git rev-parse --abbrev-ref --symbolic-full-name @{upstream}
git status --short
git remote -v
```

Si no hay upstream, decirlo. No ejecutar `git fetch`, `git pull`, `git merge` ni `git reset` para ponerse al día.

Clasificar la salida de `git status --short`:

- ` M` o `M `: modificado
- `??`: sin seguimiento
- `A`, `D`, `R`: ya preparado en el índice

Esos archivos son trabajo que todavía no está en el remoto. Nombrarlos en el resumen.

## Sincronización documental

Actualizar una guía solo si el dato salió de un comando, un archivo leído o un cambio hecho en la sesión. No borrar pendientes ni decisiones porque una sesión haya sido más corta.

## Seguridad

No leer, copiar ni pegar en las guías:

- `%AppData%\DevsFingerPrint\agent-credentials.dat` y `device-config.dat`
- `%ProgramData%\ControlFichajes\fichajes_local.db`
- `clientSecret`, tokens, connection strings y claves privadas
- plantillas o imágenes biométricas
- el paquete U.are.U y su EULA cuando no estén autorizados para copiarse

No proponer sincronizar esos archivos entre computadoras.

## Operaciones que esperan autorización

No ejecutar, aunque el working tree esté sucio o la rama esté detrás del remoto:

- `git commit`, `git push`, `git pull`, `git fetch`
- `git merge`, `git rebase`, `git reset`
- compilar el instalador para distribuirlo, o deploy

Si hace falta alguna, pedir confirmación y nombrar el comando.

## Informe

Cerrar con:

- momento: cierre de sesión o inicio en otro equipo
- rama, hash, asunto del último commit, upstream y archivos modificados, nuevos o sin commit
- pendientes que sigan abiertos según `docs/PENDIENTES.md`
- operaciones que no se ejecutaron
