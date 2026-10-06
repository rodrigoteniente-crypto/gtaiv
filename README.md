# KickChaos IV corregido

Mod para GTA IV Complete Edition con el **aCompleteEditionHook y ScriptHookDotNet
de la instalación del usuario**. El código fue recuperado del respaldo
`KickChaos.net.dll.v161` del RAR proporcionado. El RAR tenía una guía de la versión
1.7, pero no incluía su DLL principal ni fuentes: esta entrega parte de 1.6.1 y
se identifica como `1.6.3-community`.

Conserva el director de cámaras, editor con FOV, transiciones, loop, selección
aleatoria ponderada, suscriptores con nombre y vida, bandas, ranking y comandos
del chat. Las 33 cámaras del RAR están en `config/original/camaras.ini`.

## Correcciones

- F4 / **Cambiar a cámara de NPC** recorre los NPC vivos y mantiene la selección.
  F6 vuelve al loop; una nueva suscripción no cambia la cámara manual.
- Selector **IA de los NPC: Mod / Juego**, guardado desde F8 y aplicado a los
  personajes vivos. Juego es el valor predeterminado si falta la opción `IA`.
  Deja el combate, cobertura y decisiones de combate en vehículos al motor;
  Mod añade recuperación con ráfagas y movimiento dirigido. Ambos mantienen
  la creación de suscriptores, selección de rivales y recuperación de atascos.
- Persecuciones con rutas por calles, en lugar del estilo que ignora las calles.
  Una patrulla que avanza conserva su tarea; los cambios entre objetivos a pie
  y en auto tienen un margen para completar las animaciones. Se quitó la salida
  obligatoria a los 25 segundos aunque el sospechoso todavía estuviera lejos.
- Policía ambiental con un solo supervisor por patrullero, pasajeros que no
  solicitan detener el auto para bajar y validación del vehículo realmente usado.
  El director ya no desactiva globalmente la búsqueda policial de la ciudad.
- Arresto sólo cerca de un sospechoso detenido o rendido. Los corredores se
  persiguen; se respetan las animaciones de entrar/salir del auto y la cobertura.
- Combate dirigido al policía o rival elegido, con recuperación si el estado
  del juego dice combate pero el NPC no avanza ni dispara. Las ráfagas en curso se respetan.
- Policía ambiental supervisada sólo cerca de una banda; las patrullas del mod
  tienen un único administrador. Al liberar policías se retiran los cambios locales.
- Los blancos lejanos no cuentan como visibles sólo porque una native informa
  detección. La rendición ya no reinicia su propio temporizador cada tick.
- Guardado atómico de cámaras y respaldo `.bak`, selección de cámaras activas,
  validación de valores y FOV de 3 a 120 en el editor.
- Eventos duplicados de Pusher filtrados; los regalos con destinatarios crean
  NPC con sus nombres. Reconexión con cancelación, límites de cola y teclas con
  protección frente a repeticiones rápidas.
- La limpieza no borra patrullas en zonas con NPC activos ni autos protegidos.

`config/original` conserva la configuración recibida. `config/recommended`
activa IA Juego, dos patrullas, tiroteo para las primeras categorías y Arrasar para veteranos
y regalos. Desactiva NPC de follows para reservar los lugares a suscriptores.
No se aplica este perfil automáticamente sobre la instalación del usuario.

## Instalar y probar

Ver [las instrucciones de instalación](docs/INSTALACION.md). El paquete compilado
se genera en `dist/KickChaos-IV-1.6.3.zip` y contiene la DLL, su dependencia y
los ajustes opcionales. No incluye ni reemplaza el juego ni los cargadores.

La compilación y las regresiones se ejecutan en Linux. **El combate, los NPC,
la cámara dibujada y los eventos reales de Kick necesitan validación en el juego**.
La conexión conserva el mecanismo público de Kick/Pusher del mod original;
cambios futuros de Kick pueden requerir adaptar el cliente.
La IA Juego usa tareas nativas para los NPC; no convierte a un suscriptor en el
jugador Niko ni le transfiere automáticamente su sistema de estrellas.

## Desarrollar

Requiere SDK .NET 8 para herramientas/pruebas y referencias de .NET Framework
4.8 para compilar el mod. El juego necesita .NET Framework 4.8 en Windows.

```bash
bash tools/build.sh /ruta/ScriptHookDotNet.asi
bash tools/test.sh
python3 tools/package.py
```

En esta nube la referencia exacta está retenida en
`/workspace/.onboarding/ScriptHookDotNet.dll`; `bash tools/build.sh` la usa.
Los scripts de prueba enlazan el código de producción y usan dobles sólo para
teclado/natives que no se pueden ejecutar fuera del juego. No simulan la física
ni prueban la IA nativa de GTA IV. El proyecto C# está en `src/KickChaos`.
