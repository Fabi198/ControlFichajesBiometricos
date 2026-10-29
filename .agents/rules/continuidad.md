---
trigger: always_on
description: Consultar y mantener las guías de arquitectura, estado, pendientes y decisiones del agente biométrico antes de cambiar comportamiento.
---

# Continuidad del agente biométrico

`AGENTS.md` es la instrucción de este repositorio. Las guías viven en un solo lugar:

- @../../docs/ARQUITECTURA.md
- @../../docs/ESTADO_ACTUAL.md
- @../../docs/PENDIENTES.md
- @../../docs/DECISIONES_TECNICAS.md

Antes de modificar comportamiento, leer esos cuatro archivos. No copiar su contenido a esta regla.

El frontend y la API tienen documentación propia. No editarla desde este repositorio salvo pedido explícito.

Al cerrar un cambio verificado, actualizar el documento que corresponda:

- comportamiento nuevo o corregido con evidencia: `docs/ESTADO_ACTUAL.md`
- tarea abierta o fallo reproducido: `docs/PENDIENTES.md`
- elección que haya que respetar después: `docs/DECISIONES_TECNICAS.md`

No anotar secretos, tokens, `clientSecret` ni plantillas biométricas. No dar por publicado un cambio que no se haya enviado al remoto.
