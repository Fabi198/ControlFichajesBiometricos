# Instrucciones para agentes

Repositorio del agente biométrico de escritorio (`DevsFingerPrint`, WinForms, .NET Framework 4.8). El directorio padre del workspace puede contener el frontend y la API. Esos clones son otros repositorios. No modificarlos desde este.

Antes de cambiar comportamiento, leer en este orden:

1. `docs/ARQUITECTURA.md`
2. `docs/ESTADO_ACTUAL.md`
3. `docs/PENDIENTES.md`
4. `docs/DECISIONES_TECNICAS.md`
5. `README.md`

## Límites

- No modificar producción, no desplegar y no hacer merge a `main` salvo pedido explícito.
- No hacer push, commit ni operaciones destructivas de Git salvo pedido explícito.
- No cambiar contratos HTTP, la URL productiva ni el formato de las fichadas para acompañar un cambio local del agente.
- No ofrecer en esta versión un formulario local que edite los intervalos de comunicación.
- No leer ni copiar secretos, tokens, plantillas biométricas ni el contenido de `%AppData%\DevsFingerPrint` o de la base SQLite real.

## Verificación habitual

```text
dotnet test .\DevsFingerPrint.Tests\DevsFingerPrint.Tests.csproj --configuration Debug
dotnet build .\DevsFingerPrint.csproj --configuration Debug
dotnet build .\DevsFingerPrint.csproj --configuration Release
```

La suite automatizada corresponde a Debug. Release usa el lector DigitalPersona y no incluye el simulador.
