# Validación realizada

Fecha: 6 de octubre de 2026.

- Compilación Release x86, .NET Framework 4.8, con la API exacta de
  `ScriptHookDotNet.asi` enviada por el usuario: 0 errores, 0 advertencias.
- CombatPolicyTests: 21 verificaciones sobre recuperación, rangos y visión.
- CameraChecks: 1.284 verificaciones; conserva las 33 cámaras originales,
  prueba loop, pesos, exclusión de desactivadas y fallos de guardado.
- KickClientChecks: 31 verificaciones de parsing, duplicados, destinatarios
  de regalos, cooldowns, cola y cancelación local de conexiones.
- CleanupChecks: 32 verificaciones de conservación de policías, autos
  protegidos y eliminación puntual de vehículos/cadáveres.

Los tests enlazan las fuentes usadas en la DLL. Sus dobles de teclado/natives
no ejecutan el juego: no demuestran que la IA nativa dispare, que un recorrido
sea navegable ni que Kick entregue eventos de producción. Se necesita la
prueba indicada en INSTALACION.md dentro de la instalación Windows del usuario.
No se verificó restauración en una nueva tarea de nube ni publicación.

## Procedencia

Código recuperado con ILSpy del respaldo incluido en el RAR, sin ejecutar
el binario recibido. Los componentes del cargador no se distribuyen.

SHA-256 del respaldo v161: `8b4c6d7f5c3c3b2bc9c4a334e8a1799121a4f36f99f2537e4575a633ef290d73`

SHA-256 del ScriptHookDotNet recibido: `ab8e7652a53a82c00018eb85d6fb5db7f47197513f16b5959771b4ca2d8d4d3b`
