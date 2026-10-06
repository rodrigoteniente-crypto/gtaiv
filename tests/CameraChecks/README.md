# Comprobaciones de cámaras

Requieren .NET SDK 8 y no necesitan instalar GTA IV:

```sh
dotnet run --project tests/CameraChecks/CameraChecks.csproj
```

El proyecto compila directamente las clases de cámaras y sus lectores INI. Prueba el loop secuencial, las cámaras desactivadas, la selección aleatoria ponderada sin repetición inmediata, la validación de números y el guardado atómico con respaldo. También carga y vuelve a guardar las 33 cámaras originales desde una copia de la configuración incluida; las pruebas nunca sobrescriben esa configuración.

Los archivos de prueba se escriben en una carpeta temporal y se eliminan al terminar. La única sustitución es el método de normalización de `Config`, que permite compilar sin las dependencias de entrada del juego. Estas comprobaciones no simulan ni validan las cámaras nativas o la IA dentro de GTA IV.
