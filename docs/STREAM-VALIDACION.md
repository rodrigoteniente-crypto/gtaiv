# Validación de KickChaos IV 1.9.1 stream

Validación realizada en Linux con SDK .NET 8 y la referencia ScriptHookDotNet
proporcionada. Los dos binarios son x86 / .NET Framework 4.8.

## Comprobaciones ejecutadas

`bash tools/build-stream.sh` compila el mod y el panel Windows.
`bash tools/test-stream.sh` ejecuta ocho suites que enlazan código de producción:

| Suite | Comprobaciones | Qué verifica |
| --- | ---: | --- |
| StreamStability | 2167 | Progreso, cobertura, visibilidad, transiciones, persecución y recuperación policial |
| StreamCamera | 3029 | Reloj, retorno exacto, prioridades, carga, cooldown y posiciones imposibles |
| StreamEvents | 69 | Delay, colas acotadas, deduplicación, regalos, donaciones y cierre de conexión |
| StreamHud | 472 | Layout, clipping, buscador, feed, destacado y proyección de minimapa |
| StreamAmbient | 62 | Ritmo de eventos, prioridades, participantes, alianzas y cierre |
| NpcPolicy | 2524 | Principal/duplicado/follower, capacidad, reclamación y decisiones de build |
| StreamAdmin | 39 | JSON, sesiones, vencimiento, límites, coordenadas y escritura atómica |
| StreamPersistence | 187 | Cámaras anteriores, FOV, fallo de guardado, ranking y perfil entregado |

Total: **8549 comprobaciones**. La simulación de tres horas evalúa probabilidades
y cooldowns de políticas; no ejecuta GTA ni mide su rendimiento. El harness
original 1.9.0 se conserva como referencia y no se contabiliza.

La compilación completa del mod puede emitir siete advertencias CS0649 por
campos heredados sin asignación; no son errores de enlace. El panel compila
sin advertencias. Una compilación incremental puede omitirlas.

El empaquetador verifica el ZIP y cada entrada con SHA256. La descarga pública
se compara contra el paquete local antes de entregar el enlace. La carpeta para
copiar al juego no contiene cámaras, configuración actual, logs ni cargadores.

## Diagnóstico del log recibido

El log contiene varias sesiones anteriores. En una de 1.9.0, repite un policía
a pocos metros con cero tiros y varias ráfagas ordenadas. Pedir una tarea no
prueba que el juego haya ejecutado un disparo. También repite mensajes de cámara
bajo tierra/tapada y eventos frecuentes.

La revisión corrigió reseteos de tareas/temporizadores, conducción que ignoraba
calles, entrada al auto interrumpida y arma incompatible en el vehículo. La
recuperación por inactividad acumula progreso real; no se reinicia al renovar
combate. El perfil usa policías reales. Se retiró la supervisión del patrullero
ambiental mientras sigue circulando.

## Prueba pendiente en Windows/GTA

1. Cargar partida con el perfil stream y simular suscripción desde F8. Comprobar
   diez segundos de delay, NPC listo antes de cámara y destacado del HUD.
2. Anotar plano de ciudad y tiempo restante. Pedir `!npc` desde la prueba de
   chat del dueño; comprobar retorno al mismo plano y continuación del tiempo.
   Probar F4/F6 y un recorrido bajo puente o túnel.
3. Abrir panel fuera de captura, observar una fila y rescatar un personaje.
   Comprobar HP, kills y mejoras. Pausar GTA: las acciones del panel se bloquean
   hasta recibir snapshots recientes.
4. Comparar patrullas ambientales con director prendido/apagado. Provocar una
   persecución controlada y observar entradas/salidas, disparos, cobertura y
   atascos. Distinguir ráfagas ordenadas de disparos efectivos en el diagnóstico.
5. Probar batalla cerca de un follower en auto, reclamación de duplicado,
   builds, `!radio` y carreras. Una carrera terminada debe conservar participantes
   y vehículos.
6. Después probar un evento real del canal. Revisar `scripts/KickChaos/log.txt`
   si la conexión o el evento no se reconoce.

No se puede certificar desde Linux que el motor cumpla las tareas nativas, que
Kick entregue los eventos reales o que WinForms se dibuje correctamente. No se
guardan personajes completos para restaurarlos después de cerrar GTA: el ranking
persiste y el personaje vive hasta morir o terminar la partida. La reubicación
requiere una ubicación segura cargada; el panel informa cuando no está disponible.
