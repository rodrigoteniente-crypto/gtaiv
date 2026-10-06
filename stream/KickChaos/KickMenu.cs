using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace KickChaos
{
    /// <summary>Todas las paginas del menu de KickChaos. Cada cambio se guarda en config.ini / camaras.ini.</summary>
    public class KickMenu
    {
        readonly KickChaosScript host;
        public readonly Menu Menu;
        double confirmUntil;
        object confirmTarget;

        public KickMenu(KickChaosScript host)
        {
            this.host = host;
            Menu = new Menu(Root);
        }

        Config Cfg { get { return host.Cfg; } }
        IniFile Ini { get { return host.Cfg.Ini; } }
        Director Dir { get { return host.Dir; } }

        // ------------------------------------------------------------------
        // Guardar en config.ini
        // ------------------------------------------------------------------
        void Set(string section, string key, string value)
        {
            try
            {
                var doc = IniDoc.Load(Path.Combine(host.Folder, "config.ini"));
                doc.Set(section, key, value);
                doc.Save();
                host.SoftReload();
            }
            catch (Exception ex) { Menu.Toast("No se pudo guardar: " + ex.Message); }
        }

        static string Sec(float v) { return v.ToString(v < 10 && Math.Abs(v - Math.Round(v)) > 0.01 ? "0.0#" : "0", CultureInfo.InvariantCulture) + " s"; }
        static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        static float Snap(float v, float step) { return (float)Math.Round(Math.Round(v / step) * step, 3); }

        MenuItem Num(string label, string section, string key, float def, float min, float max, float step, Func<float, string> fmt, string help)
        {
            return new MenuItem
            {
                Label = label,
                Value = () => fmt(Ini.GetFloat(section, key, def)),
                OnChange = d => Set(section, key, IniFile.F(Clamp(Snap(Ini.GetFloat(section, key, def) + d * step, step), min, max))),
                Help = () => help
            };
        }

        MenuItem Bool(string label, string section, string key, bool def, string help)
        {
            return new MenuItem
            {
                Label = label,
                Value = () => Ini.GetBool(section, key, def) ? "SI" : "NO",
                OnChange = d => Set(section, key, Ini.GetBool(section, key, def) ? "no" : "si"),
                Help = () => help
            };
        }

        MenuItem Choice(string label, string section, string key, string[] values, string[] labels, string help)
        {
            return Choice(label, section, key, values, labels, values[0], help);
        }

        MenuItem Choice(string label, string section, string key, string[] values, string[] labels, string def, string help)
        {
            Func<int> cur = () =>
            {
                string v = Config.Normalize(Ini.Get(section, key, def));
                for (int i = 0; i < values.Length; i++) if (v == Config.Normalize(values[i])) return i;
                for (int i = 0; i < values.Length; i++) if (v.StartsWith(Config.Normalize(values[i]))) return i;
                return 0;
            };
            return new MenuItem
            {
                Label = label,
                Value = () => labels[cur()],
                OnChange = d => Set(section, key, values[((cur() + Math.Sign(d)) % values.Length + values.Length) % values.Length]),
                Help = () => help
            };
        }

        /// <summary>Hora: -1 = normal, 0..47 = cada media hora.</summary>
        static string TimeLabel(int slot) { return slot < 0 ? "Normal" : (slot / 2).ToString("00") + ":" + (slot % 2 == 0 ? "00" : "30"); }
        static int TimeSlot(int h, int m) { return h < 0 ? -1 : h * 2 + (m >= 30 ? 1 : 0); }
        static int NextSlot(int slot, int d) { int s = slot + Math.Sign(d); if (s < -1) s = 47; if (s > 47) s = -1; return s; }

        static readonly string[] WeatherLabels = { "Normal", "Extra soleado", "Soleado", "Ventoso", "Nublado", "Lluvia", "Llovizna", "Niebla", "Tormenta" };
        static int NextWeather(int w, int d) { int x = w + Math.Sign(d); if (x < -1) x = 7; if (x > 7) x = -1; return x; }

        bool Confirm(object target, string what)
        {
            if (confirmTarget == target && G.Now < confirmUntil) { confirmTarget = null; return true; }
            confirmTarget = target;
            confirmUntil = G.Now + 3;
            Menu.Toast("Apreta ENTER otra vez para " + what);
            return false;
        }

        static string Steps(List<ActionStep> steps)
        {
            if (steps == null || Config.IsNothing(steps)) return "Nada";
            var parts = new List<string>();
            foreach (var s in steps)
            {
                string c = ActionNames.Canonical(s.Name) ?? s.Name;
                if (c == "Nada") continue;
                parts.Add(ActionNames.Pretty(c) + (s.Repeat > 1 ? " x" + s.Repeat : ""));
            }
            return string.Join(" + ", parts.ToArray());
        }

        /// <summary>
        /// Las acciones de un evento/comando como lista editable ("Explosion*3", "Policia"...).
        /// Siempre tiene al menos 3 lugares; si en config.ini hay mas, se conservan.
        /// </summary>
        static string[] Slots(List<ActionStep> steps)
        {
            var r = new List<string>();
            if (steps != null)
                foreach (var s in steps)
                {
                    string c = ActionNames.Canonical(s.Name) ?? s.Name;
                    if (c == "Nada") continue;
                    r.Add(s.Repeat > 1 ? c + "*" + s.Repeat : c);
                }
            while (r.Count < 3) r.Add("Nada");
            return r.ToArray();
        }

        static string SlotName(string slot)
        {
            int star = slot.IndexOf('*');
            string c = star > 0 ? slot.Substring(0, star) : slot;
            return ActionNames.Pretty(c) + (star > 0 ? " x" + slot.Substring(star + 1) : "");
        }

        static string Join(string[] slots)
        {
            var parts = new List<string>();
            foreach (string s in slots) if (s != "Nada") parts.Add(s);
            return parts.Count == 0 ? "Nada" : string.Join("+", parts.ToArray());
        }

        static string CycleAction(string current, int d)
        {
            int star = current.IndexOf('*');
            if (star > 0) current = current.Substring(0, star); // al cambiar de accion se pierde la repeticion
            int i = Array.IndexOf(ActionNames.Choices, current);
            if (i < 0) i = 0;
            int n = ActionNames.Choices.Length;
            return ActionNames.Choices[((i + Math.Sign(d)) % n + n) % n];
        }

        // ------------------------------------------------------------------
        // Pagina principal
        // ------------------------------------------------------------------
        MenuPage Root()
        {
            return new MenuPage("Menu principal", () => new List<MenuItem>
            {
                new MenuItem { Info = true, DynamicLabel = () => "Kick: " + host.KickStatus() },
                new MenuItem { Label = "Buscar opciones", OnEnter = () => Menu.StartTyping("Buscar en todas las categorias", "", q => Menu.Push(SearchPage(q))), Help = () => "Nombre de opcion o NPC. CTRL+F filtra cualquier pagina" },
                new MenuItem { Label = "Kick / Eventos", Submenu = KickCategory },
                new MenuItem { Label = "NPCs", Value = NpcSummary, Submenu = NpcPage },
                new MenuItem { Label = "Camaras", Value = () => CountActive() + " activas", Submenu = CameraCategory },
                new MenuItem { Label = "Combate", Submenu = CombatCategory },
                new MenuItem { Label = "Vehiculos", Submenu = VehicleCategory },
                new MenuItem { Label = "HUD", Submenu = HudCategory },
                new MenuItem { Label = "Debug", Submenu = DebugCategory },
                new MenuItem { Label = "Configuracion", Submenu = SettingsCategory },
                new MenuItem { Label = "Cerrar menu", OnEnter = Menu.Close }
            });
        }

        MenuPage KickCategory()
        {
            return new MenuPage("Kick / Eventos", () => new List<MenuItem>
            {
                new MenuItem { Label = "Conexion con Kick", Submenu = Connection },
                new MenuItem { Label = "Efectos por evento", Submenu = Events },
                Num("Delay de todos los eventos", "DelayKick", "Segundos", 10, 0, 3600, 1, Sec, "Primero sale la alerta de tu stream; despues ocurre el efecto y la camara. Los comandos tambien esperan"),
                new MenuItem { Label = "Delay por tipo de evento", Submenu = EventDelayPage },
                new MenuItem { Label = "Comandos y palabras del chat", Value = () => Cfg.Triggers.Count.ToString(), Submenu = Commands },
                new MenuItem { Label = "Reglas y cooldowns", Submenu = Rules },
                new MenuItem { Label = "Eventos ambientales", Submenu = EventsPage }
            });
        }

        MenuPage EventDelayPage()
        {
            return new MenuPage("Delay por evento", () =>
            {
                var list = new List<MenuItem>();
                foreach (string key in new[] { "Chat", "Suscripcion", "RegaloSubs", "Follow", "Kicks", "Host", "Ban", "Otros" })
                    list.Add(Num(key, "DelayKick", key, Ini.GetFloat("DelayKick", "Segundos", 10), 0, 3600, 1, Sec, "Hereda delay global hasta que se guarda un valor propio"));
                return list;
            });
        }

        MenuPage CameraCategory()
        {
            return new MenuPage("Camaras", () => new List<MenuItem>
            {
                new MenuItem { Label = "Director de ciudad", Value = () => Dir.Active ? "PRENDIDO" : "APAGADO", OnChange = d => host.ToggleDirector(), Help = () => "Loop de camaras. Tecla " + Cfg.KeyDirector },
                new MenuItem { Label = "Volver al loop de ciudad", OnEnter = () => { if (Dir.Active) Dir.ReturnToCity(); else host.ToggleDirector(); }, Help = () => "Devuelve la camara anterior y su tiempo restante" },
                new MenuItem { Label = "Siguiente camara de ciudad", OnEnter = () => { if (Dir.Active) Dir.Next(); else host.ToggleDirector(); }, Help = () => "Avanza el loop intencionalmente" },
                new MenuItem { Label = "Cambiar a camara de NPC", OnEnter = () => host.FollowNextNpc(), Help = () => "Tecla " + Cfg.KeyFollow },
                new MenuItem { Label = "Mis camaras", Submenu = Cameras },
                new MenuItem { Label = "Escenas y transiciones", Submenu = Scenes },
                new MenuItem { Label = "Camara de NPC", Submenu = NpcCameraPage },
                new MenuItem { Label = "Interrupciones ocasionales", Submenu = InterruptionsPage }
            });
        }

        MenuPage CombatCategory()
        {
            return new MenuPage("Combate", () => new List<MenuItem>
            {
                new MenuItem { Label = "IA, encuentros y bandas", Submenu = NpcGangPage },
                new MenuItem { Label = "Policia", Submenu = PolicePage },
                new MenuItem { Label = "Armas por meses", Submenu = WeaponsMonthsPage },
                new MenuItem { Label = "Armas por nivel", Submenu = WeaponsLevelPage },
                Bool("Armas por nivel activas", SU, "ArmasPorNivel", true, "El principal recibe las mejores mejoras"),
                new MenuItem { Label = "Mejoras roguelike", Submenu = PerksPage },
                new MenuItem { Label = "Explosivos y municion", Submenu = AmmoPage }
            });
        }

        MenuPage VehicleCategory()
        {
            return new MenuPage("Vehiculos", () => new List<MenuItem>
            {
                Bool("Paseadores en auto", SU, "PaseoEnAuto", true, "Si no, pasean a pie"),
                Bool("Cambian de auto", SU, "CambianDeAuto", true, "Se respeta la animacion de entrada/salida"),
                Bool("Bandas recorren la ciudad", SU, "BandasRecorren", true, "Mantiene viajes compartidos sin ordenar tareas en cada frame"),
                Num("Velocidad de viaje", SU, "VelocidadAuto", 25, 10, 60, 5, v => v.ToString("0"), "Las persecuciones pueden usar otra velocidad"),
                Bool("Cura al conducir", SU, "CuraEnAuto", true, "Sin recibir dano"),
                Num("Cura por segundo", SU, "CuraPorSegundo", 2, 0, 20, 1, v => v.ToString("0") + "%", ""),
                Bool("Radio de NPC", SU, "RadioSiempre", true, "Tambien acepta !radio del propietario"),
                Choice("Como suena la radio", SU, "RadioModo", new[] { "Auto", "Celular" }, new[] { "Radio del auto", "Radio del celular" }, "Auto", "Radio del auto usa sonido espacial"),
                Bool("Puertas abiertas al bajar", SU, "PuertaAbierta", true, ""),
                Bool("Motor encendido al bajar", SU, "MotorPrendido", true, ""),
                new MenuItem { Label = "Trafico y limpieza", Submenu = World }
            });
        }

        MenuPage HudCategory()
        {
            return new MenuPage("HUD", () => new List<MenuItem>
            {
                Bool("HUD compacto para OBS", "HUDStream", "Mostrar", true, "Se ve en la captura del juego; muestra vivos y cooldown de !npc"),
                Num("Maximo de filas", "HUDStream", "MaxFilas", 6, 1, 12, 1, v => v.ToString("0"), "El panel externo muestra todos; el HUD limita filas para dejar la ciudad visible"),
                new MenuItem { Label = "Feed de eventos", Value = () => StreamHud.FeedVisible ? "SI" : "NO", OnChange = d => Set("HUDStream", "Feed", StreamHud.FeedVisible ? "no" : "si"), Help = () => "Subs, Kicks, follows y kills. Tecla " + Cfg.KeyFeed },
                Num("Filas del feed", "HUDStream", "FilasFeed", 3, 1, 5, 1, v => v.ToString("0"), ""),
                Num("Destacar nuevo sub", "HUDStream", "DestacadoSegundos", 8, 0, 30, 1, Sec, "Stats del nuevo NPC durante unos segundos"),
                Bool("Minimapa de camara / NPCs", "HUDStream", "Minimapa", false, "Verde SUB, azul FOLLOW, violeta DUP. Borde blanco: dentro del cono; cian: rumbo de auto proyecta entrada"),
                Num("Radio de minimapa", "HUDStream", "RadioMinimapa", 600, 50, 5000, 50, v => v.ToString("0") + " m", "Eventos y carreras cercanos se ven con E / R"),
                Num("Tamano minimapa", "HUDStream", "TamanoMinimapa", 0.21f, 0.12f, 0.4f, 0.01f, v => v.ToString("0.00", CultureInfo.InvariantCulture), "NPCs lejanos aparecen en el borde"),
                Num("Minimapa posicion X", "HUDStream", "MinimapaX", 0.025f, 0.01f, 0.75f, 0.01f, v => v.ToString("0.00", CultureInfo.InvariantCulture), ""),
                Num("Minimapa posicion Y", "HUDStream", "MinimapaY", 0.72f, 0.035f, 0.84f, 0.01f, v => v.ToString("0.00", CultureInfo.InvariantCulture), ""),
                Num("HUD posicion X", "HUDStream", "X", 0.72f, 0.01f, 0.79f, 0.01f, v => v.ToString("0.00", CultureInfo.InvariantCulture), "Se ajusta al ancho de pantalla"),
                Num("HUD posicion Y", "HUDStream", "Y", 0.055f, 0.01f, 0.30f, 0.01f, v => v.ToString("0.00", CultureInfo.InvariantCulture), ""),
                Num("HUD ancho", "HUDStream", "Ancho", 0.26f, 0.20f, 0.45f, 0.01f, v => v.ToString("0.00", CultureInfo.InvariantCulture), ""),
                new MenuItem { Label = "Carteles sobre NPCs", Submenu = NpcTagPage },
                new MenuItem { DynamicLabel = () => "Ranking de kills (" + Cfg.KeyRanking + ")", Value = () => host.Npcs.ShowRanking ? "SI" : "NO", OnChange = d => { bool on = host.Npcs.ToggleRanking(); Set(SU, "Ranking", on ? "si" : "no"); } },
                new MenuItem { Label = "Borrar ranking", OnEnter = () => { if (Confirm("ranking", "borrar ranking")) { host.Npcs.ResetRanking(); Menu.Toast("Ranking borrado"); } } },
                Bool("Feed anterior (opcional)", SU, "KillFeed", false, "Desactivarlo evita duplicar el nuevo feed")
            });
        }

        MenuPage DebugCategory()
        {
            return new MenuPage("Debug", () => new List<MenuItem>
            {
                new MenuItem { Info = true, DynamicLabel = () => StreamHudPolicy.Clean(StreamRuntime.Snapshot.Debug, 100) },
                Bool("Detalle en log.txt", SU, "Diagnostico", true, "Diagnostico de estado; sin forzar tareas por cada muestra"),
                new MenuItem { Label = "Probar acciones / eventos", Submenu = Tests },
                new MenuItem { Label = "Arreglar calle", Submenu = RepairPage },
                new MenuItem { Label = "Sacar todos los NPCs", OnEnter = () => { if (Confirm("allnpcs", "sacar todos los NPCs")) { host.Npcs.ReleaseAll(); Menu.Toast("NPCs liberados"); } } }
            });
        }

        MenuPage SettingsCategory()
        {
            return new MenuPage("Configuracion", () => new List<MenuItem>
            {
                new MenuItem { Label = "Teclas", Submenu = KeysPage },
                new MenuItem { Label = "Recargar archivos", OnEnter = () => { host.FullReload(); Menu.Toast("Configuracion y camaras recargadas"); } },
                new MenuItem { Info = true, Label = "Panel externo: KickChaos.Panel.exe, fuera de la captura" },
                Bool("Panel externo habilitado", "PanelAdmin", "Activado", true, "Lee el estado y procesa comandos locales, sin servidor de red")
            });
        }

        MenuPage InterruptionsPage()
        {
            return new MenuPage("Interrupciones ocasionales", () => new List<MenuItem>
            {
                Bool("Tomas ocasionales de NPC", "CamaraStream", "Activa", true, "Despues retorna a la misma camara con su tiempo restante"),
                Num("Evaluar cada", "CamaraStream", "EvaluarCadaSegundos", 20, 5, 300, 5, Sec, ""),
                Num("Probabilidad", "CamaraStream", "Probabilidad", 30, 0, 100, 5, v => v.ToString("0") + "%", ""),
                Num("Cooldown entre tomas", "CamaraStream", "CooldownSegundos", 120, 10, 1800, 10, Sec, ""),
                Num("Duracion de toma", "CamaraStream", "DuracionSegundos", 10, 3, 120, 1, Sec, ""),
                Num("Cooldown de !npc", "StreamingNPC", "EsperaNpc", 120, 0, 3600, 10, Sec, "Cada propietario ve el tiempo restante en el HUD"),
                Num("Duracion de !npc", "StreamingNPC", "DuracionNpc", 12, 3, 120, 1, Sec, ""),
                Num("Camara al aparecer", "CamaraStream", "DuracionSpawnSegundos", 15, 3, 120, 1, Sec, "Solo si Ir a su camara al aparecer esta habilitado"),
                Bool("Camara por donaciones", "CamaraKicks", "Activa", true, "Solo mientras su NPC sigue vivo"),
                Num("Kicks por tramo", "CamaraKicks", "Cantidad", 100, 1, 100000, 10, v => v.ToString("0"), ""),
                Num("Segundos por tramo", "CamaraKicks", "Segundos", 10, 1, 120, 1, Sec, ""),
                Num("Minimo por donacion", "CamaraKicks", "MinimoSegundos", 5, 1, 120, 1, Sec, ""),
                Num("Maximo por donacion", "CamaraKicks", "MaximoSegundos", 45, 5, 600, 5, Sec, ""),
            });
        }

        MenuPage SearchPage(string query)
        {
            return new MenuPage("Buscar: " + query, () =>
            {
                var results = new List<MenuItem>();
                var queue = new Queue<Tuple<string, MenuPage>>();
                foreach (Func<MenuPage> factory in new Func<MenuPage>[] { KickCategory, NpcPage, CameraCategory, CombatCategory, VehicleCategory, HudCategory, DebugCategory, SettingsCategory })
                {
                    MenuPage page = factory(); queue.Enqueue(Tuple.Create(page.Title, page));
                }
                int pages = 0;
                while (queue.Count > 0 && ++pages <= 250 && results.Count < 120)
                {
                    var entry = queue.Dequeue();
                    foreach (MenuItem item in entry.Item2.Build())
                    {
                        if (item.Info) continue;
                        string label = item.GetLabel();
                        if (StreamHudPolicy.Matches(label, query))
                            results.Add(new MenuItem { Label = entry.Item1 + " > " + label, Value = item.Value, OnEnter = item.OnEnter, OnChange = item.OnChange, Submenu = item.Submenu, Help = item.Help });
                        if (item.Submenu != null)
                        {
                            try { MenuPage sub = item.Submenu(); queue.Enqueue(Tuple.Create(sub.Title, sub)); } catch { }
                        }
                    }
                }
                if (results.Count == 0) results.Add(new MenuItem { Info = true, Label = "No hay coincidencias" });
                return results;
            });
        }

        void OpenEditor()
        {
            Menu.Close();
            if (!Dir.EditorActive) Dir.ToggleEditor();
        }

        int CountActive()
        {
            int n = 0;
            foreach (var s in Dir.Shots) if (s.Active && !s.IsAuto) n++;
            return n;
        }

        // ------------------------------------------------------------------
        MenuPage Scenes()
        {
            return new MenuPage("Escenas y transiciones", () => new List<MenuItem>
            {
                Num("Duracion de cada escena", "CamaraStream", "DuracionCiudadSegundos", 300, 3, 3600, 5, Sec,
                    "Duracion general de camaras de ciudad. SHIFT = de a 10"),
                Bool("Usar duracion general", "CamaraStream", "UsarDuracionGlobal", true,
                    "Desactivar para respetar la duracion guardada de cada camara"),
                Choice("Transicion entre camaras", "Director", "Transicion", new[] { "fundido", "corte" }, new[] { "Fundido a negro", "Corte directo" },
                    "Fundido: pasa por negro y carga la zona. Corte: cambia al instante (puede verse algo de carga)"),
                Num("Duracion del fundido", "Director", "FundidoMs", 500, 0, 3000, 100, v => Sec(v / 1000f), "Cuanto tarda en oscurecer y aclarar"),
                Num("Tiempo en negro para cargar", "Director", "EsperaCargaMs", 1500, 0, 6000, 250, v => Sec(v / 1000f),
                    "Mientras la pantalla esta en negro aparecen autos y gente en el plano nuevo"),
                Num("Zoom de camaras nuevas (FOV)", "Director", "FOVPorDefecto", 25, 5, 90, 1, v => v.ToString("0"), "FOV bajo = teleobjetivo. El juego normal usa ~50"),
                Num("Movimiento de camara en mano", "Director", "Balanceo", 0.25f, 0, 2, 0.05f, v => v.ToString("0.00", CultureInfo.InvariantCulture), "0 = camara fija"),
                Choice("Orden de las camaras", "Director", "Orden", new[] { "secuencial", "aleatorio" }, new[] { "En orden", "Al azar" }, ""),
                Choice("Que camaras usar", "Director", "Modo", new[] { "lista", "auto", "mixto" }, new[] { "Mis camaras", "Automaticas", "Mezcladas" },
                    "Automaticas: planos que arma solo en calles cercanas. Si no tenes camaras activas usa automaticas"),
                Num("No cortar despues de una accion", "Director", "NoCortarTrasAccion", 8, 0, 60, 1, Sec, "Espera a que se vea la explosion/policia antes de cambiar de camara"),
                Bool("Arrancar solo al cargar la partida", "Director", "AutoIniciar", true, ""),
                Num("Espera al cargar la partida", "Director", "EsperaAlCargar", 8, 0, 120, 1, Sec, "")
            });
        }

        // ------------------------------------------------------------------
        MenuPage Cameras()
        {
            return new MenuPage("Mis camaras", () =>
            {
                var items = new List<MenuItem>();
                items.Add(new MenuItem { Info = true, DynamicLabel = () => CountActive() + " de " + Dir.Shots.Count + " camaras en el loop" });
                foreach (CameraShot shot in Dir.Shots)
                {
                    if (shot.IsAuto) continue;
                    CameraShot s = shot;
                    items.Add(new MenuItem
                    {
                        DynamicLabel = () => (Dir.Active && Dir.Current == s ? "> " : "") + s.Name,
                        Value = () => (s.Active ? "" : "APAGADA  ") + (Math.Abs(s.Weight - 1f) > 0.001f ? "x" + s.Weight.ToString("0.##", CultureInfo.InvariantCulture) + "  " : "")
                                      + "FOV " + s.Fov.ToString("0") + "  " + (s.Duration > 0 ? Sec(s.Duration) : "gral"),
                        Submenu = () => CameraPage(s),
                        Help = () => "ENTER para ajustar esta camara"
                    });
                }
                items.Add(new MenuItem
                {
                    Label = "Importar camaras de Liberty's Legacy",
                    OnEnter = () =>
                    {
                        var log = new List<string>();
                        int n = CameraStore.ImportLibertysLegacy(host.GameFolder, Dir.Shots, log);
                        if (n > 0) Dir.Persist();
                        Menu.Toast(n > 0 ? "Se agregaron " + n + " camaras de Liberty's Legacy" : "No hay camaras nuevas en Liberty's Legacy\\Cameras");
                    },
                    Help = () => "Trae las camaras que guardaste con el trainer (F11). No repite las que ya estan"
                });
                items.Add(new MenuItem { Label = "Crear camara nueva (camara libre)", OnEnter = OpenEditor });
                return items;
            });
        }

        static readonly float[] WeightSteps = { 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f, 4f, 5f, 7f, 10f };

        /// <summary>Chance aproximada de que esta camara salga como la proxima (modo al azar).</summary>
        string ChancePercent(CameraShot s)
        {
            if (!s.Active) return "apagada";
            double total = 0;
            foreach (var x in Dir.Shots) if (x.Active && !x.IsAuto) total += Math.Max(0.01, x.Weight);
            if (total <= 0) return "-";
            return (100.0 * Math.Max(0.01, s.Weight) / total).ToString("0") + "%";
        }

        MenuPage CameraPage(CameraShot s)
        {
            return new MenuPage(s.Name, () => new List<MenuItem>
            {
                new MenuItem { Label = "Ver esta camara ahora", OnEnter = () => { Dir.GoTo(Dir.Shots.IndexOf(s)); Menu.Toast("Yendo a " + s.Name); } },
                new MenuItem { Label = "Usar en el loop", Value = () => s.Active ? "SI" : "NO", OnChange = d => { s.Active = !s.Active; Dir.Persist(); } },
                new MenuItem
                {
                    Label = "Duracion",
                    Value = () => s.Duration > 0 ? Sec(s.Duration) : "General (" + Sec(Ini.GetFloat("CamaraStream", "DuracionCiudadSegundos", Ini.GetFloat("Director", "DuracionPorDefecto", 300))) + ")",
                    OnChange = d =>
                    {
                        float v = s.Duration > 0 ? s.Duration : 0;
                        v += d * 5;
                        s.Duration = v < 5 ? -1f : Math.Min(3600f, v);
                        Dir.Persist();
                    },
                    Help = () => "Izquierda hasta 'General' para usar la duracion de Escenas y transiciones"
                },
                new MenuItem
                {
                    Label = "Zoom (FOV)",
                    Value = () => s.Fov.ToString("0"),
                    OnChange = d => { s.Fov = Clamp(s.Fov + d, 3, 120); if (s.EndFov > 0) s.EndFov = Clamp(s.EndFov + d, 3, 120); Dir.Persist(); },
                    Help = () => "Si la camara se esta viendo, el cambio se ve en vivo"
                },
                new MenuItem
                {
                    Label = "Chance en modo al azar",
                    Value = () => "x" + s.Weight.ToString("0.##", CultureInfo.InvariantCulture) + "  (" + ChancePercent(s) + ")",
                    OnChange = d =>
                    {
                        int i = 0;
                        while (i < WeightSteps.Length - 1 && WeightSteps[i] < s.Weight - 0.001f) i++;
                        i = Math.Max(0, Math.Min(WeightSteps.Length - 1, i + Math.Sign(d)));
                        s.Weight = WeightSteps[i];
                        Dir.Persist();
                    },
                    Help = () => Config.Normalize(Ini.Get("Director", "Orden", "")).StartsWith("alea")
                        ? "Mas alto = sale mas seguido. El % es la chance de que sea la proxima camara"
                        : "Solo cuenta con 'Orden de las camaras: Al azar' (Escenas y transiciones)"
                },
                new MenuItem
                {
                    Label = "Transicion al llegar a esta camara",
                    Value = () => s.Transition == 0 ? "Fundido" : s.Transition == 1 ? "Corte directo" : "General",
                    OnChange = d => { s.Transition = s.Transition >= 1 ? -1 : s.Transition + 1; Dir.Persist(); }
                },
                new MenuItem
                {
                    Label = "Hora del dia",
                    Value = () => TimeLabel(TimeSlot(s.Hour, s.Minute)),
                    OnChange = d =>
                    {
                        int slot = NextSlot(TimeSlot(s.Hour, s.Minute), d);
                        if (slot < 0) { s.Hour = -1; s.Minute = 0; } else { s.Hour = slot / 2; s.Minute = slot % 2 == 0 ? 0 : 30; }
                        Dir.Persist();
                    },
                    Help = () => "Normal = la hora corre como siempre (o la Hora fija general)"
                },
                new MenuItem
                {
                    Label = "Clima",
                    Value = () => WeatherLabels[s.Weather + 1],
                    OnChange = d => { s.Weather = NextWeather(s.Weather, d); Dir.Persist(); }
                },
                new MenuItem
                {
                    Label = "Movimiento de camara en mano",
                    Value = () => s.Sway >= 0 ? s.Sway.ToString("0.00", CultureInfo.InvariantCulture) : "General",
                    OnChange = d =>
                    {
                        float v = s.Sway < 0 ? -0.05f : s.Sway;
                        v = (float)Math.Round(v + 0.05f * Math.Sign(d), 2);
                        s.Sway = v < 0 ? -1f : Math.Min(2f, v);
                        Dir.Persist();
                    }
                },
                new MenuItem
                {
                    Label = "Mover arriba en la lista",
                    OnEnter = () => { int i = Dir.Shots.IndexOf(s); if (i > 0) { Dir.Shots.RemoveAt(i); Dir.Shots.Insert(i - 1, s); Dir.Persist(); Menu.Toast("Ahora es la numero " + i); } }
                },
                new MenuItem
                {
                    Label = "Mover abajo en la lista",
                    OnEnter = () => { int i = Dir.Shots.IndexOf(s); if (i >= 0 && i < Dir.Shots.Count - 1) { Dir.Shots.RemoveAt(i); Dir.Shots.Insert(i + 1, s); Dir.Persist(); Menu.Toast("Ahora es la numero " + (i + 2)); } }
                },
                new MenuItem
                {
                    Label = "Borrar esta camara",
                    OnEnter = () =>
                    {
                        if (!Confirm(s, "borrar '" + s.Name + "'")) return;
                        Dir.Shots.Remove(s);
                        Dir.Persist();
                        Menu.Back();
                        Menu.Toast("Camara borrada");
                    }
                }
            });
        }

        // ------------------------------------------------------------------
        static readonly string[,] EventDefs =
        {
            { "Suscripcion", "Nueva suscripcion / re-sub" },
            { "RegaloSubs", "Subs regaladas" },
            { "Follow", "Follow nuevo" },
            { "Host", "Host / raid" },
            { "KicksGifted", "Kicks (regalos de Kick)" },
            { "Ban", "Un mod banea a alguien" },
            { "Mensaje", "Cualquier mensaje del chat" },
            { "PrimerMensaje", "Alguien escribe por primera vez" },
            { "ChatAFull", "El chat explota (muchos mensajes)" },
            { "RegaloGrande", "Regalo grande de subs" },
            { "KicksGrandes", "Kicks grandes" },
            { "RewardRedeemedEvent", "Canjean una recompensa del canal" },
            { "MensajeFijado", "Fijan un mensaje en el chat" },
            { "Encuesta", "Empieza una encuesta" }
        };

        MenuPage Events()
        {
            return new MenuPage("Eventos de Kick", () =>
            {
                var items = new List<MenuItem>();
                var keys = new List<string>();
                for (int i = 0; i < EventDefs.GetLength(0); i++)
                {
                    string key = EventDefs[i, 0], label = EventDefs[i, 1];
                    keys.Add(key);
                    items.Add(EventItem(key, label));
                }
                // eventos raros que llegaron del chat (por si Kick agrega cosas nuevas)
                foreach (string name in host.SeenOtherEvents())
                {
                    if (keys.Contains(name)) continue;
                    keys.Add(name);
                    items.Add(EventItem(name, "Otro: " + name));
                }
                foreach (var kv in Cfg.EventMap)
                    if (!keys.Contains(kv.Key)) { keys.Add(kv.Key); items.Add(EventItem(kv.Key, kv.Key)); }
                return items;
            });
        }

        MenuItem EventItem(string key, string label)
        {
            return new MenuItem
            {
                Label = label,
                Value = () => { List<ActionStep> st; return Cfg.EventMap.TryGetValue(key, out st) ? Steps(st) : "Nada"; },
                Submenu = () => EventPage(key, label)
            };
        }

        MenuPage EventPage(string key, string label)
        {
            return new MenuPage(label, () =>
            {
                List<ActionStep> st;
                Cfg.EventMap.TryGetValue(key, out st);
                var items = new List<MenuItem>();
                for (int i = 0; i < 3; i++)
                {
                    int slot = i;
                    items.Add(new MenuItem
                    {
                        Label = "Accion " + (slot + 1),
                        Value = () => { List<ActionStep> cur; Cfg.EventMap.TryGetValue(key, out cur); return SlotName(Slots(cur)[slot]); },
                        OnChange = d =>
                        {
                            List<ActionStep> cur;
                            Cfg.EventMap.TryGetValue(key, out cur);
                            string[] s = Slots(cur);
                            s[slot] = CycleAction(s[slot], d);
                            Set("Eventos", key, Join(s));
                        },
                        Help = () => "Izquierda/derecha para elegir. Se pueden poner hasta 3 cosas a la vez"
                    });
                }
                if (key == "RegaloSubs")
                    items.Add(Bool("Repetir por cada sub regalada", "Reglas", "RepetirPorCadaRegalo", true, "5 subs regaladas = 5 veces (hasta el maximo de repeticiones)"));
                if (key == "Mensaje")
                    items.Add(new MenuItem { Info = true, Label = "Respeta el cooldown por persona y el de la accion" });
                if (key == "PrimerMensaje")
                    items.Add(new MenuItem { Info = true, Label = "Una vez por persona desde que abriste el juego" });
                if (key == "ChatAFull")
                {
                    items.Add(Num("Cuantos mensajes", "Reglas", "ChatAFullMensajes", 15, 2, 200, 1, v => v.ToString("0"), "Mensajes que tienen que llegar..."));
                    items.Add(Num("En cuantos segundos", "Reglas", "ChatAFullSegundos", 20, 3, 120, 1, Sec, "...dentro de este tiempo"));
                    items.Add(Num("Esperar entre una y otra", "Reglas", "ChatAFullCooldown", 120, 0, 1800, 10, Sec, "Para que no se dispare todo el tiempo"));
                }
                if (key == "RegaloGrande")
                    items.Add(Num("Desde cuantas subs", "Reglas", "RegaloGrandeDesde", 5, 2, 100, 1, v => v.ToString("0") + " subs",
                        "Regalos de esta cantidad o mas usan este evento en vez de 'Subs regaladas' (pasa una vez, no por cada sub)"));
                if (key == "KicksGrandes")
                    items.Add(Num("Desde cuantos kicks", "Reglas", "KicksGrandesDesde", 100, 1, 100000, 10, v => v.ToString("0"),
                        "Regalos de kicks de esta cantidad o mas usan este evento en vez de 'Kicks'"));
                if (key == "RewardRedeemedEvent")
                    items.Add(new MenuItem { Info = true, Label = "Si Kick lo manda con otro nombre, aparece abajo como 'Otro'" });
                items.Add(new MenuItem { Label = "Probar este evento", OnEnter = () =>
                {
                    string extra = key == "RegaloSubs" ? ":5" : key == "RegaloGrande" ? ":10" : key == "KicksGrandes" ? ":500" : "";
                    host.RunTest("evento:" + key + extra);
                    Menu.Toast("Evento simulado");
                } });
                return items;
            });
        }

        // ------------------------------------------------------------------
        MenuPage Commands()
        {
            return new MenuPage("Comandos del chat", () =>
            {
                var items = new List<MenuItem>();
                items.Add(new MenuItem
                {
                    Label = "+ Agregar palabra o comando",
                    OnEnter = () => Menu.StartTyping("Escribi la palabra (con ! adelante si es comando, ej: !boom)", "!", text =>
                    {
                        string key = text.Trim();
                        if (key.Length == 0 || key == "!") return;
                        ChatTrigger existing = FindTrigger(key);
                        if (existing != null) { Menu.Toast("'" + key + "' ya existe"); Menu.Push(CommandPage(existing.RawKey)); return; }
                        Set("Comandos", key, "Explosion");
                        ChatTrigger t = FindTrigger(key);
                        if (t != null) Menu.Push(CommandPage(key));
                        Menu.Toast("Agregado '" + key + "'. Elegi que hace");
                    }),
                    Help = () => "Con ! adelante: el mensaje tiene que empezar asi. Sin !: alcanza con que la palabra aparezca"
                });
                foreach (ChatTrigger trig in Cfg.Triggers)
                {
                    string raw = trig.RawKey;
                    items.Add(new MenuItem
                    {
                        Label = raw,
                        Value = () => { var t = FindTrigger(raw); return t == null ? "" : Steps(t.Steps) + RoleTag(t.RequiredLevel); },
                        Submenu = () => CommandPage(raw)
                    });
                }
                return items;
            });
        }

        ChatTrigger FindTrigger(string raw)
        {
            foreach (var t in Cfg.Triggers) if (string.Equals(t.RawKey, raw, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        static readonly string[] RoleWords = { "todos", "sub", "vip", "mod", "streamer" };
        static readonly string[] RoleLabels = { "Todos", "Subs", "VIP", "Mods", "Solo yo" };
        static string RoleTag(int lvl) { return lvl > 0 ? "  (" + RoleLabels[Math.Min(4, lvl)] + ")" : ""; }

        void SaveTrigger(string raw, string[] slots, int level, float cd)
        {
            string v = Join(slots);
            if (level > 0 || cd >= 0) v += " | " + RoleWords[Math.Max(0, Math.Min(4, level))];
            if (cd >= 0) v += " | " + IniFile.F(cd);
            Set("Comandos", raw, v);
        }

        MenuPage CommandPage(string rawKey)
        {
            string raw = rawKey;
            return new MenuPage(raw, () =>
            {
                var items = new List<MenuItem>();
                items.Add(new MenuItem
                {
                    Info = true,
                    DynamicLabel = () => raw.StartsWith("!") ? "El mensaje tiene que empezar con " + raw : "Se activa si '" + raw + "' aparece en el mensaje"
                });
                items.Add(new MenuItem
                {
                    Label = "Cambiar la palabra",
                    Value = () => raw,
                    OnEnter = () => Menu.StartTyping("Nueva palabra para '" + raw + "'", raw, text =>
                    {
                        string nk = text.Trim();
                        if (nk.Length == 0 || FindTrigger(nk) != null) { Menu.Toast("Esa palabra ya existe"); return; }
                        var doc = IniDoc.Load(Path.Combine(host.Folder, "config.ini"));
                        doc.RenameKey("Comandos", raw, nk);
                        doc.Save();
                        host.SoftReload();
                        raw = nk;
                        Menu.Back();
                        Menu.Push(CommandPage(nk));
                    })
                });
                for (int i = 0; i < 3; i++)
                {
                    int slot = i;
                    items.Add(new MenuItem
                    {
                        Label = "Accion " + (slot + 1),
                        Value = () => { var t = FindTrigger(raw); return t == null ? "" : SlotName(Slots(t.Steps)[slot]); },
                        OnChange = d =>
                        {
                            var t = FindTrigger(raw);
                            if (t == null) return;
                            string[] s = Slots(t.Steps);
                            s[slot] = CycleAction(s[slot], d);
                            SaveTrigger(raw, s, t.RequiredLevel, t.CooldownSeconds);
                        }
                    });
                }
                items.Add(new MenuItem
                {
                    Label = "Quien puede usarlo",
                    Value = () => { var t = FindTrigger(raw); return t == null ? "" : RoleLabels[Math.Min(4, t.RequiredLevel)]; },
                    OnChange = d =>
                    {
                        var t = FindTrigger(raw);
                        if (t == null) return;
                        int lvl = ((t.RequiredLevel + Math.Sign(d)) % 5 + 5) % 5;
                        SaveTrigger(raw, Slots(t.Steps), lvl, t.CooldownSeconds);
                    },
                    Help = () => "Subs incluye a VIP, mods y vos"
                });
                items.Add(new MenuItem
                {
                    Label = "Cooldown propio",
                    Value = () => { var t = FindTrigger(raw); return t == null ? "" : t.CooldownSeconds >= 0 ? Sec(t.CooldownSeconds) : "El de la accion"; },
                    OnChange = d =>
                    {
                        var t = FindTrigger(raw);
                        if (t == null) return;
                        float v = t.CooldownSeconds < 0 ? -5f : t.CooldownSeconds;
                        v += 5 * d;
                        SaveTrigger(raw, Slots(t.Steps), t.RequiredLevel, v < 0 ? -1f : Math.Min(3600f, v));
                    },
                    Help = () => "Segundos hasta que el chat lo pueda volver a usar. Izquierda hasta el final = el de la accion"
                });
                items.Add(new MenuItem { Label = "Probar (como si lo escribiera alguien)", OnEnter = () => { host.SimulateChat("prueba", raw); Menu.Toast("Mensaje simulado: " + raw); } });
                items.Add(new MenuItem
                {
                    Label = "Borrar este comando",
                    OnEnter = () =>
                    {
                        if (!Confirm(raw, "borrar '" + raw + "'")) return;
                        var doc = IniDoc.Load(Path.Combine(host.Folder, "config.ini"));
                        doc.Remove("Comandos", raw);
                        doc.Save();
                        host.SoftReload();
                        Menu.Back();
                        Menu.Toast("Borrado");
                    }
                });
                return items;
            });
        }

        // ------------------------------------------------------------------
        // NPC de suscriptores
        // ------------------------------------------------------------------
        const string SU = "Suscriptor";

        bool EventHasNpc(string key)
        {
            List<ActionStep> st;
            if (!Cfg.EventMap.TryGetValue(key, out st)) return false;
            foreach (var x in st) { string c = ActionNames.Canonical(x.Name); if (c != null && c.StartsWith("Npc")) return true; }
            return false;
        }

        /// <summary>Agrega o saca el NPC ("Npc", "NpcPasear"...) de las acciones de un evento (sin tocar las demas).</summary>
        void ToggleEventNpc(string key)
        {
            List<ActionStep> cur;
            Cfg.EventMap.TryGetValue(key, out cur);
            var list = new List<string>(Slots(cur));
            bool had = list.RemoveAll(x =>
            {
                string n = x.IndexOf('*') > 0 ? x.Substring(0, x.IndexOf('*')) : x;
                string c = ActionNames.Canonical(n);
                return c != null && c.StartsWith("Npc");
            }) > 0;
            if (!had) list.Insert(0, "Npc");
            Set("Eventos", key, Join(list.ToArray()));
        }

        string NpcSummary()
        {
            string on = EventHasNpc("Suscripcion") ? "PRENDIDO" : "APAGADO";
            int n = host.Npcs != null ? host.Npcs.AliveCount : 0;
            return n > 0 ? on + " (" + n + " en la calle)" : on;
        }

        MenuItem ModeItem(string label, string key, string legacyKey, string def, string help)
        {
            Func<int> cur = () =>
            {
                string v = Ini.Get(SU, key, "");
                NpcBehavior b = NpcRules.Resolve(v, NpcRules.Resolve(Ini.Get(SU, legacyKey, def), NpcRules.Resolve(def, NpcBehavior.Batalla)));
                return (int)b;
            };
            return new MenuItem
            {
                Label = label,
                Value = () => NpcRules.BehaviorLabels[cur()],
                OnChange = d => Set(SU, key, NpcRules.BehaviorKeys[((cur() + Math.Sign(d)) % NpcRules.Count + NpcRules.Count) % NpcRules.Count]),
                Help = () => help + ". Batalla: se busca con las otras bandas, se matan y suben de nivel (y la policia, si esta prendida). Paseo: anda por la ciudad (a pie o en auto) y nadie lo ataca"
            };
        }

        static string Months(int n) { return n == 1 ? "1 mes" : n + " meses"; }

        string TierLabel(int tier)
        {
            int[] f = host.Npcs != null ? host.Npcs.TierFrom : GangLevels.DefaultTierFrom;
            if (tier >= f.Length - 1) return "Sub de " + Months(f[f.Length - 1]) + " o mas";
            int lo = f[tier], hi = f[tier + 1] - 1;
            return lo >= hi ? "Sub de " + Months(lo) : "Sub de " + lo + " a " + Months(hi);
        }

        MenuItem WeaponItem(Func<string> label, string key, string def, string help)
        {
            Func<int> cur = () =>
            {
                int w = GangLevels.ParseWeapon(Ini.Get(SU, key, def));
                if (w < 0) w = GangLevels.ParseWeapon(def);
                int i = GangLevels.WeaponIndex(w);
                return i < 0 ? 0 : i;
            };
            return new MenuItem
            {
                DynamicLabel = label,
                Value = () => GangLevels.WeaponLabels[cur()],
                OnChange = d =>
                {
                    int n = GangLevels.WeaponKeys.Length;
                    Set(SU, key, GangLevels.WeaponKeys[((cur() + Math.Sign(d)) % n + n) % n]);
                },
                Help = () => help
            };
        }

        MenuPage NpcPage()
        {
            return new MenuPage("NPCs", () => new List<MenuItem>
            {
                new MenuItem { Info = true, DynamicLabel = () => "Vivos: " + host.Npcs.AliveCount + " / permanentes hasta morir" },
                new MenuItem { Label = "NPCs vivos (buscar con CTRL+F)", Submenu = LiveNpcsPage },
                Num("Maximo de bandas / follows", SU, "Maximo", 32, 1, 64, 1, v => v.ToString("0"), "Una vez lleno, se rechaza spawn; se conserva a los vivos"),
                Num("Maximo total de personajes", SU, "MaximoPersonajes", 48, 1, 96, 1, v => v.ToString("0"), "Incluye duplicados; mas personajes exige mas a GTA IV"),
                Choice("Build inicial", "StreamingNPC", "BuildInicial", new[] { "AGRESIVO", "CAZADOR", "SUPERVIVIENTE" }, new[] { "AGRESIVO", "CAZADOR", "SUPERVIVIENTE" }, "SUPERVIVIENTE", "El propietario puede elegir !agresivo, !cazador o !superviviente"),
                Num("Vida principal", "StreamingNPC", "VidaPrincipal", 1.35f, 0.5f, 5, 0.05f, v => "x" + v.ToString("0.00", CultureInfo.InvariantCulture), "Principal mas fuerte; persiste hasta morir"),
                Num("Vida duplicados", "StreamingNPC", "VidaDuplicado", 0.65f, 0.2f, 1, 0.05f, v => "x" + v.ToString("0.00", CultureInfo.InvariantCulture), "Mejoras reducidas respecto al principal"),
                Num("Vida follower", "StreamingNPC", "VidaFollow", 160, 80, 1000, 10, v => v.ToString("0"), "Una sola vida y sin duplicados"),
                ModeItem("Modo del sub", "ModoSub", "Nivel1", "Batalla", "Combate solo durante encuentros"),
                ModeItem("Modo del regalo", "ModoRegalo", "Regalo", "Batalla", ""),
                ModeItem("Modo del follow", "ModoFollow", "Otros", "Pasear", "Pasean y pueden sumarse a batallas cercanas"),
                Bool("Followers se suman a batallas", "StreamingNPC", "FollowersEnBatallas", true, "Pueden chocar y disparar desde el auto cuando encuentran una pelea"),
                Bool("Paseadores invulnerables", SU, "PaseoIntocable", false, "NO recomendado: una sola vida para followers"),
                Bool("Cambio de modo por chat", SU, "CambiarModoPorChat", true, "!pasear / !batalla"),
                new MenuItem { Label = "Control y mensajes de chat", Submenu = NpcControlPage },
                new MenuItem { Label = "Comandos divertidos", Submenu = FunPage },
                new MenuItem { Info = true, Label = "Duplicado libre: !unirme<ID> / camara propia: !npc" }
            });
        }

        MenuPage LiveNpcsPage()
        {
            return new MenuPage("NPCs vivos", () =>
            {
                var list = new List<MenuItem>();
                StreamSnapshot snapshot = StreamRuntime.Snapshot;
                if (snapshot != null && snapshot.Npcs != null)
                    foreach (StreamNpcRow row in snapshot.Npcs)
                    {
                        int id = row.Id;
                        list.Add(new MenuItem { Label = row.Name + " [" + row.Kind + "]", Value = () => StreamHudPolicy.Stats(FindNpc(id) ?? row), Submenu = () => LiveNpcPage(id) });
                    }
                if (list.Count == 0) list.Add(new MenuItem { Info = true, Label = "No hay NPCs vivos" });
                return list;
            });
        }

        StreamNpcRow FindNpc(int id)
        {
            StreamSnapshot snapshot = StreamRuntime.Snapshot;
            if (snapshot != null && snapshot.Npcs != null) foreach (StreamNpcRow row in snapshot.Npcs) if (row.Id == id) return row;
            return null;
        }

        void NpcAdmin(int id, string action, string value = "", string zone = "")
        {
            string message;
            host.Npcs.AdminAction(new StreamAdminCommand { NpcId = id, Action = action, Value = value, Zone = zone }, out message);
            Menu.Toast(message);
        }

        MenuPage LiveNpcPage(int id)
        {
            return new MenuPage("NPC #" + id, () => new List<MenuItem>
            {
                new MenuItem { Info = true, DynamicLabel = () => { StreamNpcRow row = FindNpc(id); return row == null ? "Ya no esta vivo" : row.Name + " [" + row.Kind + "]"; } },
                new MenuItem { Info = true, DynamicLabel = () => { StreamNpcRow row = FindNpc(id); return row == null ? "-" : StreamHudPolicy.Stats(row); } },
                new MenuItem { Info = true, DynamicLabel = () => { StreamNpcRow row = FindNpc(id); return row == null ? "-" : row.State + " / " + row.Objective; } },
                new MenuItem { Label = "Observar 15 segundos", OnEnter = () => { StreamNpcRow row = FindNpc(id); Menu.Toast(row != null && Dir.ObserveNpc(row.Ped, 15, "menu", 100, true) ? "Observando " + row.Name : "NPC no disponible"); } },
                new MenuItem { Label = "Fijar camara en este NPC", OnEnter = () => { StreamNpcRow row = FindNpc(id); Menu.Toast(row != null && Dir.PinNpc(row.Ped) ? "Camara fijada" : "NPC no disponible"); } },
                new MenuItem { Label = "Build AGRESIVO", OnEnter = () => NpcAdmin(id, "build", "AGRESIVO") },
                new MenuItem { Label = "Build CAZADOR", OnEnter = () => NpcAdmin(id, "build", "CAZADOR") },
                new MenuItem { Label = "Build SUPERVIVIENTE", OnEnter = () => NpcAdmin(id, "build", "SUPERVIVIENTE") },
                new MenuItem { Label = "Modo paseo", OnEnter = () => NpcAdmin(id, "behavior", "Pasear") },
                new MenuItem { Label = "Modo batalla", OnEnter = () => NpcAdmin(id, "behavior", "Batalla") },
                new MenuItem { Label = "Rescatar si quedo trabado", OnEnter = () => NpcAdmin(id, "relocate") },
                new MenuItem { Label = "Teletransportar", Submenu = () => NpcTeleportPage(id) },
                new MenuItem { Label = "Diagnostico de este NPC", OnEnter = () => NpcAdmin(id, "debug") },
                new MenuItem { Label = "Eliminar este NPC", OnEnter = () => { if (Confirm("npc" + id, "eliminar este NPC")) NpcAdmin(id, "remove"); } }
            });
        }

        MenuPage NpcTeleportPage(int id)
        {
            return new MenuPage("Teletransportar NPC #" + id, () =>
            {
                var items = new List<MenuItem>();
                foreach (string zone in new[] { "cámara", "Broker", "Algonquin", "Alderney", "Bohan" })
                {
                    string selected = zone;
                    items.Add(new MenuItem { Label = selected, OnEnter = () => NpcAdmin(id, "teleport", "", selected), Help = () => "La zona debe estar cargada; conserva vida, kills y mejoras" });
                }
                return items;
            });
        }

        MenuPage PolicePage()
        {
            return new MenuPage("Policia", () => new List<MenuItem>
            {
                Bool("Policia habilitada", SU, "Policia", true, "Interviene en encuentros y delitos, sin forzar caos permanente"),
                Num("Patrulleros", SU, "Patrulleros", 1, 0, 6, 1, v => v.ToString("0"), "Refuerzos maximos"),
                Num("Probabilidad tras robo", "StreamingNPC", "ProbabilidadPoliciaRobo", 10, 0, 100, 5, v => v.ToString("0") + "%", ""),
                Num("Intervalo tras robo", "StreamingNPC", "IntervaloPoliciaRobo", 360, 30, 3600, 30, Sec, ""),
                Bool("Policia agresiva", SU, "PoliciaAgresiva", true, "Les tiran de verdad: rafaga, se mueven al costado y hacia adelante, otra rafaga. NO = el combate del juego (como antes)"),
                Bool("Policia disfrazada", SU, "PoliciaDisfrazada", true, "Los del patrullero son personas comunes con ropa de policia: si pelean (los policias de verdad no)"),
                Bool("Se suman los de la calle", SU, "PoliciasDeLaCalle", true, "Los policias que andan cerca de un tiroteo se suman contra la banda (y despues se los suelta)"),
                WeaponItem(() => "Arma de la policia", "ArmaPolicia", "Pistola", "Con 4 estrellas o mas tambien usan escopeta y AK"),
            });
        }

        MenuPage AmmoPage()
        {
            return new MenuPage("Explosivos, municion y chaleco", () => new List<MenuItem>
            {
                Bool("Explosivos habilitados", SU, "UsanExplosivos", false, "Apagar para fondo mas tranquilo"),
                Bool("Granadas", SU, "UsanGranadas", true, ""),
                Bool("Molotov", SU, "UsanMolotov", true, ""),
                Bool("RPG", SU, "UsanRpg", true, "No lo tiran si hay uno de la banda cerca del objetivo o en el camino"),
                Bool("Explosion si no sale", SU, "ExplosivoSimulado", true, "Si la granada no sale (se queda quieto) o el cohete desaparece, explota igual en el objetivo"),
                Bool("Se mueven apuntando", SU, "MoverseApuntando", false, "Caminar apuntando al moverse. A veces los dejaba apuntando al cielo: NO = corren"),
                Bool("Municion limitada", SU, "MunicionLimitada", true, "Se les acaban las balas: sacan la pistola y despues pelean a las pinas. Las mejoras y niveles recargan"),
                Num("Cargadores", SU, "Cargadores", 6, 1, 30, 1, v => v.ToString("0"), "Cuantos cargadores traen de cada arma"),
                Num("Velocidad al correr", SU, "VelocidadBase", 1.08f, 0.9f, 1.4f, 0.02f, v => "x" + v.ToString("0.00", CultureInfo.InvariantCulture), ""),
                Num("Mejora velocidad: suma", SU, "VelocidadMejora", 0.05f, 0f, 0.2f, 0.01f, v => "+" + v.ToString("0.00", CultureInfo.InvariantCulture), "Antes era +0.12 (muy fuerte)"),
                Num("Velocidad maxima", SU, "VelocidadMaxima", 1.22f, 1f, 1.5f, 0.02f, v => "x" + v.ToString("0.00", CultureInfo.InvariantCulture), ""),
            });
        }

        MenuPage EventsPage()
        {
            return new MenuPage("Eventos al azar", () => new List<MenuItem>
            {
                Bool("Charlar con la gente", SU, "EventoCharlar", true, "Un NPC tranquilo se acerca a alguien de la calle y se pone a charlar"),
                Num("Charla cada", SU, "CharlaCada", 90, 20, 900, 10, Sec, ""),
                Num("Duracion de la charla", SU, "DuracionCharla", 12, 4, 60, 2, Sec, ""),
                Bool("Carreras", SU, "EventoCarrera", true, "Hasta 4 NPC van a una de tus camaras, cada uno en un auto, y corren hasta lo que mira otra camara. Tregua mientras corren"),
                Num("Carrera cada", SU, "CarreraCada", 300, 60, 1800, 30, Sec, ""),
                Num("Corredores", SU, "CorredoresCarrera", 4, 2, 4, 1, v => v.ToString("0"), ""),
                Num("Tiempo maximo de la carrera", SU, "DuracionCarrera", 150, 40, 600, 10, Sec, ""),
                Num("Velocidad en la carrera", SU, "VelocidadCarrera", 45, 20, 80, 5, v => v.ToString("0"), ""),
                new MenuItem { Label = "Probar: carrera ahora", OnEnter = () => Menu.Toast(host.Npcs.StartRaceNow() ? "Carrera!" : "Hacen falta 2 NPC (mira log.txt)") },
            });
        }

        MenuPage FunPage()
        {
            return new MenuPage("Comandos divertidos", () =>
            {
                var l = new List<MenuItem>();
                l.Add(Choice("Quien los usa", SU, "ComandosDivertidos", new[] { "Chat", "Suscriptor", "No" },
                    new[] { "El sub y el chat", "Solo el sub", "Nadie (apagados)" }, "Chat",
                    "El sub los usa con su banda. El chat: con la banda que esta en camara"));
                l.Add(Num("Espera entre comandos", SU, "EsperaDivertidos", 8, 0, 60, 1, Sec, "Por banda"));
                for (int i = 0; i < GangRules.FunKeys.Length; i++)
                    l.Add(Bool(GangRules.FunLabels[i], "Divertidos", GangRules.FunKeys[i], true, ""));
                return l;
            });
        }

        MenuPage WeaponsMonthsPage()
        {
            return new MenuPage("Armas segun los meses", () =>
            {
                var l = new List<MenuItem>();
                l.Add(new MenuItem { Info = true, Label = "Con que arma aparece en modo batalla (el que regala: 3 meses por sub)" });
                for (int i = 0; i < GangLevels.Tiers; i++)
                {
                    int t = i;
                    l.Add(WeaponItem(() => TierLabel(t), "Arma" + (t + 1), GangLevels.DefaultTierWeapons[t],
                        "Si elegis granadas, molotov o RPG, las tira de vez en cuando y en la mano lleva una pistola"));
                }
                l.Add(Num("Rango 2 desde", SU, "Nivel2Desde", 2, 2, 120, 1, v => Months((int)v), "Meses desde los que vale el arma del rango 2"));
                l.Add(Num("Rango 3 desde", SU, "Nivel3Desde", 6, 3, 120, 1, v => Months((int)v), ""));
                l.Add(Num("Rango 4 desde", SU, "Nivel4Desde", 12, 4, 120, 1, v => Months((int)v), ""));
                l.Add(Num("Rango 5 desde", SU, "Nivel5Desde", 24, 5, 120, 1, v => Months((int)v), "Cada rango tiene que empezar despues del anterior"));
                return l;
            });
        }

        MenuPage WeaponsLevelPage()
        {
            return new MenuPage("Armas que ganan por nivel", () =>
            {
                var l = new List<MenuItem>();
                l.Add(Bool("Ganan armas al subir de nivel", SU, "ArmasPorNivel", true, "Cada NPC de otra banda que matan les sube un nivel (hasta 10)"));
                for (int i = 0; i < GangLevels.DefaultLevelWeapons.Length; i++)
                {
                    int lv = i + 2;
                    l.Add(WeaponItem(() => "Nivel " + lv, "ArmaNivel" + lv, GangLevels.DefaultLevelWeapons[lv - 2],
                        "Granadas: +4, molotov: +4, RPG: +3 cohetes (para toda la banda). Un arma de mano: si es mejor, la llevan en la mano"));
                }
                return l;
            });
        }

        MenuPage PerksPage()
        {
            return new MenuPage("Mejoras al matar (roguelike)", () => new List<MenuItem>
            {
                new MenuItem { Info = true, Label = "Al matar aparecen 3 mejoras arriba del NPC: !1 !2 !3" },
                Bool("Mejoras al matar", SU, "Mejoras", true, "Chaleco, vida, mejor arma, granadas, RPG, molotovs, punteria, velocidad, refuerzo, regeneracion, escudo, +60 s"),
                Choice("Aparecen al matar a", SU, "MejoraCon", GangRules.GrowKeys, GangRules.GrowLabels, "Cualquiera", "A quien tienen que matar para que aparezcan mejoras"),
                Num("Tiempo para elegir", SU, "TiempoMejora", 20, 5, 60, 5, Sec, "Si nadie elige, sale una al azar"),
                new MenuItem { Label = "Cuales pueden salir", Submenu = PerkListPage },
                new MenuItem { Info = true, DynamicLabel = () => "Elige: " + GangRules.ModeLabels[GangRules.ParseMode(Ini.Get(SU, "QuienDecide", "Suscriptor"))] + " (se cambia en Control desde el chat)" }
            });
        }

        MenuPage PerkListPage()
        {
            return new MenuPage("Mejoras que pueden salir", () =>
            {
                var l = new List<MenuItem>();
                for (int i = 0; i < PerkRules.Count; i++)
                    l.Add(Bool(PerkRules.Labels[i], "Mejoras", PerkRules.Keys[i], true, PerkRules.Help[i]));
                return l;
            });
        }

        string GangSummary()
        {
            int max = Math.Max(1, Math.Min(6, Ini.GetInt(SU, "BandaMaximo", 4)));
            return "hasta " + max;
        }

        string ControlSummary()
        {
            int u = GangRules.ParseUnlock(Ini.Get(SU, "ControlDelChat", "AlDuplicarse"));
            if (u == GangRules.UnlockNever) return "NUNCA";
            return GangRules.ModeLabels[GangRules.ParseMode(Ini.Get(SU, "QuienDecide", "Suscriptor"))];
        }

        MenuPage NpcGangPage()
        {
            return new MenuPage("IA, encuentros y bandas", () => new List<MenuItem>
            {
                Choice("Inteligencia al pelear", SU, "IA", new[] { "Juego", "Mod" }, new[] { "IA del juego", "IA del mod" }, "Juego", "La IA nativa mantiene combate y cobertura; la IA del mod dirige pasos concretos"),
                Bool("Pelea dinamica (IA del mod)", SU, "PeleaDinamica", true, "Rafagas y coberturas"),
                Bool("Cobertura del juego", SU, "CoberturaDelJuego", true, "Paredes y columnas"),
                Bool("Entorno tranquilo", "StreamingNPC", "EntornoTranquilo", true, "Combates ocasionales; conserva viajes, charlas y momentos sin accion"),
                Num("Encuentro: evaluar cada", "StreamingNPC", "EncuentroCada", 120, 10, 3600, 10, Sec, ""),
                Num("Encuentro: probabilidad", "StreamingNPC", "EncuentroProbabilidad", 20, 0, 100, 5, v => v.ToString("0") + "%", ""),
                Num("Encuentro: radio", "StreamingNPC", "EncuentroRadio", 100, 10, 500, 10, v => v.ToString("0") + " m", "Solo enemigos cercanos"),
                Num("Duracion de encuentro", "StreamingNPC", "DuracionEncuentro", 60, 10, 300, 5, Sec, ""),
                Bool("Peleas entre bandas", SU, "PelearEntreEllos", true, "Se activa dentro de encuentros"),
                Bool("Atacan civiles", SU, "AtacanALaGente", false, "Apagado mantiene el fondo mas tranquilo"),
                Bool("Persecuciones en auto", SU, "PersecucionesEnAuto", true, "Conductor mantiene tarea; pasajero dispara con arma compatible"),
                Bool("Persecucion aleatoria", SU, "PersecucionAleatoria", true, "Tambien depende del ritmo de encuentros"),
                Num("Persecucion cada", SU, "PersecucionCada", 600, 30, 3600, 30, Sec, ""),
                Bool("Traer bandas lejanas", SU, "TraerBandas", false, "Apagado conserva encuentros naturales sin reubicar para pelear"),
                Bool("Spawn separado entre bandas", SU, "ApareceLejos", true, ""),
                Choice("Duplicar al matar", SU, "SeDuplicaCon", GangRules.GrowKeys, GangRules.GrowLabels, "Policias", "Followers no generan duplicados"),
                Num("Maximo por banda", SU, "BandaMaximo", 4, 1, 6, 1, v => v.ToString("0"), "Tambien limitado por MaximoPersonajes"),
                Num("Vida para escapar", SU, "VidaParaHuir", 35, 0, 80, 5, v => v <= 0 ? "Nunca" : v.ToString("0") + "%", ""),
                Bool("Comprar comida", SU, "ComprarComida", false, "Escapa para recuperarse"),
                Num("Tiempo comiendo", SU, "SegundosComiendo", 7, 2, 30, 1, Sec, "")
            });
        }

        MenuPage NpcControlPage()
        {
            return new MenuPage("Control desde el chat", () => new List<MenuItem>
            {
                new MenuItem { Info = true, DynamicLabel = () => "Arriba del NPC: " + GangRules.ChoicesLine(host.Npcs.Choices) + "  (o !1 !2 !3)" },
                Choice("Se activa", SU, "ControlDelChat", GangRules.UnlockKeys, GangRules.UnlockLabels, "AlDuplicarse",
                    "Cuando mata a uno y se duplica, el chat puede darle ordenes con ! (aparecen arriba de su cabeza)"),
                Choice("Quien decide", SU, "QuienDecide", GangRules.ModeKeys, GangRules.ModeLabels, "Suscriptor",
                    "Solo el suscriptor: sus ordenes se cumplen al toque. Vota el chat: cuenta los votos un rato y gana la mas votada. El chat, el primero: vale la primera orden de cualquiera"),
                Num("Tiempo de votacion", SU, "TiempoVotacion", 15, 5, 60, 5, Sec, "Arranca con el primer voto. Cada persona vota una vez (vale el ultimo)"),
                Num("Cuanto dura la orden", SU, "DuracionOrden", 30, 10, 120, 5, Sec, "Despues vuelve a hacer lo suyo (!banda dura al menos 60 s)"),
                Num("Espera entre ordenes", SU, "EsperaEntreOrdenes", 4, 0, 30, 1, Sec, "Para que no lo vuelvan loco"),
                SlotItem(0),
                SlotItem(1),
                SlotItem(2),
                new MenuItem
                {
                    Label = "Probar: sumar uno (desbloquea)",
                    OnEnter = () => Menu.Toast(host.Npcs.TestGrow() ? "Se sumo uno a la banda" : "No hay banda (o ya esta completa)")
                },
                new MenuItem
                {
                    Label = "Probar: el sub escribe en el chat",
                    OnEnter = () =>
                    {
                        string u = host.Npcs.NewestUser();
                        if (u == null) { Menu.Toast("No hay ningun NPC en la calle"); return; }
                        host.SimulateChat(u, "hola chat [emote:37226:KEKW] miren lo que hago con mi banda");
                        Menu.Toast("Mensaje de " + u);
                    }
                },
                new MenuItem
                {
                    DynamicLabel = () => "Probar: el sub escribe !" + GangRules.CmdKeys[host.Npcs.Choices[0]],
                    OnEnter = () =>
                    {
                        string u = host.Npcs.NewestUser();
                        if (u == null) { Menu.Toast("No hay ningun NPC en la calle"); return; }
                        host.SimulateChat(u, "!" + GangRules.CmdKeys[host.Npcs.Choices[0]]);
                        Menu.Toast("Orden de " + u + " (si el control esta desbloqueado)");
                    }
                }
            });
        }

        /// <summary>Una de las 3 ordenes del chat. Al cambiarla se saltean las que ya estan en las otras dos.</summary>
        MenuItem SlotItem(int i)
        {
            string key = "Opcion" + (i + 1);
            Func<int, int> slot = j =>
            {
                int k = GangRules.ParseKey(Ini.Get(SU, "Opcion" + (j + 1), GangRules.DefaultSlots[j]));
                return k >= 0 ? k : GangRules.ParseKey(GangRules.DefaultSlots[j]);
            };
            return new MenuItem
            {
                Label = "Opcion " + (i + 1),
                Value = () => GangRules.CmdLabels[slot(i)],
                OnChange = d =>
                {
                    int n = GangRules.CmdKeys.Length, c = slot(i);
                    var used = new HashSet<int>();
                    for (int j = 0; j < 3; j++) if (j != i) used.Add(slot(j));
                    for (int step = 0; step < n; step++)
                    {
                        c = ((c + Math.Sign(d)) % n + n) % n;
                        if (!used.Contains(c)) break;
                    }
                    Set(SU, key, GangRules.CmdKeys[c]);
                },
                Help = () => "Las 3 ordenes que se pueden escribir en el chat (no se repiten). Tambien valen !1 !2 !3"
            };
        }

        MenuPage NpcTagPage()
        {
            return new MenuPage("Cartel arriba de la cabeza", () => new List<MenuItem>
            {
                Bool("Mostrar el nombre arriba", SU, "MostrarNombre", true, "Nombre en blanco y los meses (o 'LA BANDA DE ...') en verde Kick"),
                Bool("Mensajes del sub", SU, "MostrarMensajes", true, "Lo que escribe en el chat (sin !) aparece arriba de su personaje unos segundos"),
                Bool("Barra de vida", SU, "BarraDeVida", true, "Se va vaciando cuando le pegan"),
                Bool("Municion", SU, "MostrarMunicion", true, ""),
                Bool("Barra de chaleco", SU, "BarraDeChaleco", true, ""),
                Bool("Color de Kick", SU, "ColorDeKick", true, "Toma color del chat si esta disponible"),
                Num("Tamano del cartel", SU, "TamanoNombre", 1, 0.5f, 2.5f, 0.1f, v => "x" + v.ToString("0.0", CultureInfo.InvariantCulture), ""),
                Bool("Cartel del juego (online)", SU, "NombreDelJuego", false,
                    "En vez del cartel propio usa el de los nombres del modo online de GTA IV (sin meses, barra, reloj ni mensajes)")
            });
        }

        MenuPage NpcCameraPage()
        {
            return new MenuPage("Camara que lo sigue", () => new List<MenuItem>
            {
                Bool("Ir a su camara al aparecer", SU, "IrAlAparecer", true, "La camara se va con el apenas aparece (con fundido)"),
                Choice("Como aparece", SU, "AlAparecer", new[] { "CamaraYSeguir", "Camara", "Seguir" }, new[] { "En camara y lo sigue", "En camara", "La camara lo sigue" }, "CamaraYSeguir", "El delay de Kick se aplica antes del spawn"),
                Num("Espera visible antes de seguir", SU, "SegundosEnCamara", 5, 0, 30, 1, Sec, "Con 'En camara y lo sigue'"),
                Bool("Camara bajo puentes / tuneles", SU, "CamaraBajoTierra", true, "Busca encuadre seguro o descarta la toma temporalmente"),
                Num("Distancia de la camara", SU, "CamaraDistancia", 22, 5, 150, 1, v => v.ToString("0") + " m", "En auto se aleja un poco mas"),
                Num("Altura de la camara", SU, "CamaraAltura", 6, 1, 80, 1, v => v.ToString("0") + " m", "Si un edificio tapa, sube sola"),
                Num("Zoom de la camara (FOV)", SU, "CamaraFOV", 30, 5, 90, 1, v => v.ToString("0"), "FOV bajo = teleobjetivo"),
                Bool("Camara al tiroteo", SU, "CamaraAlTiroteo", true, "Cuando empieza un tiroteo, la camara va ahi y encuadra a todos (con 'Seguir NPC' puesto, se queda con el)"),
                Bool("Camara apunta al objetivo", SU, "CamaraApuntaAlObjetivo", true, "Cuando dispara, la camara se pone atras de el y se ve a quien le tira"),
                Bool("Camara dinamica", SU, "CamaraDinamica", true,
                    "Cada 5-9 s cambia de toma: lo rodea, se acerca, va de costado, sube, se pone de frente. A los tiros prefiere las tomas cerca"),
            });
        }

        // ------------------------------------------------------------------
        string RepairSummary()
        {
            float t = Ini.GetFloat("Arreglar", "Tras", 60);
            return t <= 0 ? "NUNCA" : "a los " + Sec(t);
        }

        MenuPage RepairPage()
        {
            return new MenuPage("Arreglar la calle", () => new List<MenuItem>
            {
                new MenuItem
                {
                    Label = "Arreglar despues de",
                    Value = () => { float t = Ini.GetFloat("Arreglar", "Tras", 60); return t <= 0 ? "Nunca" : Sec(t); },
                    OnChange = d => Set("Arreglar", "Tras", IniFile.F(Clamp(Snap(Ini.GetFloat("Arreglar", "Tras", 60) + d * 5, 5), 0, 900))),
                    Help = () => "Segundos desde la ultima accion (sub, explosion...). Si el chat no para, igual arregla cada tanto. 0 = nunca"
                },
                Choice("Como arreglarla", "Arreglar", "Forma", new[] { "fundido", "cambio", "directo" },
                    new[] { "Fundido y vuelve a la misma camara", "En el proximo cambio de camara", "Al instante (se ve)" },
                    "Fundido: se va a negro un momento, se arregla y vuelve la misma toma"),
                Num("Radio", "Arreglar", "Radio", 70, 20, 250, 10, v => v.ToString("0") + " m", "Distancia alrededor de donde paso la accion"),
                Bool("Sacar escombros", "Arreglar", "Escombros", true, "Objetos rotos o tirados por las explosiones"),
                Bool("Sacar a la policia que quedo", "Arreglar", "Policias", true, "Patrulleros y policias que llegaron por las acciones"),
                new MenuItem
                {
                    Info = true,
                    DynamicLabel = () =>
                    {
                        double s = Dir.Repair.SecondsLeft();
                        return s < 0 ? "Todo limpio (arreglos hechos: " + Dir.Repair.Repairs + ")" : "Proximo arreglo en " + Math.Ceiling(s) + " s";
                    }
                },
                new MenuItem { Label = "Arreglar ahora", OnEnter = () => { if (Dir.Repair.IsDirty) { Dir.RepairSoon(); Menu.Toast("Arreglando la calle..."); } else Menu.Toast("No hay nada para arreglar"); } }
            });
        }

        // ------------------------------------------------------------------
        MenuPage Tests()
        {
            return new MenuPage("Probar acciones", () =>
            {
                var items = new List<MenuItem>();
                items.Add(new MenuItem { Label = "Simular una suscripcion", OnEnter = () => host.RunTest("evento:Suscripcion") });
                items.Add(new MenuItem { Label = "Simular 5 subs regaladas", OnEnter = () => host.RunTest("evento:RegaloSubs:5") });
                items.Add(new MenuItem { Label = "Simular un follow", OnEnter = () => host.RunTest("evento:Follow") });
                items.Add(new MenuItem { Label = "Simular un host / raid", OnEnter = () => host.RunTest("evento:Host") });
                items.Add(new MenuItem { Label = "NPC de suscriptor (6 meses)", OnEnter = () => { host.TestNpc("Suscripcion", 6); Menu.Toast("NPC en cola"); } });
                foreach (string a in ActionNames.Choices)
                {
                    if (a == "Nada") continue;
                    string act = a;
                    items.Add(new MenuItem { Label = ActionNames.Pretty(act), OnEnter = () => { host.RunTest(act); Menu.Toast(ActionNames.Pretty(act) + " en cola"); } });
                }
                items.Add(new MenuItem { Label = "Vaciar la cola de acciones", Value = () => host.QueueCount() + " en espera", OnEnter = () => host.ClearQueue() });
                return items;
            });
        }

        // ------------------------------------------------------------------
        MenuPage Rules()
        {
            return new MenuPage("Reglas y cooldowns", () => new List<MenuItem>
            {
                Num("Cooldown por persona", "Reglas", "CooldownPorUsuario", 15, 0, 600, 1, Sec, "Cada persona del chat puede disparar algo cada X segundos (vos no tenes limite)"),
                Num("Tiempo entre acciones", "Reglas", "SeparacionEntreAcciones", 2, 0, 60, 0.5f, Sec, "Separacion entre una accion y la siguiente de la cola"),
                Num("Maximo de acciones en espera", "Reglas", "ColaMaxima", 15, 1, 100, 1, v => v.ToString("0"), "Si se llena, se ignoran los pedidos del chat (los subs entran igual)"),
                Num("Maximo de repeticiones", "Reglas", "MaxRepeticiones", 10, 1, 50, 1, v => v.ToString("0"), "Tope para Explosion*N y para las subs regaladas"),
                Bool("Subs y eventos ignoran cooldowns", "Reglas", "EventosIgnoranCooldown", true, ""),
                Bool("Una accion por mensaje", "Reglas", "UnaAccionPorMensaje", true, "Si un mensaje tiene varias palabras clave, solo cuenta la primera"),
                Bool("Mostrar 'usuario -> accion' en el juego", "Reglas", "AvisoEnJuego", false, "Subtitulo dentro del juego. El texto para OBS se escribe siempre"),
                new MenuItem { Label = "Cooldown de cada accion", Submenu = ActionCooldowns }
            });
        }

        MenuPage ActionCooldowns()
        {
            return new MenuPage("Cooldown de cada accion", () =>
            {
                var items = new List<MenuItem>();
                foreach (string a in ActionNames.Choices)
                {
                    if (a == "Nada") continue;
                    items.Add(Num(ActionNames.Pretty(a), "Cooldowns", a, TriggerEngine.DefaultActionCooldown, 0, 3600, 5, Sec, "Segundos hasta que el chat la pueda repetir"));
                }
                return items;
            });
        }

        // ------------------------------------------------------------------
        MenuPage World()
        {
            return new MenuPage("Hora, clima y trafico", () => new List<MenuItem>
            {
                Bool("Hora real (la de Windows)", "Director", "HoraReal", true,
                    "La hora del juego es la de tu PC (le gana a la hora fija y a la de cada camara). Las acciones del chat que cambian la hora igual funcionan un rato"),
                new MenuItem
                {
                    Label = "Hora fija",
                    Value = () => { int h, m; CameraStore.ParseTime(Ini.Get("Director", "HoraFija", ""), out h, out m); return TimeLabel(TimeSlot(h, m)); },
                    OnChange = d =>
                    {
                        int h, m;
                        CameraStore.ParseTime(Ini.Get("Director", "HoraFija", ""), out h, out m);
                        int slot = NextSlot(TimeSlot(h, m), d);
                        Set("Director", "HoraFija", slot < 0 ? "" : TimeLabel(slot));
                    },
                    Help = () => "Se aplica en cada cambio de camara (las camaras con hora propia usan la suya)"
                },
                new MenuItem
                {
                    Label = "Clima fijo",
                    Value = () => WeatherLabels[CameraStore.ParseWeather(Ini.Get("Director", "ClimaFijo", "")) + 1],
                    OnChange = d =>
                    {
                        int w = NextWeather(CameraStore.ParseWeather(Ini.Get("Director", "ClimaFijo", "")), d);
                        Set("Director", "ClimaFijo", w < 0 ? "" : CameraStore.WeatherName(w));
                    }
                },
                Num("Cantidad de peatones", "Director", "DensidadPeatones", 1, 0, 1, 0.1f, v => (v * 100).ToString("0") + "%", ""),
                Num("Cantidad de trafico", "Director", "DensidadTrafico", 1, 0, 1, 0.1f, v => (v * 100).ToString("0") + "%", "Menos trafico = menos choques"),
                Bool("Sacar autos chocados sin que se note", "Trafico", "SacarChocados", true,
                    "Saca autos chocados, dados vuelta, en llamas o trabados cuando la camara no los ve"),
                Choice("Al cambiar de camara", "Trafico", "AlCambiarCamara", new[] { "chocados", "todo", "nada" },
                    new[] { "Sacar chocados y trabados", "Vaciar la calle", "No tocar nada" },
                    "Se hace mientras la pantalla esta en negro. 'Vaciar la calle' saca todos los autos y el trafico vuelve a entrar"),
                Num("Radio de limpieza", "Trafico", "Radio", 90, 20, 250, 10, v => v.ToString("0") + " m", "Distancia alrededor del lugar de la escena"),
                new MenuItem { Info = true, DynamicLabel = () => "Autos sacados en esta sesion: " + Dir.Traffic.Removed },
                Num("Altura de Niko escondido", "Director", "AlturaJugadorOculto", 20, 3, 80, 1, v => v.ToString("0") + " m",
                    "Niko flota invisible arriba del lugar de las acciones para que haya autos y gente")
            });
        }

        // ------------------------------------------------------------------
        MenuPage Connection()
        {
            return new MenuPage("Conexion con Kick", () => new List<MenuItem>
            {
                new MenuItem { Info = true, DynamicLabel = () => "Estado: " + host.KickStatus() },
                new MenuItem { Info = true, DynamicLabel = () => "Canal: " + Cfg.Channel + "   (chat " + Cfg.ChatroomId + ")" },
                new MenuItem
                {
                    Label = "Conectarse al chat",
                    Value = () => Ini.GetBool("Kick", "Conectar", true) ? "SI" : "NO",
                    OnChange = d =>
                    {
                        var doc = IniDoc.Load(Path.Combine(host.Folder, "config.ini"));
                        doc.Set("Kick", "Conectar", Ini.GetBool("Kick", "Conectar", true) ? "no" : "si");
                        doc.Save();
                        host.FullReload();
                    }
                },
                new MenuItem { Label = "Reconectar ahora", OnEnter = () => { host.FullReload(); Menu.Toast("Reconectando..."); } }
            });
        }

        // ------------------------------------------------------------------
        static readonly Keys[] KeyChoices =
        {
            Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12,
            Keys.Insert, Keys.Home, Keys.PageUp, Keys.Delete, Keys.End, Keys.PageDown, Keys.Pause, Keys.Scroll
        };

        MenuItem KeyItem(string label, string key, Keys def, string help)
        {
            return new MenuItem
            {
                Label = label,
                Value = () => Config.ParseKey(Ini.Get("Teclas", key), def).ToString(),
                OnChange = d =>
                {
                    Keys cur = Config.ParseKey(Ini.Get("Teclas", key), def);
                    int i = Array.IndexOf(KeyChoices, cur);
                    if (i < 0) i = 0;
                    int n = KeyChoices.Length;
                    Set("Teclas", key, KeyChoices[((i + Math.Sign(d)) % n + n) % n].ToString());
                },
                Help = () => help
            };
        }

        MenuPage KeysPage()
        {
            return new MenuPage("Teclas", () => new List<MenuItem>
            {
                KeyItem("Abrir este menu", "Menu", Keys.F8, "Liberty's Legacy usa F11 y TrafficKeeperLite F10: no elijas esas"),
                KeyItem("Prender / apagar el director", "Director", Keys.F9, ""),
                KeyItem("Camara libre (crear camaras)", "Editor", Keys.F7, ""),
                KeyItem("Ciudad / siguiente camara", "SiguienteCamara", Keys.F6, "Desde un NPC retorna al plano anterior; en ciudad avanza"),
                KeyItem("Recargar archivos", "Recargar", Keys.F5, ""),
                KeyItem("Seguir NPC (el proximo)", "SeguirNpc", Keys.F4, "La camara va con el proximo NPC de suscriptor"),
                KeyItem("Ranking de kills", "Ranking", Keys.F3, "Mostrar / ocultar"),
                KeyItem("Kill feed", "KillFeed", Keys.F2, "Mostrar / ocultar"),
                KeyItem("HUD compacto", "HUDStream", Keys.F1, "Mostrar / ocultar"),
                new MenuItem { Info = true, Label = "Menu: flechas, ENTER y RETROCESO (SHIFT = cambiar de a 10)" }
            });
        }
    }
}
