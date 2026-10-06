# Panel externo de KickChaos

Copiá `KickChaos.Panel.exe` junto a `GTAIV.exe` y abrilo en Windows con .NET Framework 4.8. También podés abrirlo desde otra carpeta y elegir la instalación de GTA IV en el panel. El mod y el panel pueden ejecutarse con una cuenta normal de Windows; no necesitan puertos ni permisos de administrador.

La lista muestra los personajes vivos, vida, kills, banda, vehículo, objetivo, build y mejoras. El buscador filtra por nombre, dueño, tipo y estado. Un click observa al personaje durante los segundos elegidos; desactivá «Click en NPC: observar» para seleccionar sin cambiar la cámara. «Fijar cámara» conserva al personaje hasta volver al loop. «Observar» permite una visita temporal y vuelve al plano anterior.

«Destrabar / reubicar» mueve al mismo personaje a una posición cercana segura y conserva su vida, kills y mejoras. Teletransportar permite elegir una zona, la cámara actual o coordenadas con punto decimal. La zona debe estar cargada: si GTA no devuelve una ubicación segura, el panel informa el problema y conserva la posición actual. Los cambios de comportamiento Pasear/Batalla afectan a su banda; la build modifica al personaje seleccionado. Matar/eliminar termina su vida actual.

El minimapa de posiciones se centra en la cámara. El cono indica la dirección y el FOV; verde es SUB, azul FOLLOW y violeta DUPLICADO. El aro amarillo marca personajes que se aproximan hacia el área visible. Los eventos de carrera se marcan en naranja. Es un mapa de coordenadas para preparar situaciones, sin cartografía de calles.

Los mensajes de respuesta aparecen al pie del panel. Debug escribe el diagnóstico en el log del juego y devuelve su tarea, objetivo y vehículo. Si GTA está pausado o deja de actualizar, el panel muestra ese estado y espera.

Configuración en `scripts/KickChaos/config.ini`:

```ini
[PanelAdmin]
Activado=true
IntervaloSegundos=0.75
```

El intercambio local vive en `scripts/KickChaos/panel`: `snapshot.json`, `commands/` y `acks/`. Los comandos llevan identificadores únicos, la sesión de juego, escritura atómica y vencen a los dos minutos. Un comando anterior a una recarga no puede afectar a un personaje nuevo. El mod procesa como máximo cuatro comandos por Tick y consulta la cola cada 0,15 segundos. Estos archivos son temporales de funcionamiento; no reemplazan cámaras, configuración ni estadísticas.
