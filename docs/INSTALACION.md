# Instalar KickChaos IV corregido

Esta entrega se compiló usando el `ScriptHookDotNet.asi` que enviaste. Está
destinada a tu Complete Edition con aCompleteEditionHook. Cerrá el juego antes
de cambiar archivos. Windows necesita .NET Framework 4.8.

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
   En tu config original Patrulleros estaba en 0 y todos los niveles en Robar:
   Robar prioriza huir/arresto al principio y no produce un tiroteo inmediato.
5. Cargá una partida y abrí F8. En NPC de suscriptores usá **Probar: sub** para
   crear una banda; después creá otra. También se puede usar `kick sub` en la
   consola de ScriptHookDotNet. Las bandas deben buscar rivales y enfrentarse;
   las patrullas cercanas deben bajar, apuntar y disparar cuando corresponda.
6. Para probar Kick revisá el estado de conexión en el menú, enviá un comando
   configurado desde tu chat y comprobá su acción. Con una suscripción o regalo
   real, verificá el nombre del NPC y que un evento repetido no cree duplicados.

## Cámaras

- F7: editor. WASD para mover, Q/E para altura, mouse o IJKL para orientar,
  Z/X para FOV. ENTER guarda la cámara.
- SHIFT+ENTER: guarda el punto final de la última cámara para un plano móvil.
- F8: duración, modo al azar, pesos de cámara y transiciones.
- F6: siguiente cámara. F9: director encendido/apagado. F5: recargar archivos.
- La selección aleatoria evita repetir inmediatamente una cámara cuando hay
  otras activas. Cada guardado correcto deja `camaras.ini.bak` con la versión anterior.

## Si algo falla

`scripts\KickChaos\log.txt` indica versión, conexión, acciones y diagnósticos de
combate. Para un caso en el que no disparen, guardá el tramo desde que aparece
la banda hasta el fallo y anotá qué NPC está a pie/en auto. No hay todavía una
prueba de combate dentro del juego realizada desde esta nube.

Para volver al mod anterior, cerrá el juego y restaurá los archivos respaldados.
`Recuperacion-del-RAR` trae los INI originales recibidos, por si necesitás
recuperarlos; no los copies sobre cámaras nuevas que hayas guardado después.
