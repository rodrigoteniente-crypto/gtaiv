# Instalar KickChaos IV 1.6.3

Esta entrega se compiló usando el `ScriptHookDotNet.asi` que enviaste. Está
destinada a tu Complete Edition con aCompleteEditionHook. Cerrá el juego antes
de cambiar archivos. Windows necesita .NET Framework 4.8.

Si ya instalaste 1.6.2 con su dependencia, basta reemplazar
`scripts\KickChaos.net.dll` y reiniciar GTA IV. F5 recarga los INI;
no carga una DLL nueva. El inicio del log debe decir `1.6.3-community`.

1. Guardá una copia de `scripts\KickChaos.net.dll` y de la carpeta
   `scripts\KickChaos` que ya tenés. Si existe `System.Numerics.Vectors.dll` en
   la carpeta del juego, guardá una copia también.
2. Del ZIP, copiá `Para-copiar-al-juego\scripts\KickChaos.net.dll` a la carpeta
   `scripts` del juego. Copiá `Para-copiar-al-juego\System.Numerics.Vectors.dll`
   junto a `GTAIV.exe`. El mod necesita esta dependencia para sus cálculos.
3. Conservá `scripts\KickChaos\camaras.ini`, `ranking.ini` y tus archivos de OBS.
   El ZIP no trae una carpeta de datos que los reemplace al instalar la DLL.
4. Para probar el combate con el perfil preparado, copiá
   `Perfil-recomendado\config.ini` a `scripts\KickChaos\config.ini`, después de
   respaldar el original. Mantiene tu canal y demás ajustes del RAR, activa dos
   patrullas y comportamientos de tiroteo. Si preferís conservar tu config actual,
   en F8 > NPC de suscriptores elegí **Tiroteo** y **Patrulleros: 2**.
   Elegí también **IA de los NPC: IA del juego** para probar las reacciones
   nativas. El perfil recomendado ya lo incluye. Si tu INI no tiene `IA`, la
   nueva DLL usa Juego; un `IA = Mod` guardado antes se conserva.
   En tu config original Patrulleros estaba en 0 y todos los niveles en Robar:
   Robar prioriza huir/arresto al principio y no produce un tiroteo inmediato.
5. Cargá una partida y abrí F8. En NPC de suscriptores usá **Probar: sub** para
   crear una banda; después creá otra. También se puede usar `kick sub` en la
   consola de ScriptHookDotNet. Las bandas deben buscar rivales y enfrentarse;
   las patrullas cercanas deben perseguir y combatir cuando corresponda. Observá
   una persecución en auto y comprobá que no se interrumpa al subir/bajar el blanco.
6. Para probar Kick revisá el estado de conexión en el menú, enviá un comando
   configurado desde tu chat y comprobá su acción. Con una suscripción o regalo
   real, verificá el nombre del NPC y que un evento repetido no cree duplicados.

## Cámaras

- F4 / **Cambiar a cámara de NPC**: pasa al próximo NPC vivo y lo sigue hasta
  que lo cambies o muera. Nuevas suscripciones no te quitan la selección.
- F6 / **Volver al loop de cámaras**: sale del seguimiento. En el loop, F6
  también permite avanzar al próximo plano.
- F7: editor. WASD para mover, Q/E para altura, mouse o IJKL para orientar,
  Z/X para FOV. ENTER guarda la cámara.
- SHIFT+ENTER: guarda el punto final de la última cámara para un plano móvil.
- F8: duración, modo al azar, pesos de cámara y transiciones.
- F9: director encendido/apagado. F5: recargar archivos.
- La selección aleatoria evita repetir inmediatamente una cámara cuando hay
  otras activas. Cada guardado correcto deja `camaras.ini.bak` con la versión anterior.

## Comparar las dos IA

F8 > NPC de suscriptores > **IA de los NPC** cambia entre IA del juego y IA del
mod sin reiniciar. Juego permite que el motor decida cobertura, reacciones y
combate en vehículos; Mod añade órdenes de recuperación más frecuentes. Ambos
siguen creando NPC, coordinando rivales y recuperando tareas que se traban.
Para una comparación clara usá las mismas categorías Tiroteo/Arrasar y dos
patrullas. Robar/Huir priorizan escapar y no siempre intentan disparar.
El combate desde autos depende del vehículo, asiento y arma; el motor puede
decidir bajar para pelear. Los pasajeros disponen de armas compatibles.

## Si algo falla

`scripts\KickChaos\log.txt` indica versión, conexión, acciones y diagnósticos de
combate. Si quedan quietos o los patrulleros siguen frenando a tirones, guardá
el tramo desde que aparece la banda hasta el fallo y anotá la IA seleccionada,
si ocurrió en patrulleros del mod o del tráfico y quién estaba a pie/en auto.
Esta entrega fue compilada y verificada con pruebas de lógica; falta comprobar
la conducción y el combate reales dentro de tu juego.

Para volver al mod anterior, cerrá el juego y restaurá los archivos respaldados.
`Recuperacion-del-RAR` trae los INI originales recibidos, por si necesitás
recuperarlos; no los copies sobre cámaras nuevas que hayas guardado después.
