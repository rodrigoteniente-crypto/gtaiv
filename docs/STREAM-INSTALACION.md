# KickChaos IV 1.9.2-camera

La 1.9.2 corrige una regresión del seguimiento de NPC: la cámara podía colocarse por debajo del terreno. El log nuevo confirma que el personaje de prueba se creó, robó autos y condujo; el problema reportado se concentra en la cámara. El seguimiento ahora usa una toma estable y cercana, valida el terreno antes de mover la cámara y recupera la vista de ciudad cuando no encuentra una posición utilizable.

[Descargar KickChaos IV 1.9.2-camera](https://github.com/rodrigoteniente-crypto/gtaiv/raw/refs/heads/kickchaos-camera-1.9.2/dist/KickChaos-IV-1.9.2-camera.zip)

## Actualizar desde 1.9.1

1. Cerrá GTA IV y guardá una copia de `scripts/KickChaos.net.dll` fuera de la carpeta `scripts`.
2. Del ZIP, copiá **solamente** `Para-copiar-al-juego/scripts/KickChaos.net.dll` sobre el DLL de tu instalación.
3. Conservá `scripts/KickChaos/config.ini`, `camaras.ini`, ranking, cargadores, dependencia y panel. Reiniciá GTA IV; F5 no carga un DLL nuevo.

El panel no cambió. No hace falta aplicar nuevamente el perfil ni editar la configuración para activar la corrección. Si todavía no instalaste la 1.9.1, seguí la instalación completa de abajo.

## Seguimiento estable

La toma de NPC es estable por defecto, a unos 6 metros del personaje a pie y 10 metros en auto. Conserva tu FOV existente de `[Suscriptor] CamaraFOV`. F8 → Cámaras → Cámara de NPC permite ajustar «Seguimiento estable», «Distancia estable a pie» y «Distancia estable en auto». También podés cambiar estos valores en la sección `[Suscriptor]` de tu configuración actual:

```ini
CamaraEstable = si
DistanciaEstablePie = 6
DistanciaEstableAuto = 10
```

Si las claves no están, el mod usa esos valores automáticamente. La cámara prueba posiciones cercanas según el terreno cargado y conserva al NPC cuando necesita regresar a ciudad. Esta comprobación de altura no es un raycast de paredes ni demuestra visibilidad real: hace falta probar puentes, túneles y obstáculos dentro del juego.

Para comprobar el hotfix, simulá una suscripción desde F8 → Debug → Probar acciones / eventos → Simular una suscripcion. Esperá el delay, pulsá F4 y mirá al personaje tanto a pie como en auto. Debe verse la toma cercana; F6 debe volver al plano de ciudad. Si vuelve a fallar, guardá `scripts/KickChaos/log.txt`: las líneas `[camdiag]` registran `actor`, `requested` y `native`, cada uno con XYZ, además de rotación, FOV y estado de seguimiento. El mod compiló fuera del juego; todavía falta validar esta versión en tu GTA IV.

La versión conserva el perfil de fondo tranquilo, eventos demorados, cámaras temporales, personajes sin vencimiento por tiempo, HUD y panel externo que partieron de tu mod 1.9.0. Está preparada para GTA IV Complete Edition con tu instalación de aCompleteEditionHook y ScriptHookDotNet. El ZIP completo incluye el mod y el panel; usá los cargadores que ya tenés instalados.

## Instalación completa

1. Cerrá GTA IV y el panel. Guardá una copia de `scripts/KickChaos.net.dll` y de la carpeta `scripts/KickChaos`, especialmente `config.ini`, `camaras.ini` y el archivo de ranking que ya usás.
2. Descomprimí el ZIP. Copiá **el contenido** de `Para-copiar-al-juego` a la carpeta donde está `GTAIV.exe`, aceptando reemplazar el DLL del mod. La carpeta `scripts` se combina con la existente.
3. Comprobá estas ubicaciones:

   ```text
   GTAIV.exe
   System.Numerics.Vectors.dll
   KickChaos.Panel.exe
   KickChaos.Panel.exe.config
   scripts/
       KickChaos.net.dll
       KickChaos/
           config.ini
           camaras.ini
           ...tu ranking y otros archivos existentes...
   ```

4. Conservá tu `config.ini`, cámaras y ranking. El contenido de `Para-copiar-al-juego` no los reemplaza. No dejes otra copia de `KickChaos.net.dll` con distinto nombre dentro de `scripts`, porque el cargador podría ejecutar ambos mods.
5. Volvé a iniciar GTA IV. **F5 recarga configuración; reemplazar el DLL requiere cerrar y reiniciar el juego.**

La dependencia `System.Numerics.Vectors.dll` va junto a `GTAIV.exe`. El panel necesita Windows con .NET Framework 4.8 y funciona con una cuenta normal, sin puertos ni permisos de administrador.

## Aplicar el perfil de stream

Para probar el ritmo recomendado, con el juego cerrado renombrá tu `scripts/KickChaos/config.ini` a `config-antes-stream.ini` y copiá `Perfil-stream/config.ini` como `scripts/KickChaos/config.ini`. Tu `camaras.ini` y ranking quedan donde estaban. El ZIP también contiene una copia de la configuración recibida de la 1.9.0 en `Respaldo-1.9.0`.

El perfil ya tiene el canal `rodsquare`, `ChatroomId=31759082` y `ChannelId=32047403`. Si usás otro canal, cambiá el nombre y sus IDs. F8 permite ajustar las opciones y guardarlas; si editás el INI desde afuera, volvé al juego y pulsá F5.

El perfil usa estos valores:

| Ajuste | Valor |
| --- | --- |
| Plano de ciudad | 300 segundos |
| Delay de eventos y comandos de Kick | 10 segundos |
| Evaluar una visita ocasional a NPC | Cada 20 segundos, 30% de probabilidad |
| Separación entre visitas automáticas | 120 segundos |
| Visita automática / nuevo sub | 10 / 15 segundos |
| `!npc` | 12 segundos de cámara, 120 segundos de espera por dueño |
| Donación de Kicks | 100 Kicks = 10 segundos; mínimo 5, máximo 45 segundos |
| Build inicial | SUPERVIVIENTE |

Una donación enfoca al NPC vivo de quien dona. La duración es proporcional: 50 Kicks dan 5 segundos, 200 dan 20 y 500 quedan en el máximo de 45. El cambio ocurre después del delay configurado. Una cámara fijada manualmente conserva tu selección frente a interrupciones automáticas.

Las visitas temporales vuelven al plano anterior y siguen su tiempo restante. Por ejemplo, un plano con 260 segundos pendientes conserva esos 260 durante una visita al NPC; el loop no vuelve a empezar desde 300. Las duraciones y FOV guardados de tus cámaras se conservan en `camaras.ini`; `CamaraStream.UsarDuracionGlobal` decide si el director usa los 300 segundos generales o las duraciones individuales.

Podés ajustar el delay global en `[DelayKick]` y agregar excepciones como `Chat=0` o `Suscripcion=15`. Las opciones de encuentros, persecuciones, carreras y convoyes tienen probabilidades y esperas propias. Revisá `[Comandos]` si querés reducir las acciones de caos que ya habías habilitado para el chat.

## Teclas

Estos son los valores del perfil; si conservás otra configuración, mirá su sección `[Teclas]`.

| Tecla | Acción |
| --- | --- |
| F1 | Mostrar/ocultar el HUD compacto |
| F2 | Mostrar/ocultar el feed de eventos |
| F3 | Mostrar/ocultar el ranking |
| F4 | Cambiar a cámara de NPC y fijarlo; otra pulsación pasa al siguiente |
| F5 | Recargar configuración y cámaras |
| F6 | Desde un NPC, volver al plano de ciudad con su tiempo restante; desde ciudad, pasar al siguiente |
| F7 | Abrir/cerrar el editor de cámaras |
| F8 | Menú por categorías, con búsqueda |
| F9 | Prender/apagar el director |

El editor permite mover la cámara y cambiar el FOV; ENTER guarda el plano, T fija su objetivo y BACKSPACE elimina el último plano manual. El menú muestra las instrucciones del editor. Las teclas de GTA se procesan cuando el juego tiene foco, para que escribir en el panel u OBS no dispare acciones del mod.

## Panel externo y OBS

Abrí `KickChaos.Panel.exe`. Si está junto a `GTAIV.exe`, detecta esa carpeta; si lo abriste desde otro lugar, elegí la instalación con «Elegir…». Esperá el estado «Conectado» y la lista de personajes. Podés usarlo en otro monitor para administrar el mod fuera de cámara.

En OBS usá una fuente **Captura de juego** que capture GTA IV. El panel es otra ventana: mantenelo fuera de esa fuente. Si usás Captura de pantalla, elegí el monitor donde está el juego para que el panel no aparezca en el stream. El HUD del mod se dibuja dentro de GTA y sí queda incluido en la captura.

La lista permite buscar, observar con un click, fijar cámara, aplicar builds, cambiar Pasear/Batalla, reubicar al personaje trabado y teletransportarlo. «Destrabar / reubicar» conserva el mismo personaje, su vida, kills y mejoras. Las zonas de destino deben estar cargadas: si GTA no encuentra una posición segura, informa el problema y no lo mueve. El cambio Pasear/Batalla afecta a su banda; la build se aplica al personaje elegido.

«Observar» dura los segundos indicados y regresa a la vista anterior; «Fijar cámara» mantiene la selección hasta volver al loop. «Volver al loop» recupera la ciudad con su tiempo restante, y «Próxima cámara» avanza deliberadamente al siguiente plano. Matar/eliminar termina la vida actual. Debug devuelve la tarea, objetivo y vehículo y escribe el diagnóstico en el log.

El minimapa del panel muestra posiciones, tipos, dirección/FOV de la cámara y personajes que se aproximan. Es un mapa de coordenadas para preparar situaciones, sin cartografía de calles. El minimapa dentro del juego se puede activar aparte desde F8 → HUD. El panel muestra a todos los personajes; el HUD limita filas para dejar visible la ciudad.

El intercambio local usa `scripts/KickChaos/panel`. Si GTA se pausa o deja de actualizar durante ocho segundos, el panel bloquea nuevos envíos; también los bloquea si no puede leer un snapshot válido. Cada comando pertenece a una sesión del mod: un pedido pendiente de una recarga anterior no puede afectar a un personaje nuevo.

## Comandos para viewers

| Comando | Efecto |
| --- | --- |
| `!unirme34` | Poseer el duplicado cuyo cartel ofrece ese número; reemplazá 34 por el ID real |
| `!agresivo` | Buscar encuentros con más presión y riesgo |
| `!cazador` | Priorizar enemigos debilitados o aislados |
| `!superviviente` | Viajar y buscar combates con ventaja |
| `!npc` | Visita temporal al personaje del sub o dueño de duplicado, con su espera visible |
| `!radio` | Pedir la siguiente estación para el auto del NPC propio |

Los comandos de build actúan sobre el personaje propio. El duplicado disponible muestra `Escribe !unirme<ID> para unirte`; al poseerlo recibe el nombre y los mensajes de su dueño. Cada viewer puede poseer un duplicado vivo. Los followers tienen una vida y no generan duplicados.

La radio se aplica al NPC cuando la cámara lo observa y está en un auto. `!radio` no cambia la radio global de todos los vehículos. Con el perfil, los mensajes y comandos también esperan diez segundos antes de procesarse.

## Prueba local antes del stream

Esta prueba simula eventos dentro del juego y no requiere comprar ni recibir una suscripción real.

1. Cargá la partida y esperá que el director muestre una cámara de ciudad. Si estabas siguiendo un NPC, pulsá F6 para volver al loop.
2. En F8, comprobá que esté habilitado el NPC para Suscripción y «Ir a su cámara al aparecer». Para probar una nueva suscripción usá **Debug → Probar acciones / eventos → Simular una suscripcion**. La opción «NPC de suscriptor (6 meses)» también usa el mismo circuito demorado de eventos.
3. Cerrá el menú. Esperá el delay configurado de diez segundos y el margen de carga/cola. Debe aparecer el personaje `prueba`, entrar al feed/HUD y recibir su toma temporal. Después debe volver al plano previo con el tiempo que faltaba. La prueba local no reproduce la alerta de tu servicio de stream: valida el orden dentro del mod.
4. Abrí el panel y buscá `prueba`. Observá durante 15 segundos y comprobá la vuelta al plano anterior. Fijá la cámara y volvé con «Volver al loop» o F6.
5. Aplicá otra build y probá «Destrabar / reubicar». Comprobá que el ID, vida, kills y mejoras se conserven. Probá F1/F2 y el minimapa según lo que quieras dejar en OBS.
6. Simulá un follow desde la misma página de pruebas: debe entrar como FOLLOW, normalmente paseando/conduciendo, sin obligar a enfocar cada aparición con el perfil tranquilo.

Si falla, guardá `scripts/KickChaos/log.txt` de esa sesión y anotá el botón usado y lo que se vio. La compilación y las pruebas automatizadas se hicieron fuera del juego; la conducción, cámara, panel en Windows y recepción real de Kick todavía requieren esta comprobación en tu instalación.

## Duración de personajes y capacidad

Los NPCs vivos conservan vida, kills, mejoras y estado durante la sesión, sin vencimiento por tiempo paseando. Morir termina esa vida. Esto no guarda la ciudad completa: cerrar el juego, recargar scripts, cargar otra partida o un reinicio del estado del jugador puede liberar los personajes; el ranking se conserva en su archivo.

El perfil permite 32 grupos lógicos y 48 personajes totales, incluidos duplicados. El límite configurable de personajes llega a 96. GTA IV ofrece ocho grupos de relación utilizados por este sistema: siete se reservan para bandas de subs y uno se comparte entre followers. Por eso aumentar el límite general no permite superar siete bandas independientes de subs simultáneas. Cuando no hay lugar, los eventos esperan capacidad y se mantienen los NPCs vivos.

La policía y los NPCs reutilizan tareas de combate/conducción del juego con supervisión del mod. Eso permite perseguir, correr, robar autos y disparar desde vehículos; no convierte a cada NPC en el jugador Niko ni reproduce exactamente su sistema de búsqueda policial. La fluidez real de patrulleros y disparos desde autos se debe verificar dentro de GTA IV.
