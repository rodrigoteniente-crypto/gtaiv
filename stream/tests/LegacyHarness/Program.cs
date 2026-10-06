using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using KickChaos;

/// <summary>Pruebas de la parte del mod que no depende del juego.</summary>
public static class Harness
{
    static int fails = 0, passes = 0;

    static void Check(bool cond, string what)
    {
        if (cond) { passes++; Console.WriteLine("  ok   " + what); }
        else { fails++; Console.WriteLine("  FAIL " + what); }
    }

    public static int Main(string[] args)
    {
        string distCfg = args[0];
        int port = int.Parse(args[1]);
        string tmp = Path.Combine(Path.GetTempPath(), "kc_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);

        // ---------- config.ini real del paquete ----------
        Console.WriteLine("[config.ini]");
        string text = File.ReadAllText(Path.Combine(distCfg, "config.ini"));
        File.WriteAllText(Path.Combine(tmp, "config.ini"), text.Replace("Conectar = si", "Conectar = si\nUrlWebsocket = ws://127.0.0.1:" + port + "/app/test"));
        File.Copy(Path.Combine(distCfg, "camaras.ini"), Path.Combine(tmp, "camaras.ini"));
        var warn = new List<string>();
        Config cfg = Config.Load(tmp, warn);
        foreach (var w in warn) Console.WriteLine("  aviso: " + w);
        Check(warn.Count == 0, "sin avisos al cargar config.ini");
        Check(cfg.ChatroomId == 31759082 && cfg.ChannelId == 32047403, "ids de rodsquare");
        Check(cfg.Triggers.Count == 22, "22 comandos (" + cfg.Triggers.Count + ")");
        Check(cfg.EventMap.Count == 14, "14 eventos mapeados (" + cfg.EventMap.Count + ")");
        Check(cfg.KeyDirector == System.Windows.Forms.Keys.F9 && cfg.KeyEditor == System.Windows.Forms.Keys.F7, "teclas F9/F7 con comentario al lado");
        Check(cfg.TestKeys.Count == 0, "sin teclas de NumPad por defecto");
        Check(cfg.KeyMenu == System.Windows.Forms.Keys.F8, "tecla del menu F8");
        Check(cfg.RepeatPerGift, "repetir por cada regalo");
        Check(cfg.ObsFile == "ultimo_evento.txt", "comentario inline removido de ArchivoOBS");
        Check(cfg.Ini.Get("Director", "HoraFija", "") == "", "HoraFija vacia");
        Check(cfg.GetCooldown("Bombardeo", 0) == 30f, "cooldown Bombardeo = 30");
        Check(Math.Abs(cfg.P("Explosion", "ProbabilidadCoche", 0f) - 0.7f) < 0.001f, "parametro de accion con comentario");
        Check(cfg.PS("LluviaDeCoches", "Modelos", "").Split(',').Length == 14, "lista de modelos");
        foreach (var t in cfg.Triggers)
            foreach (var st in t.Steps)
                Check(ActionNames.Canonical(st.Name) != null, "accion valida en comando '" + t.Keyword + "': " + st.Name);
        foreach (var kv in cfg.EventMap)
            foreach (var st in kv.Value)
                Check(ActionNames.Canonical(st.Name) != null, "accion valida en evento '" + kv.Key + "': " + st.Name);
        foreach (var kv in cfg.TestKeys)
            if (!kv.Value.StartsWith("evento:"))
                Check(ActionNames.Canonical(kv.Value) != null, "tecla de prueba " + kv.Key + " -> " + kv.Value);

        var apoc = cfg.Triggers.Find(t => t.Keyword == "apocalipsis");
        Check(apoc != null && apoc.Steps.Count == 2 && apoc.RequiredLevel == 1 && apoc.CooldownSeconds == 120f, "combo con rol y cooldown propio");
        var steps = Config.ParseSteps("Explosion*3 + Policia x2 + Explosion");
        Check(steps.Count == 3 && steps[0].Repeat == 3 && steps[1].Repeat == 2 && steps[2].Name == "Explosion" && steps[2].Repeat == 1, "ParseSteps con * y x");

        // ---------- coincidencias de texto ----------
        Console.WriteLine("[palabras clave]");
        var boom = new ChatTrigger { Keyword = "!boom", IsCommand = true };
        var expl = new ChatTrigger { Keyword = "explota", IsCommand = false };
        Check(TriggerEngine.Matches(boom, TriggerEngine.CleanMessage("!boom")), "!boom");
        Check(TriggerEngine.Matches(boom, TriggerEngine.CleanMessage("!BOOM jaja")), "!BOOM jaja");
        Check(!TriggerEngine.Matches(boom, TriggerEngine.CleanMessage("hola !boom")), "!boom tiene que ir al principio");
        Check(!TriggerEngine.Matches(boom, TriggerEngine.CleanMessage("!boomer")), "!boomer no es !boom");
        Check(TriggerEngine.Matches(expl, TriggerEngine.CleanMessage("que EXPLOTÁ todo!!")), "tildes y mayusculas");
        Check(TriggerEngine.Matches(expl, TriggerEngine.CleanMessage("explota!!!")), "signos pegados");
        Check(!TriggerEngine.Matches(expl, TriggerEngine.CleanMessage("explotaron")), "palabra completa");
        Check(TriggerEngine.Matches(expl, TriggerEngine.CleanMessage("[emote:1:KEKW]explota")), "emote pegado");

        // ---------- camaras.ini ida y vuelta ----------
        Console.WriteLine("[camaras.ini]");
        var cams = CameraStore.Load(tmp, 25f, 20f, warn);
        Check(cams.Count == 0, "el camaras.ini del paquete arranca vacio");
        var shot = new CameraShot { Name = CameraStore.NextName(cams), Pos = new Vector3(100.5f, -200.25f, 30f), Rot = new Vector3(-10f, 0f, 135f), Fov = 22f, Duration = 18f, HasTarget = true, Target = new Vector3(120f, -220f, 5f) };
        cams.Add(shot);
        var shot2 = new CameraShot { Name = CameraStore.NextName(cams), Pos = new Vector3(1, 2, 3), Rot = new Vector3(0, 0, 90), Fov = 30f, Duration = 25f, HasEnd = true, EndPos = new Vector3(5, 2, 3), EndRot = new Vector3(0, 0, 80), EndFov = 20f, Hour = 21, Minute = 30, Weather = 4 };
        cams.Add(shot2);
        CameraStore.Save(tmp, cams);
        var back = CameraStore.Load(tmp, 25f, 20f, warn);
        Check(back.Count == 2 && back[0].Name == "Camara 1" && back[1].Name == "Camara 2", "nombres Camara 1 / Camara 2");
        Check(Vector3.Distance(back[0].Pos, shot.Pos) < 0.01f && Math.Abs(back[0].Fov - 22f) < 0.01f && back[0].HasTarget, "posicion, FOV y objetivo");
        Check(back[1].HasEnd && back[1].EndFov == 20f && back[1].Hour == 21 && back[1].Minute == 30 && back[1].Weather == 4, "movimiento, hora y clima");
        Check(File.ReadAllText(Path.Combine(tmp, "camaras.ini")).Contains("Controles del editor"), "se conserva la cabecera con instrucciones");
        File.WriteAllText(Path.Combine(tmp, "camaras.ini"), "[Mano]\nPos = 0, 0, 10\nMirarA = 0, 10, 0\n");
        var mano = CameraStore.Load(tmp, 25f, 20f, warn);
        Check(mano.Count == 1 && Math.Abs(mano[0].Rot.Z) < 0.01f && mano[0].Rot.X < -40f, "MirarA calcula la rotacion (" + (mano.Count > 0 ? mano[0].Rot.ToString() : "") + ")");

        // ---------- matematica de camara ----------
        Console.WriteLine("[camara]");
        Vector3 f = MathX.Forward(new Vector3(0, 0, 0));
        Check(Math.Abs(f.Y - 1f) < 1e-4, "rumbo 0 = norte (+Y)");
        f = MathX.Forward(new Vector3(0, 0, 90));
        Check(Math.Abs(f.X + 1f) < 1e-4, "rumbo 90 = oeste (-X), convencion GTA");
        var from = new Vector3(10, 10, 20); var to = new Vector3(-30, 50, 0);
        Vector3 dir = Vector3.Normalize(to - from);
        Vector3 fw = MathX.Forward(MathX.LookRotation(from, to));
        Check(Vector3.Distance(dir, fw) < 1e-3, "LookRotation y Forward coinciden");
        Check(Math.Abs(MathX.LerpAngle(350f, 10f, 0.5f) - 360f) < 0.01f, "interpolacion de angulos por el camino corto");

        // ---------- conexion contra el Pusher falso ----------
        Console.WriteLine("[kick: websocket]");
        var logs = new List<string>();
        var client = new KickClient(cfg, s => { lock (logs) logs.Add(s); Console.WriteLine("    log: " + s); });
        var engine = new TriggerEngine(cfg);
        var queue = new ActionQueue();
        var enqueued = new List<string>();
        var ignored = new List<string>();
        client.Start();
        DateTime end = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < end)
        {
            KickEvent ev;
            while (client.Events.TryDequeue(out ev))
            {
                string why;
                var acts = engine.Process(ev, DateTime.UtcNow, out why);
                if (acts.Count == 0) ignored.Add(ev.Kind + ":" + ev.User + " (" + why + ")");
                foreach (var pa in acts)
                {
                    var unknown = new List<string>();
                    int n = queue.Enqueue(pa, cfg, ActionNames.Canonical, unknown);
                    enqueued.Add(pa.User + "=" + pa + "#" + n);
                }
            }
            Thread.Sleep(50);
        }
        client.Stop();
        Console.WriteLine("  encolado: " + string.Join(" | ", enqueued.ToArray()));
        Console.WriteLine("  ignorado: " + string.Join(" | ", ignored.ToArray()));

        Func<string, bool> has = s => enqueued.Exists(x => x.StartsWith(s));
        Check(enqueued.FindAll(x => x.StartsWith("juan=")).Count == 1, "juan !boom una sola vez (duplicado filtrado)");
        Check(has("juan=Explosion#1"), "juan -> Explosion");
        Check(!has("maria="), "maria bloqueada por cooldown de Explosion");
        Check(!has("pepe="), "pepe sin sub no puede !bomba");
        Check(has("subby=Bombardeo#1"), "subby (sub) -> Bombardeo");
        Check(has("luis=Policia#1"), "luis 'sirenas' con emote -> Policia");
        Check(has("Rodsquare=Explosion#1"), "el streamer no tiene cooldown");
        Check(!has("charla="), "mensaje normal no dispara nada");
        Check(enqueued.FindAll(x => x.StartsWith("nuevo_sub=")).Count == 1 && has("nuevo_sub=Npc#1"), "sub -> Npc (una vez)");
        Check(has("generoso=Npc+Explosion (x3)#4"), "3 subs regaladas -> 1 NPC + 3 explosiones");
        Check(has("fan=Fuego#1") && !has("ex_fan="), "follow -> Fuego, unfollow ignorado");
        Check(has("amigo=Persecucion+Bombardeo#2"), "host -> Persecucion+Bombardeo");
        Check(has("tipper=LluviaDeCoches#1"), "evento desconocido KicksGifted mapeado por nombre");
        Check(has("vuelta=LluviaDeCoches#1"), "reconecto solo despues del corte y siguio leyendo");
        lock (logs)
        {
            Check(logs.Exists(l => l.Contains("suscripto a chatrooms.31759082.v2")), "suscripcion a chatrooms.<id>.v2");
            Check(logs.Exists(l => l.Contains("suscripto a channel.32047403")), "suscripcion a channel.<id>");
            Check(logs.Exists(l => l.Contains("evento no reconocido 'KicksGifted'")), "log de evento desconocido");
        }

        // ---------- cola ----------
        Console.WriteLine("[cola]");
        var q = new ActionQueue();
        var small = Config.Load(tmp, new List<string>());
        small.MaxQueue = 3;
        int a1 = q.Enqueue(new PendingAction { Steps = Config.ParseSteps("Explosion"), User = "x" }, small, ActionNames.Canonical, null);
        int a2 = q.Enqueue(new PendingAction { Steps = Config.ParseSteps("Bombardeo+Fuego"), User = "y" }, small, ActionNames.Canonical, null);
        int a3 = q.Enqueue(new PendingAction { Steps = Config.ParseSteps("Panico"), User = "z" }, small, ActionNames.Canonical, null);
        Check(a1 == 1 && a2 == 2 && a3 == 0, "cola llena descarta pedidos del chat");
        int a4 = q.Enqueue(new PendingAction { Steps = Config.ParseSteps("Explosion*50"), User = "sub", FromEvent = true }, small, ActionNames.Canonical, null);
        Check(a4 == small.MaxRepeat && q.Count == a4, "evento entra igual, saca los del chat y respeta MaxRepeticiones");
        var unk = new List<string>();
        q.Enqueue(new PendingAction { Steps = Config.ParseSteps("Volar"), User = "x", FromEvent = true }, small, ActionNames.Canonical, unk);
        Check(unk.Count == 1 && unk[0] == "Volar", "accion desconocida reportada");
        var d1 = q.TryDequeue(100.0, 2.0); var d2 = q.TryDequeue(100.1, 2.0); var d3 = q.TryDequeue(100.5, 2.0);
        Check(d1 != null && d2 == null && d3 != null, "repeticiones del mismo pedido salen cada 0.45 s");


        // ---------- menu: editor de ini, Nada, Mensaje, Ban, regalos ----------
        Console.WriteLine("[menu / eventos nuevos]");
        string cfgPath = Path.Combine(tmp, "config.ini");
        string before = File.ReadAllText(cfgPath);
        var doc = IniDoc.Load(cfgPath);
        doc.Set("Director", "DuracionPorDefecto", "35");
        doc.Set("Eventos", "Mensaje", "Explosion");
        doc.Set("Comandos", "!nuevo", "Fuego+Panico | sub | 30");
        doc.RenameKey("Comandos", "!fuego", "!llamas");
        doc.Remove("Comandos", "!dia");
        doc.Set("SeccionNueva", "Clave", "1");
        doc.Save();
        string after = File.ReadAllText(cfgPath);
        var c2 = Config.Load(tmp, new List<string>());
        Check(c2.Ini.GetFloat("Director", "DuracionPorDefecto", 0) == 35f, "IniDoc cambia un valor");
        Check(after.Contains("DuracionPorDefecto  = 35") && after.Contains("; segundos por plano"), "IniDoc conserva el comentario de la linea");
        Check(after.Split('\n').Length >= before.Split('\n').Length, "IniDoc no pierde lineas");
        var nuevo = c2.Triggers.Find(t => t.RawKey == "!nuevo");
        Check(nuevo != null && nuevo.Steps.Count == 2 && nuevo.RequiredLevel == 1 && nuevo.CooldownSeconds == 30f, "comando agregado desde el menu");
        Check(c2.Triggers.Find(t => t.RawKey == "!llamas") != null && c2.Triggers.Find(t => t.RawKey == "!fuego") == null, "comando renombrado");
        Check(c2.Triggers.Find(t => t.RawKey == "!dia") == null, "comando borrado");
        Check(c2.Ini.Get("SeccionNueva", "Clave") == "1", "seccion nueva agregada");

        var eng2 = new TriggerEngine(c2);
        string why2;
        var r1 = eng2.Process(new KickEvent { Kind = KickEventKind.Chat, User = "x1", Text = "hola gente" }, DateTime.UtcNow, out why2);
        Check(r1.Count == 1 && r1[0].Source == "mensaje", "Cualquier mensaje -> Explosion");
        var r2 = eng2.Process(new KickEvent { Kind = KickEventKind.Ban, User = "malo", EventName = "UserBannedEvent" }, DateTime.UtcNow, out why2);
        Check(r2.Count == 0, "Ban = Nada no hace nada");
        var doc2 = IniDoc.Load(cfgPath); doc2.Set("Eventos", "Ban", "Explosion+Nada"); doc2.Set("Reglas", "RepetirPorCadaRegalo", "no"); doc2.Set("Comandos", "!boom", "Nada"); doc2.Save();
        var c3 = Config.Load(tmp, new List<string>());
        var eng3 = new TriggerEngine(c3);
        var r3 = eng3.Process(new KickEvent { Kind = KickEventKind.Ban, User = "malo", EventName = "UserBannedEvent" }, DateTime.UtcNow, out why2);
        Check(r3.Count == 1, "Ban -> Explosion");
        var q3 = new ActionQueue();
        Check(q3.Enqueue(r3[0], c3, ActionNames.Canonical, null) == 1, "'Nada' dentro de un combo se ignora");
        var r4 = eng3.Process(new KickEvent { Kind = KickEventKind.GiftedSubs, User = "g", Count = 5 }, DateTime.UtcNow, out why2);
        Check(r4.Count == 1 && r4[0].Multiplier == 1, "regalos sin repetir por cada sub");
        var r5 = eng3.Process(new KickEvent { Kind = KickEventKind.Chat, User = "y", Text = "!boom" }, DateTime.UtcNow, out why2);
        Check(r5.Count == 1 && r5[0].Source == "mensaje", "!boom puesto en Nada no dispara (cae en 'cualquier mensaje')");

        // ---------- NPC de suscriptores ----------
        Console.WriteLine("[npc de suscriptores]");
        Check(cfg.Ini.Get("Suscriptor", "ModoSub", "") == "Batalla" && cfg.Ini.Get("Suscriptor", "ModoFollow", "") == "Pasear" &&
              cfg.Ini.GetInt("Suscriptor", "Nivel3Desde", 0) == 6 && cfg.Ini.Get("Suscriptor", "Arma4", "") == "AK47", "seccion [Suscriptor] del paquete");
        Func<string, NpcBehavior> pick = src => NpcRules.ChooseMode(src, NpcBehavior.Batalla, NpcBehavior.Batalla, NpcBehavior.Pasear);
        Check(pick("Suscripcion") == NpcBehavior.Batalla && pick("RegaloSubs") == NpcBehavior.Batalla && pick("RegaloGrande") == NpcBehavior.Batalla, "sub y regalo: batalla");
        Check(pick("Follow") == NpcBehavior.Pasear && pick("tecla") == NpcBehavior.Pasear && pick("!npc") == NpcBehavior.Pasear && pick("Host") == NpcBehavior.Pasear, "follow (y lo demas): paseo");
        Check(NpcRules.ChooseMode("Suscripcion", NpcBehavior.Pasear, NpcBehavior.Batalla, NpcBehavior.Batalla) == NpcBehavior.Pasear, "el modo del sub se respeta");
        Check(NpcRules.Parse("tiroteo") == NpcBehavior.Batalla && NpcRules.Parse("Persecución") == NpcBehavior.Batalla && NpcRules.Parse("PINAS") == NpcBehavior.Batalla
              && NpcRules.Parse("arrasar") == NpcBehavior.Batalla && NpcRules.Parse("Robar") == NpcBehavior.Batalla && NpcRules.Parse("batalla") == NpcBehavior.Batalla
              && NpcRules.Parse("pasear") == NpcBehavior.Pasear && NpcRules.Parse("Paseo") == NpcBehavior.Pasear && NpcRules.Parse("Azar") == null, "nombres de modo (los viejos son batalla)");
        Check(NpcRules.Resolve("", NpcBehavior.Pasear) == NpcBehavior.Pasear && NpcRules.Resolve("Tiroteo", NpcBehavior.Pasear) == NpcBehavior.Batalla, "modo por defecto");
        Check(NpcRules.Subtitle("Suscripcion", 1) == "NUEVO SUB" && NpcRules.Subtitle("Suscripcion", 7) == "SUB 7 MESES" && NpcRules.Subtitle("RegaloSubs", 5) == "REGALO 5 SUBS"
              && NpcRules.Subtitle("RegaloSubs", 1) == "REGALO 1 SUB" && NpcRules.Subtitle("tecla", 1) == "", "segunda linea del cartel");
        for (int i = 0; i < NpcRules.BehaviorKeys.Length; i++)
            Check(NpcRules.Parse(NpcRules.BehaviorKeys[i]) == (NpcBehavior)i, "clave " + NpcRules.BehaviorKeys[i] + " ida y vuelta");

        float px, py, pd;
        var camP = new Vector3(0, 0, 10); var camR = new Vector3(0, 0, 0); // mirando al norte
        Check(NpcRules.Project(new Vector3(0, 50, 10), camP, camR, 30f, 16f / 9f, out px, out py, out pd) && Math.Abs(px - 0.5f) < 1e-3 && Math.Abs(py - 0.5f) < 1e-3 && Math.Abs(pd - 50f) < 1e-3, "proyeccion: centro de pantalla");
        Check(NpcRules.Project(new Vector3(5, 50, 10), camP, camR, 30f, 16f / 9f, out px, out py, out pd) && px > 0.5f && Math.Abs(py - 0.5f) < 1e-3, "proyeccion: este = derecha");
        Check(NpcRules.Project(new Vector3(0, 50, 15), camP, camR, 30f, 16f / 9f, out px, out py, out pd) && py < 0.5f, "proyeccion: arriba = y menor");
        float edgeZ = 10f + 50f * (float)Math.Tan(15.0 * Math.PI / 180.0);
        NpcRules.Project(new Vector3(0, 50, edgeZ), camP, camR, 30f, 16f / 9f, out px, out py, out pd);
        Check(Math.Abs(py) < 1e-3, "proyeccion: borde superior con FOV vertical (" + py + ")");
        Check(!NpcRules.Project(new Vector3(0, -50, 10), camP, camR, 30f, 16f / 9f, out px, out py, out pd), "proyeccion: atras de la camara no se dibuja");
        var camYaw = new Vector3(0, 0, 90); // mirando al oeste
        Check(NpcRules.Project(new Vector3(-40, 0, 10), new Vector3(0, 0, 10), camYaw, 30f, 16f / 9f, out px, out py, out pd) && Math.Abs(px - 0.5f) < 1e-3, "proyeccion: camara girada al oeste");
        Check(NpcRules.ApparentHeight(20f, 30f) > NpcRules.ApparentHeight(80f, 30f), "mas lejos se ve mas chico");

        var qn = new ActionQueue();
        var big = Config.Load(tmp, new List<string>());
        int nn = qn.Enqueue(new PendingAction { Steps = Config.ParseSteps("Npc+Explosion"), User = "gen", Source = "RegaloSubs", Count = 5, Multiplier = 5, FromEvent = true }, big, ActionNames.Canonical, null);
        var first = qn.TryDequeue(1000, 2);
        Check(nn == 6 && first != null && first.Name == "Npc" && first.Count == 5 && first.Source == "RegaloSubs", "regalo de 5: un NPC (con los meses/subs) + 5 explosiones");
        Check(ActionNames.Canonical("npc") == "Npc" && ActionNames.Canonical("personaje") == "Npc" && Array.IndexOf(ActionNames.Choices, "Npc") > 0, "accion Npc en el menu y con alias");
        var engN = new TriggerEngine(big);
        var rs = engN.Process(new KickEvent { Kind = KickEventKind.Subscription, User = "fiel", Count = 14, Simulated = true }, DateTime.UtcNow, out why2);
        Check(rs.Count == 1 && rs[0].Count == 14 && rs[0].Source == "Suscripcion", "los meses llegan hasta la accion");

        // ---------- eventos nuevos ----------
        Console.WriteLine("[eventos nuevos]");
        Check(NpcRules.FromAction("NpcBatalla") == NpcBehavior.Batalla && NpcRules.FromAction("NpcPasear") == NpcBehavior.Pasear && NpcRules.FromAction("Npc") == null, "acciones NPC con modo fijo");
        Check(ActionNames.Canonical("NpcRobar") == "NpcBatalla" && ActionNames.Canonical("NpcTiroteo") == "NpcBatalla" && ActionNames.Canonical("NpcArrasar") == "NpcBatalla" &&
              ActionNames.Canonical("NpcPasear") == "NpcPasear" && ActionNames.Canonical("autos voladores") == "AutosVoladores" && ActionNames.Canonical("lluvia") == "Lluvia", "acciones viejas de NPC ahora son batalla");
        foreach (string ch in ActionNames.Choices)
            Check(ActionNames.Canonical(ch) == ch && ActionNames.Display.ContainsKey(ch), "accion " + ch + " tiene nombre y alias");
        var qnpc = new ActionQueue();
        int nnpc = qnpc.Enqueue(new PendingAction { Steps = Config.ParseSteps("NpcTiroteo+Explosion"), User = "g", Source = "RegaloSubs", Multiplier = 4, FromEvent = true }, big, ActionNames.Canonical, null);
        Check(nnpc == 5, "NpcTiroteo no se repite por regalo (" + nnpc + ")");
        var dEv = IniDoc.Load(Path.Combine(tmp, "config.ini"));
        dEv.Set("Eventos", "PrimerMensaje", "Lluvia");
        dEv.Set("Eventos", "ChatAFull", "AutosVoladores");
        dEv.Set("Eventos", "RegaloGrande", "NpcArrasar+Bombardeo");
        dEv.Set("Reglas", "ChatAFullMensajes", "4");
        dEv.Set("Reglas", "ChatAFullSegundos", "10");
        dEv.Set("Cooldowns", "Lluvia", "0");
        dEv.Save();
        var cEv = Config.Load(tmp, new List<string>());
        var eEv = new TriggerEngine(cEv);
        DateTime t0 = DateTime.UtcNow;
        var p1 = eEv.Process(new KickEvent { Kind = KickEventKind.Chat, User = "nuevo1", Text = "hola" }, t0, out why2);
        var p2 = eEv.Process(new KickEvent { Kind = KickEventKind.Chat, User = "nuevo1", Text = "otra vez" }, t0.AddSeconds(1), out why2);
        Check(p1.Exists(x => x.Source == "PrimerMensaje") && !p2.Exists(x => x.Source == "PrimerMensaje"), "primer mensaje una sola vez por persona");
        var p3 = eEv.Process(new KickEvent { Kind = KickEventKind.Chat, User = "nuevo2", Text = "a" }, t0.AddSeconds(2), out why2);
        var p4 = eEv.Process(new KickEvent { Kind = KickEventKind.Chat, User = "nuevo3", Text = "b" }, t0.AddSeconds(3), out why2);
        var p5 = eEv.Process(new KickEvent { Kind = KickEventKind.Chat, User = "nuevo1", Text = "c" }, t0.AddSeconds(30), out why2);
        Check(p4.Exists(x => x.Source == "ChatAFull") && !p3.Exists(x => x.Source == "ChatAFull") && !p5.Exists(x => x.Source == "ChatAFull"), "chat a full con 4 mensajes en 10 s");
        var g10 = eEv.Process(new KickEvent { Kind = KickEventKind.GiftedSubs, User = "rico", Count = 10 }, t0, out why2);
        var g2 = eEv.Process(new KickEvent { Kind = KickEventKind.GiftedSubs, User = "medio", Count = 2 }, t0, out why2);
        Check(g10.Count == 1 && g10[0].Source == "RegaloGrande" && g10[0].Multiplier == 1 && g10[0].Count == 10, "regalo grande reemplaza a RegaloSubs");
        Check(g2.Count == 1 && g2[0].Source == "RegaloSubs", "regalo chico sigue siendo RegaloSubs");
        var giftEv = new KickEvent { Kind = KickEventKind.GiftedSubs, User = "regalador", Count = 3 };
        giftEv.Recipients.Add("ana"); giftEv.Recipients.Add("beto"); giftEv.Recipients.Add("caro");
        var gDef = new TriggerEngine(cEv).Process(giftEv, t0.AddSeconds(100), out why2);
        Check(gDef.Count == 1 && gDef[0].User == "regalador", "regalo: por defecto el NPC es del que regala");
        cEv.GiftNpcForRecipients = true;
        var gRec = new TriggerEngine(cEv).Process(giftEv, t0.AddSeconds(200), out why2);
        Check(gRec.Exists(x => x.User == "ana" && x.Source == "Suscripcion" && x.Steps.Exists(st => st.Name == "Npc")) && gRec.Exists(x => x.User == "caro") &&
              !gRec.Exists(x => x.User == "regalador" && x.Steps.Exists(st => st.Name == "Npc")), "regalo con la opcion: un NPC por cada uno que recibe (" + gRec.Count + ")");
        cEv.GiftNpcForRecipients = false;
        Check(NpcRules.ChooseMode("RegaloGrande", NpcBehavior.Pasear, NpcBehavior.Batalla, NpcBehavior.Pasear) == NpcBehavior.Batalla && NpcRules.Subtitle("RegaloGrande", 10) == "REGALO 10 SUBS", "regalo grande usa el modo del regalo");
        string fl = "Nueva suscripcion / re-sub", fv = "NPC con su nombre + Bombardeo + Coches bomba";
        MenuText.FitRow(ref fl, ref fv, 0.294f);
        Check(fl.Length + fv.Length + 3 <= (int)(0.294f / (0.026f * 0.5f / (16f / 9f))), "el menu acorta el texto para que no se pise (" + fl + " | " + fv + ")");

        // ---------- bandas y control por chat ----------
        Console.WriteLine("[bandas]");
        int[] slots = { GangRules.CmdRobarAuto, GangRules.CmdAtacar, GangRules.CmdHuir };
        Check(GangRules.ParseChoice("!robarauto", slots) == 0, "!robarauto -> opcion 1");
        Check(GangRules.ParseChoice("  !ROBAR AUTO  ", slots) == 0, "!ROBAR AUTO -> opcion 1");
        Check(GangRules.ParseChoice("!robá un auto", slots) == 0, "!robá un auto -> opcion 1");
        Check(GangRules.ParseChoice("!huir ya!!", slots) == 2, "!huir ya!! -> opcion 3");
        Check(GangRules.ParseChoice("! atacar", slots) == 1, "! atacar -> opcion 2");
        Check(GangRules.ParseChoice("!2", slots) == 1 && GangRules.ParseChoice("!3 dale", slots) == 2 && GangRules.ParseChoice("!4", slots) == -1, "!2 / !3 por numero");
        Check(GangRules.ParseChoice("huir", slots) == -1, "sin ! no es una orden");
        Check(GangRules.ParseChoice("!comer", slots) == -1, "orden que no esta entre las 3 -> nada");
        int[] dance = { GangRules.CmdAtacar, GangRules.CmdHuir, GangRules.CmdBailar };
        Check(GangRules.ParseChoice("!bailar", dance) == 2 && GangRules.ParseChoice("!BAILEN", dance) == 2 && GangRules.ParseChoice("!perreo", dance) == 2 &&
              GangRules.ParseChoice("!comer", dance) == -1 && GangRules.ParseKey("comer") == -1 && GangRules.ParseKey("bailar") == GangRules.CmdBailar, "!bailar (ya no hay !comer)");
        int[] defSlots = { GangRules.ParseKey(GangRules.DefaultSlots[0]), GangRules.ParseKey(GangRules.DefaultSlots[1]), GangRules.ParseKey(GangRules.DefaultSlots[2]) };
        Check(GangRules.ChoicesLine(defSlots) == "!atacar   !huir   !bailar", "ordenes por defecto");
        Check(GangRules.ModeSwitch("!pasear") == 0 && GangRules.ModeSwitch("!Paseo") == 0 && GangRules.ModeSwitch("!batalla ya") == 1 && GangRules.ModeSwitch("pasear") == -1 &&
              GangRules.ModeSwitch("!atacar") == -1 && GangRules.ModeSwitch("!pelear") == -1, "!pasear / !batalla");
        // ---- 1.9 ----
        Check(GangRules.FunCommand("!skin") == GangRules.FunSkin && GangRules.FunCommand("!Saltá") == GangRules.FunSaltar &&
              GangRules.FunCommand("!manos arriba") == GangRules.FunManos && GangRules.FunCommand("!turbo ya") == GangRules.FunTurbo &&
              GangRules.FunCommand("skin") == -1 && GangRules.FunCommand("!atacar") == -1 && GangRules.FunCommand("!bailar") == -1, "comandos divertidos");
        Check(GangRules.FunKeys.Length == GangRules.FunLabels.Length, "divertidos: etiquetas");
        for (int i = 0; i < GangRules.FunKeys.Length; i++) Check(GangRules.FunCommand("!" + GangRules.FunKeys[i]) == i, "divertido !" + GangRules.FunKeys[i]);
        Check(PerkRules.Labels.Length == PerkRules.Count && PerkRules.Help.Length == PerkRules.Count && PerkRules.Keys.Length == PerkRules.Count, "mejoras: 17 con nombre y ayuda");
        Check(PerkRules.ParsePick("!vampiro", new[] { PerkRules.Chaleco, PerkRules.Vampiro, PerkRules.Rpg }) == 1, "mejora por nombre (vampiro)");
        Check(NpcRules.ParseColor("#FFAA00") == unchecked((int)0xFFFFAA00) && NpcRules.ParseColor("00ff00") == unchecked((int)0xFF00FF00) &&
              NpcRules.ParseColor("") == 0 && NpcRules.ParseColor("#zzz") == 0 && NpcRules.ParseColor("#12345") == 0, "color de Kick");
        { int dark = NpcRules.ParseColor("#000000"); Check(dark != 0 && ((dark >> 16) & 255) > 80, "color de Kick muy oscuro se aclara"); }
        Check(Math.Abs(MathX.SegmentDistance(new Vector3(5, 3, 0), new Vector3(0, 0, 0), new Vector3(10, 0, 0)) - 3f) < 0.01f &&
              Math.Abs(MathX.SegmentDistance(new Vector3(-4, 3, 0), new Vector3(0, 0, 0), new Vector3(10, 0, 0)) - 5f) < 0.01f, "distancia a un segmento");
        Check(GangLevels.ClipSize(7) == 17 && GangLevels.ClipSize(15) == 30 && GangLevels.ClipSize(0) == 0 && GangLevels.ClipSize(18) == 0, "cargadores");
        Check(GangRules.ParseChoice("!piñas", new[] { GangRules.CmdPinas }) == 0, "!piñas con ñ");
        Check(GangRules.ParseChoice("!12", slots) == -1 && GangRules.ParseChoice("!", slots) == -1 && GangRules.ParseChoice("", slots) == -1, "mensajes raros");
        Check(GangRules.ParseKey("RobarAuto") == 0 && GangRules.ParseKey("Banda") == 5 && GangRules.ParseKey("cualquiera") == -1, "nombres de config de las ordenes");
        Check(GangRules.ChoicesLine(slots) == "!robarauto   !atacar   !huir", "linea de opciones");
        int[] dupes = { GangRules.CmdHuir, GangRules.CmdHuir, GangRules.CmdAtacar };
        GangRules.Dedupe(dupes);
        Check(dupes[0] == GangRules.CmdHuir && dupes[1] == GangRules.CmdRobarAuto && dupes[2] == GangRules.CmdAtacar, "opciones repetidas se corrigen (" + GangRules.ChoicesLine(dupes) + ")");
        Check(GangRules.ParseMode("Votacion") == GangRules.ModeVote && GangRules.ParseMode("Chat") == GangRules.ModeFirst && GangRules.ParseMode("Suscriptor") == GangRules.ModeSub && GangRules.ParseMode("") == GangRules.ModeSub, "modo de control");
        Check(GangRules.ParseUnlock("Siempre") == GangRules.UnlockAlways && GangRules.ParseUnlock("Nunca") == GangRules.UnlockNever && GangRules.ParseUnlock("AlDuplicarse") == GangRules.UnlockOnGrow, "cuando se desbloquea el control");
        Check(GangRules.Grows(GangRules.GrowCops, true, false) && !GangRules.Grows(GangRules.GrowCops, false, true) && GangRules.Grows(GangRules.GrowCopsAndGangs, false, true) && !GangRules.Grows(GangRules.GrowCopsAndGangs, false, false) && GangRules.Grows(GangRules.GrowAnyone, false, false), "que muerte hace crecer la banda");
        Check(GangRules.ParseGrow("PoliciasYBandas") == GangRules.GrowCopsAndGangs && GangRules.ParseGrow("Policias") == GangRules.GrowCops && GangRules.ParseGrow("Cualquiera") == GangRules.GrowAnyone, "config de crecer");
        var box = new GangRules.VoteBox(3);
        Check(!box.Open && box.Winner() == -1, "votacion vacia");
        box.Add("ana", 1, 100, 15);
        box.Add("beto", 2, 101, 15);
        box.Add("caro", 2, 102, 15);
        box.Add("ANA", 2, 103, 15);   // cambia el voto (mismo usuario)
        box.Add("dani", 1, 104, 15);
        Check(box.Open && box.End == 115 && box.Count(2) == 3 && box.Count(1) == 1 && box.Total == 4, "un voto por persona, vale el ultimo");
        Check(box.Winner() == 2 && !box.Due(114.9) && box.Due(115), "gana el mas votado al terminar el tiempo");
        Check(GangRules.VoteLine(slots, box, 109.2) == "!robarauto 0   !atacar 1   !huir 3   (6s)", "linea de votos (" + GangRules.VoteLine(slots, box, 109.2) + ")");
        var tie = new GangRules.VoteBox(3);
        tie.Add("a", 0, 10, 5); tie.Add("b", 1, 11, 5);
        Check(tie.Winner() == 0, "empate: gana el que llego primero");
        box.Reset();
        Check(!box.Open && box.Total == 0 && box.Winner() == -1, "votacion reiniciada");
        Check(GangRules.Clock(185) == "3:05" && GangRules.Clock(59.2) == "1:00" && GangRules.Clock(-3) == "0:00" && GangRules.Clock(900) == "15:00", "reloj m:ss");
        string[] bub = GangRules.Bubble("hola [emote:37226:KEKW] chat 😂 como andan todos hoy en el stream de rodsquare jajaja", 20, 2);
        Check(bub.Length == 2 && bub[0] == "hola KEKW chat como" && bub[1].EndsWith("..") && bub[1].Length <= 20, "globito: emotes, emojis y cortado (" + string.Join("|", bub) + ")");
        string[] bub2 = GangRules.Bubble(new string('a', 70), 20, 3);
        Check(bub2.Length == 3 && bub2[0].Length == 20 && bub2[2].EndsWith("..") && bub2[2].Length == 20, "globito: palabra larguisima (" + string.Join("|", bub2) + ")");
        Check(GangRules.Bubble("   ", 20, 2).Length == 0 && GangRules.Bubble("hola", 20, 2).Length == 1, "globito vacio / corto");
        Check(GangRules.BubbleSeconds("hola") == 5.0 && GangRules.BubbleSeconds(new string('a', 300)) == 12.0, "tiempo del globito");
        var cov = GangRules.CoverBehind(new Vector3(10, 0, 5), new Vector3(30, 0, 5), 2.3f);
        Check(Math.Abs(cov.X - 7.7f) < 0.01f && Math.Abs(cov.Y) < 0.01f && cov.Z == 5f, "cobertura del lado contrario a la policia");
        Vector3 me = new Vector3(0, 0, 0), cop = new Vector3(40, 0, 0);
        float sNear = GangRules.CoverScore(me, new Vector3(8, 3, 0), cop, 16f, true);
        float sBack = GangRules.CoverScore(me, new Vector3(-12, 0, 0), cop, 16f, true);
        float sOnTop = GangRules.CoverScore(me, new Vector3(37, 0, 0), cop, 60f, true);
        float sFar = GangRules.CoverScore(me, new Vector3(30, 20, 0), cop, 16f, true);
        Check(sNear < float.MaxValue && sBack == float.MaxValue && sOnTop == float.MaxValue && sFar == float.MaxValue, "autos que sirven para cubrirse");
        float sAdv = GangRules.CoverScore(me, new Vector3(12, 0, 0), cop, 16f, true);
        float sSide = GangRules.CoverScore(me, new Vector3(0, 9, 0), cop, 16f, true);
        Check(sAdv < sSide, "avanzar de auto en auto hacia la policia");
        var stf = GangRules.Strafe(me, cop, 5f, 2f);
        Check(Math.Abs(stf.X - 2f) < 0.01f && Math.Abs(Math.Abs(stf.Y) - 5f) < 0.01f, "correrse al costado");
        Check(GangRules.HotspotWeight(50) > GangRules.HotspotWeight(200) && GangRules.HotspotWeight(200) > GangRules.HotspotWeight(1000), "las camaras cerca del lio pesan mas");
        Check(GangRules.ShotDistance(new Vector3(100, 0, 0), new Vector3(0, 0, 0), new Vector3(90, 0, 0)) == 10f, "distancia al objetivo de la camara");
        Check(GangRules.GangTitle("rodsquare", 3) == "LA BANDA DE RODSQUARE (3)" && GangRules.MemberName("ana") == "banda de ana", "nombres de la banda");

        // ---------- 1.7: ranking de kills, niveles y armas por meses ----------
        var rk = new KillRanking();
        rk.Kill("ana", KillRanking.Cop); rk.Kill("ana", KillRanking.Npc); rk.Kill("ana", KillRanking.Civilian);
        rk.Kill("bob", KillRanking.Npc); rk.Kill("bob", KillRanking.Npc); rk.Kill("bob", KillRanking.Cop);
        rk.Kill("Cris", KillRanking.Cop); rk.Death("cris"); rk.Death("dani");
        rk.Level("bob", 3); rk.Level("bob", 2);
        var top = rk.Top(10);
        Check(top.Count == 4 && top[0].Name == "bob" && top[1].Name == "ana" && top[2].Name == "Cris" && top[3].Name == "dani",
            "ranking: mas kills, despues mas NPC (" + string.Join(",", top.ConvertAll(x => x.Name).ToArray()) + ")");
        Check(rk.Get("CRIS").Deaths == 1 && rk.Get("bob").Level == 3 && rk.Dirty, "ranking: sin mayusculas, el mejor nivel, sucio");
        Check(rk.Top(2).Count == 2, "ranking: top N");
        var rk2 = KillRanking.Parse(rk.Serialize());
        Check(rk2.Count == 4 && rk2.Get("bob").Kills == 3 && rk2.Get("bob").Npcs == 2 && rk2.Get("bob").Cops == 1 && rk2.Get("bob").Level == 3 &&
              rk2.Get("ana").Civilians == 1 && rk2.Get("Cris").Deaths == 1 && !rk2.Dirty, "ranking.ini ida y vuelta");
        Check(KillRanking.Parse("; x\n[Ranking]\nraro = 5,a,,9\nbasura\n=3\n").Get("raro").Kills == 5 &&
              KillRanking.Parse("[Ranking]\nraro = 5,a,,9\n").Get("raro").Civilians == 9, "ranking.ini con basura");
        Check(rk.ObsText(2) == "1. bob - 3 kills\r\n2. ana - 3 kills\r\n", "ranking.txt para OBS (" + rk.ObsText(2).Replace("\r\n", "|") + ")");
        Check(KillRanking.Detail(rk.Get("bob")) == "1 poli, 2 npc, nv 3" && KillRanking.Detail(rk.Get("dani")) == "", "detalle del ranking");
        rk.Kill("e=v[i]l;", KillRanking.Npc);
        Check(KillRanking.Parse(rk.Serialize()).Get("evil").Kills == 1, "nombres raros no rompen ranking.ini");
        rk.Clear();
        Check(rk.Top(10).Count == 0 && rk.Dirty, "borrar ranking");
        Check(GangLevels.FromNpcKills(0) == 1 && GangLevels.FromNpcKills(1) == 2 && GangLevels.FromNpcKills(50) == 10, "nivel de banda por kills de NPC");
        int[] tf = GangLevels.DefaultTierFrom;
        Check(GangLevels.TierFor(1, tf) == 0 && GangLevels.TierFor(0, tf) == 0 && GangLevels.TierFor(2, tf) == 1 && GangLevels.TierFor(5, tf) == 1 &&
              GangLevels.TierFor(6, tf) == 2 && GangLevels.TierFor(12, tf) == 3 && GangLevels.TierFor(23, tf) == 3 && GangLevels.TierFor(24, tf) == 4 && GangLevels.TierFor(99, tf) == 4, "rango de meses");
        Check(GangLevels.ParseWeapon("AK47") == 14 && GangLevels.ParseWeapon("ak") == 14 && GangLevels.ParseWeapon("M4") == 15 && GangLevels.ParseWeapon("rpg") == 18 &&
              GangLevels.ParseWeapon("Granadas") == 4 && GangLevels.ParseWeapon("molotov") == 5 && GangLevels.ParseWeapon("EscopetaCombate") == 11 &&
              GangLevels.ParseWeapon("escopeta") == 10 && GangLevels.ParseWeapon("Micro Uzi") == 12 && GangLevels.ParseWeapon("AMano") == 0 && GangLevels.ParseWeapon("cualquiera") == -1, "nombres de armas en config");
        bool allW = true;
        for (int i = 0; i < GangLevels.WeaponKeys.Length; i++)
            if (GangLevels.ParseWeapon(GangLevels.WeaponKeys[i]) != GangLevels.WeaponIds[i] || GangLevels.WeaponIndex(GangLevels.WeaponIds[i]) != i) allW = false;
        foreach (string dw in GangLevels.DefaultTierWeapons) if (GangLevels.ParseWeapon(dw) < 0) allW = false;
        foreach (string dw in GangLevels.DefaultLevelWeapons) if (GangLevels.ParseWeapon(dw) < 0) allW = false;
        Check(allW && GangLevels.DefaultLevelWeapons.Length == 9 && GangLevels.DefaultTierWeapons.Length == GangLevels.Tiers, "lista de armas ida y vuelta");
        Check(GangLevels.IsSpecial(4) && GangLevels.IsSpecial(5) && GangLevels.IsSpecial(18) && !GangLevels.IsSpecial(14) && GangLevels.Rank(18) == 0, "granadas / molotov / RPG son especiales");
        Check(GangLevels.Upgrade(7) == 9 && GangLevels.Upgrade(13) == 11 && GangLevels.Upgrade(14) == 15 && GangLevels.Upgrade(15) == 15 && GangLevels.Upgrade(0) == 7, "mejor arma");
        Check(GangLevels.AmmoFor(18) == 3 && GangLevels.AmmoFor(4) == 4 && GangLevels.AmmoFor(14) == 600, "balas");
        // mejoras roguelike
        var prng = new Random(3);
        bool offerOk = true;
        for (int i = 0; i < 50; i++)
        {
            int[] of = PerkRules.Offer(prng, 3, new HashSet<int> { PerkRules.Refuerzo });
            if (of.Length != 3 || of[0] == of[1] || of[1] == of[2] || of[0] == of[2] || Array.IndexOf(of, PerkRules.Refuerzo) >= 0) offerOk = false;
        }
        Check(offerOk && PerkRules.Labels.Length == PerkRules.Count && PerkRules.Help.Length == PerkRules.Count, "3 mejoras distintas (sin las que no sirven)");
        var allSkip = new HashSet<int>(); for (int i = 0; i < PerkRules.Count - 1; i++) allSkip.Add(i);
        Check(PerkRules.Offer(prng, 3, allSkip).Length == 1, "si quedan pocas, ofrece las que hay");
        int[] offer = { PerkRules.Chaleco, PerkRules.Rpg, PerkRules.Velocidad };
        Check(PerkRules.Line(offer) == "!1 CHALECO   !2 RPG   !3 VELOCIDAD", "linea de mejoras (" + PerkRules.Line(offer) + ")");
        Check(PerkRules.ParsePick("!1", offer) == 0 && PerkRules.ParsePick("!3 dale", offer) == 2 && PerkRules.ParsePick("!rpg", offer) == 1 &&
              PerkRules.ParsePick("!velocidad", offer) == 2 && PerkRules.ParsePick("!chal", offer) == 0 && PerkRules.ParsePick("!4", offer) == -1 &&
              PerkRules.ParsePick("1", offer) == -1 && PerkRules.ParsePick("!atacar", offer) == -1 && PerkRules.ParsePick("!a", offer) == -1, "elegir mejora por numero o nombre");
        var pv = new GangRules.VoteBox(3);
        pv.Add("x", 1, 0, 20); pv.Add("y", 1, 1, 20); pv.Add("z", 0, 2, 20);
        Check(PerkRules.TagLine(offer, pv, 11.2, true) == "MEJORA (12): !1 CHALECO (1)   !2 RPG (2)   !3 VELOCIDAD", "cartel de la mejora con votos (" + PerkRules.TagLine(offer, pv, 11.2, true) + ")");
        Check(PerkRules.TagLine(offer, pv, 5, false) == "MEJORA (5): !1 CHALECO   !2 RPG   !3 VELOCIDAD", "cartel de la mejora sin votos");
        // camara: distancia para que entren todos
        float fd1 = MathX.FitDistance(10f, 40f, 16f / 9f), fd2 = MathX.FitDistance(30f, 40f, 16f / 9f), fd3 = MathX.FitDistance(10f, 60f, 16f / 9f);
        Check(fd2 > fd1 && fd3 < fd1 && fd1 > 10f, "la camara del tiroteo se aleja si estan mas separados (" + fd1.ToString("0.0") + " / " + fd2.ToString("0.0") + ")");
        Check(Math.Abs(MathX.AzOf(MathX.Forward(new Vector3(0, 0, 37f))) - 37f) < 0.01f && Math.Abs(MathX.AngleDiff(350f, 10f) - 20f) < 0.01f, "angulos");
        Check(GangLevels.Rank(15) > GangLevels.Rank(14) && GangLevels.Rank(14) > GangLevels.Rank(11) && GangLevels.Rank(11) > GangLevels.Rank(7) && GangLevels.Rank(1) == 0, "que arma es mejor");
        Check(GangLevels.MonthsFor("Suscripcion", 7) == 7 && GangLevels.MonthsFor("RegaloSubs", 5) == 15 && GangLevels.MonthsFor("RegaloSubs", 50) == 36 &&
              GangLevels.MonthsFor("comando", 9) == 1 && GangLevels.MonthsFor("suscripción", 3) == 3, "meses equivalentes (regalos x3)");
        Check(GangLevels.WeaponName(14) == "AK47" && GangLevels.WeaponName(0) == "A mano" && GangLevels.WeaponName(18) == "RPG", "nombres de armas");

        // ---------- importar Liberty's Legacy ----------
        string llDir = Path.Combine(Path.Combine(tmp, "Liberty's Legacy"), "Cameras");
        Directory.CreateDirectory(llDir);
        File.WriteAllText(Path.Combine(llDir, "live.ini"), "[camera_data]\nX=-34.554260\nY=345.992096\nZ=14.254812\nrotX=1.925925\nrotY=0.000000\nrotZ=19.981987\nFOV=20.000000\nWeather=0\nHour=22\nMinute=0\n");
        var list = new List<CameraShot>();
        int imp1 = CameraStore.ImportLibertysLegacy(tmp, list, null);
        int imp2 = CameraStore.ImportLibertysLegacy(tmp, list, null);
        Check(imp1 == 1 && imp2 == 0 && list[0].Name == "live" && list[0].Fov == 20f && list[0].Hour == 22 && list[0].Weather == 0 && Math.Abs(list[0].Rot.Z - 19.982f) < 0.01f, "importa camaras de Liberty's Legacy sin repetir");
        list[0].Active = false; list[0].Transition = 1; list[0].Duration = 45;
        CameraStore.Save(tmp, list);
        var back2 = CameraStore.Load(tmp, 25f, 20f, new List<string>());
        Check(back2.Count == 1 && !back2[0].Active && back2[0].Transition == 1 && back2[0].Duration == 45f, "activa / transicion / duracion por camara");

        Console.WriteLine();
        Console.WriteLine("RESULTADO: " + passes + " ok, " + fails + " fallas");
        try { Directory.Delete(tmp, true); } catch { }
        return fails == 0 ? 0 : 1;
    }
}
