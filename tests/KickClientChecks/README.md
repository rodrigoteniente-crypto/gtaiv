Estas comprobaciones ejecutan los archivos reales de parseo de Kick, configuración,
disparadores y cola del mod bajo .NET 8. El único reemplazo es el enum de teclas de
Windows Forms, que no participa en las comprobaciones. Los eventos de Kick son
ejemplos sintéticos; la prueba de cancelación usa un puerto local cerrado.

```sh
dotnet run --project tests/KickClientChecks/KickClientChecks.csproj -c Release
```

No comprueban una suscripción real, la conexión de producción a Kick ni la IA
dentro de GTA IV. Esas pruebas se realizan con el juego en Windows.
