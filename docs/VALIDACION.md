# Validación realizada

Fecha: 6 de octubre de 2026.

- Compilación Release x86, .NET Framework 4.8, con la API exacta de
  `ScriptHookDotNet.asi` enviada por el usuario: 0 errores, 0 advertencias.
- CombatPolicyTests: 12 casos de combate, 9 de visión, 7 de movimiento/cobertura,
  14 de entrada a vehículos/arresto/navegación, 2.100 decisiones de IA Juego y
  17 casos de conducción/selección de objetivo. Incluye una secuencia de 600
  ticks en la que una persecución que avanza no reinicia su tarea.
- CameraChecks: 1.301 verificaciones; conserva las 33 cámaras originales,
  prueba loop, pesos, exclusión de desactivadas, fallos de guardado, selección
  manual y regreso al loop protegido durante el fundido.
- KickClientChecks: 36 verificaciones de parsing, duplicados, destinatarios
  de regalos, valores predeterminados y recarga de IA/teclas, cooldowns, cola
  y cancelación local de conexiones.
- CleanupChecks: 32 verificaciones de conservación de policías, autos
  protegidos y eliminación puntual de vehículos/cadáveres.

Los tests enlazan las fuentes usadas en la DLL. Sus dobles de teclado/natives
no ejecutan el juego: no demuestran que la IA nativa dispare, que un recorrido
sea navegable ni que Kick entregue eventos de producción. Se necesita la
prueba indicada en INSTALACION.md dentro de la instalación Windows del usuario.
El paquete publicado se verifica descargándolo y comparando su SHA-256 con
el ZIP local. No se verificó restauración en una nueva tarea de nube.

El log recibido contiene sesiones de varias versiones: se usó como evidencia
de estados detenidos, arrestos lejanos y transiciones de vehículos interrumpidas,
no como prueba de esta DLL nueva. El estilo de conducción se contrastó con
`DrivingStyle` de ScriptHookDotNet: 1 sigue calles y evita tráfico; 2 ignora calles.

## Procedencia

Código recuperado con ILSpy del respaldo incluido en el RAR, sin ejecutar
el binario recibido. Los componentes del cargador no se distribuyen.

SHA-256 del respaldo v161: `8b4c6d7f5c3c3b2bc9c4a334e8a1799121a4f36f99f2537e4575a633ef290d73`

SHA-256 del ScriptHookDotNet recibido: `ab8e7652a53a82c00018eb85d6fb5db7f47197513f16b5959771b4ca2d8d4d3b`
