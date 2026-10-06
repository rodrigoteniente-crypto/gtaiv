# Validación de KickChaos IV 1.9.2-camera

Validación realizada en Linux con SDK .NET 8 y la referencia ScriptHookDotNet
proporcionada. Los dos binarios son x86 / .NET Framework 4.8.

## Comprobaciones ejecutadas

`bash tools/build-stream.sh` compila el mod y el panel Windows.
`bash tools/test-stream.sh` ejecuta nueve suites que enlazan código de producción:

| Suite | Comprobaciones | Qué verifica |
| --- | ---: | --- |
| StreamStability | 2167 | Progreso, cobertura, visibilidad, transiciones, persecución y recuperación policial |
| StreamCamera | 3029 | Reloj, retorno exacto, prioridades, carga, cooldown y posiciones imposibles |
| StreamCameraPlacement | 161 | Altura local, terreno entre cámara y actor, cambios de cota, suelo no cargado y posiciones alternativas |
| StreamEvents | 69 | Delay, colas acotadas, deduplicación, regalos, donaciones y cierre de conexión |
| StreamHud | 472 | Layout, clipping, buscador, feed, destacado y proyección de minimapa |
| StreamAmbient | 62 | Ritmo de eventos, prioridades, participantes, alianzas y cierre |
| NpcPolicy | 2524 | Principal/duplicado/follower, capacidad, reclamación y decisiones de build |
| StreamAdmin | 39 | JSON, sesiones, vencimiento, límites, coordenadas y escritura atómica |
| StreamPersistence | 187 | Cámaras anteriores, FOV, fallo de guardado, ranking y perfil entregado |

Total: **8710 comprobaciones**. La simulación de tres horas evalúa probabilidades
y cooldowns de políticas; no ejecuta GTA ni mide su rendimiento. El harness
original 1.9.0 se conserva como referencia y no se contabiliza.

La compilación completa del mod puede emitir siete advertencias CS0649 por
campos heredados sin asignación; no son errores de enlace. El panel compila
sin advertencias. Una compilación incremental puede omitirlas.

El empaquetador verifica el ZIP y cada entrada con SHA256. La descarga pública
se compara contra el paquete local antes de entregar el enlace. La carpeta para
copiar al juego no contiene cámaras, configuración actual, logs ni cargadores.

## Diagnóstico del log recibido

El nuevo log de la 1.9.1 confirma que `prueba` existe, mantiene 100% de vida,
roba autos y conduce con velocidades distintas de cero. También registra la
solicitud manual de seguir al NPC 20484. Esas líneas prueban creación y actividad
del personaje; no prueban que la imagen de la cámara lo mostrara correctamente.

La revisión de la regresión encontró que el seguimiento había quedado sin la
validación local de terreno antes de aplicar la posición. La 1.9.2 instala una
posición de seguimiento estable cercana por defecto, con 6 m a pie y 10 m en
vehículo; conserva el FOV configurado. Usa `CamaraEstable`,
`DistanciaEstablePie` y `DistanciaEstableAuto` en `[Suscriptor]`.

`ValidateFollowPose` ejecuta la política de terreno antes de `SET_CAM_POS`.
Comprueba puntos cercanos y el terreno entre actor y cámara, prueba alternativas
cortas y recupera la ciudad si no obtiene una pose utilizable. No elimina al NPC.
Se retiraron `ENABLE_CAM_COLLISION` y `SET_CAM_TARGET_PED` del director para
mantener una sola autoridad sobre la posición y orientación de esta cámara manual.
`KeepFollowSceneLoaded` conserva la escena en ambos modos de seguimiento.
Cuando el jugador oculto se aleja más de 20 m, actualiza su ancla y solicita
colisión con una separación mínima de 1,5 s entre movimientos. Las distancias del
modo estable usan sus claves propias, sin quedar limitadas por el ajuste anterior
`CamaraDistancia`.

El nuevo diagnóstico `[camdiag]` registra XYZ del actor, posición solicitada
(`requested`) y posición leída de la cámara nativa (`native`), además de rotación,
FOV, diferencia de altura y estado. Permite distinguir una posición calculada
incorrecta de una cámara nativa que no coincida con el pedido. La consulta de
terreno no es un raycast de paredes ni certifica visibilidad real en interiores.
El adaptador del juego `G.GroundZ` trata una altura devuelta de cero como ausencia
de datos. Es una decisión conservadora: no distingue terreno a cota cero de una
consulta sin resultado. La política acepta cota cero solamente cuando su proveedor
devuelve éxito explícito; la prueba de ese caso no certifica que el adaptador nativo
pueda reconocerlo. Ante una consulta no utilizable, el seguimiento intenta otra
posición o vuelve a ciudad.

### Antecedentes de la 1.9.0

El log anterior contiene varias sesiones. En una de 1.9.0, repite un policía
a pocos metros con cero tiros y varias ráfagas ordenadas. Pedir una tarea no
prueba que el juego haya ejecutado un disparo. También repite mensajes de cámara
bajo tierra/tapada y eventos frecuentes.

La revisión corrigió reseteos de tareas/temporizadores, conducción que ignoraba
calles, entrada al auto interrumpida y arma incompatible en el vehículo. La
recuperación por inactividad acumula progreso real; no se reinicia al renovar
combate. El perfil usa policías reales. Se retiró la supervisión del patrullero
ambiental mientras sigue circulando.

## Prueba pendiente en Windows/GTA

Para actualizar desde la 1.9.1, cerrar GTA, reemplazar solamente
`scripts/KickChaos.net.dll` y reiniciar. Conservar configuración, cámaras y
ranking; el panel no cambió. El paquete de este hotfix está en
[`KickChaos-IV-1.9.2-camera.zip`](https://github.com/rodrigoteniente-crypto/gtaiv/raw/refs/heads/kickchaos-camera-1.9.2/dist/KickChaos-IV-1.9.2-camera.zip).

Prueba corta del arreglo: simular una suscripción desde F8, esperar el delay,
seguir con F4 a pie y en auto, y volver con F6. Comprobar que el encuadre está
por encima del terreno, que conserva el FOV y que retorna al plano de ciudad con
el tiempo pendiente. Repetir bajo puente/túnel y guardar las líneas `[camdiag]`
si aparece una vista incorrecta o vuelve a ciudad por falta de pose segura.

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
