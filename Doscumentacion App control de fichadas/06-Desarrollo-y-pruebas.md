# Desarrollo, compilación y pruebas

## Requisitos de desarrollo

- Windows y Visual Studio o MSBuild compatible con proyectos .NET Framework.
- .NET Framework 4.8 Developer Pack.
- Paquetes NuGet del proyecto.
- Para compilar Release, referencias administradas del SDK DigitalPersona instaladas en la ruta referenciada por `DevsFingerPrint.csproj`.
- Para compilar el instalador, Inno Setup y el paquete de runtime U.are.U autorizado; véase [[04-Configuration|Configuración]].

El proyecto es WinForms, `WinExe`, `AnyCPU`, .NET Framework 4.8. Debug define `DEBUG` y compila el simulador; Release no lo compila y utiliza DigitalPersona.

## Restaurar, compilar y probar

Desde la raíz del repositorio, en PowerShell:

```powershell
dotnet test .\DevsFingerPrint.Tests\DevsFingerPrint.Tests.csproj --configuration Debug
dotnet build .\DevsFingerPrint.csproj --configuration Debug
dotnet build .\DevsFingerPrint.csproj --configuration Release
```

La suite automatizada está destinada a Debug. Las pruebas usan persistencia temporal y un servidor HTTP local falso; no requieren lector físico ni deberían dirigirse a la API productiva. Release debe compilarse con el SDK DigitalPersona disponible.

## Alcance de las pruebas automatizadas

La suite cubre, entre otros aspectos:

- Reglas de duplicidad y alternancia de fichadas.
- Cola offline, orden, límite de 500 y confirmación de lotes.
- Respuestas HTTP/JSON y operaciones del cliente API.
- Persistencia local y credenciales protegidas.
- Catálogo, recuperación del lector e instancia única.
- Asegurar que el servicio biométrico simulado no forme parte de Release.

Las pruebas no certifican el funcionamiento físico del lector, el runtime del proveedor, la aceptación de enrolamientos en el servidor real ni el despliegue. Esas validaciones requieren un entorno de aceptación autorizado.

## Flujo recomendado de cambio

1. Leer las guías de contexto del repositorio antes de modificar comportamiento.
2. Mantener sin cambios los contratos HTTP y la URL productiva salvo autorización explícita.
3. No agregar configuración local para cambiar intervalos de comunicación.
4. Añadir o ajustar pruebas del comportamiento afectado.
5. Ejecutar las pruebas Debug y las compilaciones Debug y Release.
6. Actualizar esta guía y la documentación de continuidad si el comportamiento cambió.

## Versionado del paquete

Antes de preparar una distribución, comparar `Properties/AssemblyInfo.cs` con `AppVersion` y `OutputBaseFilename` de `innoDevs.iss`. En el estado documentado aquí, el ensamblado es `1.0.6.0` y el instalador declara `1.0.7`; no publicar hasta resolver la discrepancia y validar el ejecutable Release.
