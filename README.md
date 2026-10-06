# KickChaos IV 1.9.2 camera

Mod de GTA IV Complete Edition para el aCompleteEditionHook y ScriptHookDotNet
proporcionados por el usuario. Reutiliza el código completo del ZIP 1.9.0:
cámaras/editor/FOV, chat, NPCs, mejoras, ranking, radio y eventos. La versión
nueva está en `stream/`; `src/` conserva la entrega 1.6.3 para recuperación.

La 1.9.2 corrige la regresión del seguimiento de NPCs de la 1.9.1. El log
recibido confirma que el personaje aparece y conduce; la cámara había perdido
la comprobación del terreno. Ahora usa seguimiento manual cercano por defecto,
valida cada posición antes de aplicarla y vuelve a ciudad si no encuentra una
toma válida. Mantiene la carga de la ciudad alrededor del NPC y registra las
coordenadas del personaje y la cámara para diagnosticar la prueba en el juego.

## Cambios

- Suscriptores sin vencimiento: conservan vida, kills y mejoras hasta morir
  en la partida. El principal recibe mejores estadísticas que los duplicados.
  Viewers pueden reclamar un duplicado mediante `!unirme<ID>` y usar su nombre
  y burbujas de chat. Followers débiles, sin duplicación, pasean y pueden
  sumarse a una batalla cercana.
- Builds AGRESIVO, CAZADOR y SUPERVIVIENTE cambian enemigos preferidos, cadencia,
  puntería, cobertura y huida. Objetivos visibles en el panel. Conserva las
  elecciones de mejoras y órdenes del chat.
- Delay configurable de diez segundos para todos los eventos y mensajes de
  Kick. La prueba de suscripción usa la misma entrada. El NPC aparece antes
  de pedir cámara; la duración visible comienza después de cargar la toma.
- Loop de ciudad de 300 segundos. Evalúa NPCs cada 20 segundos, con un 30%
  de probabilidad, visita de diez segundos y cooldown de 120. Después retorna
  exactamente al plano anterior con su tiempo restante. F4 fija un NPC, F6
  vuelve a ciudad. Donaciones de 100 Kicks equivalen a diez segundos, con
  límites configurables. Seguimiento manual con FOV configurable, comprobación
  del terreno local y recuperación de tomas inválidas, incluso con un NPC fijado.
- HUD compacto con stats, builds, cooldown de `!npc`, nuevo suscriptor y feed.
  F1 oculta el HUD; F2 cambia el feed. Minimapa con cono del FOV, colores por
  tipo y marcadores de carreras/convoyes.
- Panel Windows separado: buscar, observar, fijar cámara, rescatar al mismo
  personaje conservando estadísticas, teletransportar, cambiar build o
  comportamiento, matar/eliminar y debug. Archivos locales con sesión,
  respuestas y vencimiento; las acciones se bloquean si el juego no actualiza.
- Menú F8 en ocho categorías con buscador. Perfil de 32 bandas y 48 personajes;
  siete bandas de suscriptores independientes y followers con facción común.
  Las apariciones esperan capacidad sin expulsar a los personajes vivos.
- Policía y NPCs conservan sus tareas cuando avanzan. Las animaciones de entrar
  al auto, cobertura y combate tienen margen para completar. Se corrigió la
  recuperación policial que reiniciaba su contador. La policía ambiental se
  incorpora a pie: los patrulleros de tránsito siguen con la IA del juego.
- Combate nativo en vehículos, armas compatibles para ventanillas y salida
  ante atasco o pistola agotada. Carreras y convoyes ocasionales con autos
  existentes, cerca de cámaras, sin teletransportar participantes. Conserva
  viajes compartidos y pasajeros con conductores de otra banda.

## Instalación

[Descargar ZIP compilado](https://github.com/rodrigoteniente-crypto/gtaiv/raw/refs/heads/kickchaos-camera-1.9.2/dist/KickChaos-IV-1.9.2-camera.zip).
Seguir [las instrucciones](docs/STREAM-INSTALACION.md). Incluye la DLL, Numerics,
el panel y un perfil tranquilo opcional. Conservar `camaras.ini`, `ranking.ini`
y una copia del config propio antes de aplicar el perfil. No entrega el juego
ni reemplaza cargadores.

Para actualizar desde la 1.9.1, cerrar GTA y reemplazar solamente
`scripts/KickChaos.net.dll`. Conservar la configuración y las cámaras propias;
las nuevas opciones usan valores seguros aunque no estén en el config existente.

Las posiciones y FOV guardados se conservan. `UsarDuracionGlobal` aplica los
300 segundos sin reescribir las duraciones individuales; desactivarlo las respeta.

## Validación y límites

Compilado x86 para .NET Framework 4.8 contra el ScriptHookDotNet recibido.
Las suites enlazan producción para políticas de combate/conducción, eventos,
persistencia de cámaras/ranking, HUD, ritmo ambiental e IPC del panel. Ver
[resultados y prueba en el juego](docs/STREAM-VALIDACION.md).

Windows/GTA IV no están disponibles en la nube: física, animaciones, combate
nativo, aspecto de cámaras/HUD y WinForms requieren prueba en el juego. No se
probó una sesión real de tres horas ni una suscripción pagada de Kick. Se
conserva Kick/Pusher del mod original; depende de los eventos que emita Kick.

La IA Juego usa tareas nativas, sin transferir automáticamente al NPC todo el
sistema de búsqueda de Niko. Vida/kills/mejoras del personaje duran durante
la partida; el ranking se guarda entre sesiones. La API de radio es global:
`!radio` guarda la elección por NPC y la aplica cuando se lo observa.

## Desarrollo

Requiere SDK .NET 8, Python 3 y la referencia local del hook. El juego y panel
necesitan .NET Framework 4.8 en Windows. Los cargadores no se distribuyen.

```bash
bash tools/build-stream.sh /ruta/ScriptHookDotNet.asi
bash tools/test-stream.sh
python3 tools/package-stream.py
```

En esta nube se puede omitir el argumento: usa
`/workspace/.onboarding/ScriptHookDotNet.dll`. Los binarios restauran dependencias
con lockfile. `stream/original/` conserva los textos y config recibidos.
`stream/tests/LegacyHarness` conserva el harness 1.9.0 como referencia y no se
contabiliza entre las pruebas nuevas. El ZIP incluye fuentes, guías, licencias
Numerics y checksums SHA256.
