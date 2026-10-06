using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace KickChaos;

public class KickMenu
{
	private readonly KickChaosScript host;

	public readonly Menu Menu;

	private double confirmUntil;

	private object confirmTarget;

	private static readonly string[] WeatherLabels = new string[9] { "Normal", "Extra soleado", "Soleado", "Ventoso", "Nublado", "Lluvia", "Llovizna", "Niebla", "Tormenta" };

	private static readonly float[] WeightSteps = new float[11]
	{
		0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f, 4f, 5f, 7f,
		10f
	};

	private static readonly string[,] EventDefs = new string[14, 2]
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

	private static readonly string[] RoleWords = new string[5] { "todos", "sub", "vip", "mod", "streamer" };

	private static readonly string[] RoleLabels = new string[5] { "Todos", "Subs", "VIP", "Mods", "Solo yo" };

	private const string SU = "Suscriptor";

	private static readonly Keys[] KeyChoices;

	private Config Cfg => host.Cfg;

	private IniFile Ini => host.Cfg.Ini;

	private Director Dir => host.Dir;

	public KickMenu(KickChaosScript host)
	{
		this.host = host;
		Menu = new Menu(Root);
	}

	private void Set(string section, string key, string value)
	{
		try
		{
			IniDoc iniDoc = IniDoc.Load(Path.Combine(host.Folder, "config.ini"));
			iniDoc.Set(section, key, value);
			iniDoc.Save();
			host.SoftReload();
		}
		catch (Exception ex)
		{
			Menu.Toast("No se pudo guardar: " + ex.Message);
		}
	}

	private static string Sec(float v)
	{
		return v.ToString((!(v < 10f) || !(Math.Abs((double)v - Math.Round(v)) > 0.01)) ? "0" : "0.0#", CultureInfo.InvariantCulture) + " s";
	}

	private static float Clamp(float v, float a, float b)
	{
		return (v < a) ? a : ((!(v > b)) ? v : b);
	}

	private static float Snap(float v, float step)
	{
		return (float)Math.Round(Math.Round(v / step) * (double)step, 3);
	}

	private MenuItem Num(string label, string section, string key, float def, float min, float max, float step, Func<float, string> fmt, string help)
	{
		MenuItem menuItem = new MenuItem();
		menuItem.Label = label;
		menuItem.Value = () => fmt(Ini.GetFloat(section, key, def));
		menuItem.OnChange = delegate(int d)
		{
			Set(section, key, IniFile.F(Clamp(Snap(Ini.GetFloat(section, key, def) + (float)d * step, step), min, max)));
		};
		menuItem.Help = () => help;
		return menuItem;
	}

	private MenuItem Bool(string label, string section, string key, bool def, string help)
	{
		MenuItem menuItem = new MenuItem();
		menuItem.Label = label;
		menuItem.Value = () => (!Ini.GetBool(section, key, def)) ? "NO" : "SI";
		menuItem.OnChange = delegate
		{
			Set(section, key, (!Ini.GetBool(section, key, def)) ? "si" : "no");
		};
		menuItem.Help = () => help;
		return menuItem;
	}

	private MenuItem Choice(string label, string section, string key, string[] values, string[] labels, string help)
	{
		return Choice(label, section, key, values, labels, values[0], help);
	}

	private MenuItem Choice(string label, string section, string key, string[] values, string[] labels, string def, string help)
	{
		Func<int> cur = delegate
		{
			string text = Config.Normalize(Ini.Get(section, key, def));
			for (int i = 0; i < values.Length; i++)
			{
				if (text == Config.Normalize(values[i]))
				{
					return i;
				}
			}
			for (int j = 0; j < values.Length; j++)
			{
				if (text.StartsWith(Config.Normalize(values[j])))
				{
					return j;
				}
			}
			return 0;
		};
		MenuItem menuItem = new MenuItem();
		menuItem.Label = label;
		menuItem.Value = () => labels[cur()];
		menuItem.OnChange = delegate(int d)
		{
			Set(section, key, values[((cur() + Math.Sign(d)) % values.Length + values.Length) % values.Length]);
		};
		menuItem.Help = () => help;
		return menuItem;
	}

	private static string TimeLabel(int slot)
	{
		return (slot >= 0) ? ((slot / 2).ToString("00") + ":" + ((slot % 2 != 0) ? "30" : "00")) : "Normal";
	}

	private static int TimeSlot(int h, int m)
	{
		return (h >= 0) ? (h * 2 + ((m >= 30) ? 1 : 0)) : (-1);
	}

	private static int NextSlot(int slot, int d)
	{
		int num = slot + Math.Sign(d);
		if (num < -1)
		{
			num = 47;
		}
		if (num > 47)
		{
			num = -1;
		}
		return num;
	}

	private static int NextWeather(int w, int d)
	{
		int num = w + Math.Sign(d);
		if (num < -1)
		{
			num = 7;
		}
		if (num > 7)
		{
			num = -1;
		}
		return num;
	}

	private bool Confirm(object target, string what)
	{
		if (confirmTarget == target && G.Now < confirmUntil)
		{
			confirmTarget = null;
			return true;
		}
		confirmTarget = target;
		confirmUntil = G.Now + 3.0;
		Menu.Toast("Apreta ENTER otra vez para " + what);
		return false;
	}

	private static string Steps(List<ActionStep> steps)
	{
		if (steps == null || Config.IsNothing(steps))
		{
			return "Nada";
		}
		List<string> list = new List<string>();
		foreach (ActionStep step in steps)
		{
			string text = ActionNames.Canonical(step.Name) ?? step.Name;
			if (!(text == "Nada"))
			{
				list.Add(ActionNames.Pretty(text) + ((step.Repeat <= 1) ? string.Empty : (" x" + step.Repeat)));
			}
		}
		return string.Join(" + ", list.ToArray());
	}

	private static string[] Slots(List<ActionStep> steps)
	{
		List<string> list = new List<string>();
		if (steps != null)
		{
			foreach (ActionStep step in steps)
			{
				string text = ActionNames.Canonical(step.Name) ?? step.Name;
				if (!(text == "Nada"))
				{
					list.Add((step.Repeat <= 1) ? text : (text + "*" + step.Repeat));
				}
			}
		}
		while (list.Count < 3)
		{
			list.Add("Nada");
		}
		return list.ToArray();
	}

	private static string SlotName(string slot)
	{
		int num = slot.IndexOf('*');
		string canonical = ((num <= 0) ? slot : slot.Substring(0, num));
		return ActionNames.Pretty(canonical) + ((num <= 0) ? string.Empty : (" x" + slot.Substring(num + 1)));
	}

	private static string Join(string[] slots)
	{
		List<string> list = new List<string>();
		foreach (string text in slots)
		{
			if (text != "Nada")
			{
				list.Add(text);
			}
		}
		return (list.Count != 0) ? string.Join("+", list.ToArray()) : "Nada";
	}

	private static string CycleAction(string current, int d)
	{
		int num = current.IndexOf('*');
		if (num > 0)
		{
			current = current.Substring(0, num);
		}
		int num2 = Array.IndexOf(ActionNames.Choices, current);
		if (num2 < 0)
		{
			num2 = 0;
		}
		int num3 = ActionNames.Choices.Length;
		return ActionNames.Choices[((num2 + Math.Sign(d)) % num3 + num3) % num3];
	}

	private MenuPage Root()
	{
		return new MenuPage("Menu principal", () => new List<MenuItem>
		{
			new MenuItem
			{
				Info = true,
				DynamicLabel = () => "Kick: " + host.KickStatus()
			},
			new MenuItem
			{
				Label = "Fondo de camaras (director)",
				Value = () => (!Dir.Active) ? "APAGADO" : "PRENDIDO",
				OnChange = delegate
				{
					host.ToggleDirector();
				},
				Help = () => "Esconde a Niko y recorre tus camaras en loop. Tecla rapida: " + Cfg.KeyDirector
			},
			new MenuItem
			{
				Label = "Pasar a la siguiente camara",
				OnEnter = delegate
				{
					if (Dir.Active)
					{
						Dir.Next();
					}
					else
					{
						host.ToggleDirector();
					}
				},
				Help = () => "Tecla rapida: " + Cfg.KeyNextCam
			},
			new MenuItem
			{
				Label = "Escenas y transiciones",
				Submenu = Scenes,
				Help = () => "Cuanto dura cada escena, fundido o corte, zoom, movimiento..."
			},
			new MenuItem
			{
				Label = "Mis camaras",
				Value = () => CountActive() + " activas",
				Submenu = Cameras,
				Help = () => "Prender/apagar, duracion, zoom y transicion de cada camara. Importar las de Liberty's Legacy."
			},
			new MenuItem
			{
				Label = "Crear camara nueva (camara libre)",
				OnEnter = OpenEditor,
				Help = () => string.Concat("WASD mover, Q/E bajar/subir, mouse girar, Z/X zoom, ENTER guardar. ", Cfg.KeyEditor, " para salir.")
			},
			new MenuItem
			{
				Label = "Que pasa con cada evento de Kick",
				Submenu = Events,
				Help = () => "Suscripciones, subs regaladas, follows, raids, bans, cualquier mensaje..."
			},
			new MenuItem
			{
				Label = "NPC de suscriptores",
				Value = () => NpcSummary(),
				Submenu = NpcPage,
				Help = () => "Cuando alguien se suscribe aparece una persona con su nombre arriba de la cabeza. Segun los meses hace distintas cosas y la camara lo sigue"
			},
			new MenuItem
			{
				Label = "Comandos y palabras del chat",
				Value = () => Cfg.Triggers.Count.ToString(),
				Submenu = Commands,
				Help = () => "!boom, !poli... agregar, cambiar o borrar"
			},
			new MenuItem
			{
				Label = "Arreglar la calle despues del caos",
				Value = () => RepairSummary(),
				Submenu = RepairPage,
				Help = () => "Saca autos destruidos, cuerpos, fuego y escombros un rato despues de las acciones"
			},
			new MenuItem
			{
				Label = "Probar acciones",
				Submenu = Tests,
				Help = () => "Dispara cualquier accion o simula eventos de Kick sin el chat"
			},
			new MenuItem
			{
				Label = "Reglas y cooldowns",
				Submenu = Rules
			},
			new MenuItem
			{
				Label = "Hora, clima y trafico",
				Submenu = World
			},
			new MenuItem
			{
				Label = "Conexion con Kick",
				Submenu = Connection
			},
			new MenuItem
			{
				Label = "Teclas",
				Submenu = KeysPage
			},
			new MenuItem
			{
				Label = "Recargar archivos",
				OnEnter = delegate
				{
					host.FullReload();
					Menu.Toast("config.ini y camaras.ini recargados");
				},
				Help = () => "Si editaste config.ini o camaras.ini a mano"
			},
			new MenuItem
			{
				Label = "Cerrar menu",
				OnEnter = delegate
				{
					Menu.Close();
				}
			}
		});
	}

	private void OpenEditor()
	{
		Menu.Close();
		if (!Dir.EditorActive)
		{
			Dir.ToggleEditor();
		}
	}

	private int CountActive()
	{
		int num = 0;
		foreach (CameraShot shot in Dir.Shots)
		{
			if (shot.Active && !shot.IsAuto)
			{
				num++;
			}
		}
		return num;
	}

	private MenuPage Scenes()
	{
		return new MenuPage("Escenas y transiciones", () => new List<MenuItem>
		{
			Num("Duracion de cada escena", "Director", "DuracionPorDefecto", 20f, 3f, 600f, 1f, Sec, "Segundos que se queda cada camara (las que tengan duracion propia usan la suya). SHIFT = de a 10"),
			Choice("Transicion entre camaras", "Director", "Transicion", new string[2] { "fundido", "corte" }, new string[2] { "Fundido a negro", "Corte directo" }, "Fundido: pasa por negro y carga la zona. Corte: cambia al instante (puede verse algo de carga)"),
			Num("Duracion del fundido", "Director", "FundidoMs", 500f, 0f, 3000f, 100f, (float v) => Sec(v / 1000f), "Cuanto tarda en oscurecer y aclarar"),
			Num("Tiempo en negro para cargar", "Director", "EsperaCargaMs", 1500f, 0f, 6000f, 250f, (float v) => Sec(v / 1000f), "Mientras la pantalla esta en negro aparecen autos y gente en el plano nuevo"),
			Num("Zoom de camaras nuevas (FOV)", "Director", "FOVPorDefecto", 25f, 5f, 90f, 1f, (float v) => v.ToString("0"), "FOV bajo = teleobjetivo. El juego normal usa ~50"),
			Num("Movimiento de camara en mano", "Director", "Balanceo", 0.25f, 0f, 2f, 0.05f, (float v) => v.ToString("0.00", CultureInfo.InvariantCulture), "0 = camara fija"),
			Choice("Orden de las camaras", "Director", "Orden", new string[2] { "secuencial", "aleatorio" }, new string[2] { "En orden", "Al azar" }, string.Empty),
			Choice("Que camaras usar", "Director", "Modo", new string[3] { "lista", "auto", "mixto" }, new string[3] { "Mis camaras", "Automaticas", "Mezcladas" }, "Automaticas: planos que arma solo en calles cercanas. Si no tenes camaras activas usa automaticas"),
			Num("No cortar despues de una accion", "Director", "NoCortarTrasAccion", 8f, 0f, 60f, 1f, Sec, "Espera a que se vea la explosion/policia antes de cambiar de camara"),
			Bool("Arrancar solo al cargar la partida", "Director", "AutoIniciar", def: true, string.Empty),
			Num("Espera al cargar la partida", "Director", "EsperaAlCargar", 8f, 0f, 120f, 1f, Sec, string.Empty)
		});
	}

	private MenuPage Cameras()
	{
		return new MenuPage("Mis camaras", delegate
		{
			List<MenuItem> list = new List<MenuItem>
			{
				new MenuItem
				{
					Info = true,
					DynamicLabel = () => CountActive() + " de " + Dir.Shots.Count + " camaras en el loop"
				}
			};
			foreach (CameraShot shot in Dir.Shots)
			{
				if (!shot.IsAuto)
				{
					CameraShot s = shot;
					list.Add(new MenuItem
					{
						DynamicLabel = () => ((!Dir.Active || Dir.Current != s) ? string.Empty : "> ") + s.Name,
						Value = () => ((!s.Active) ? "APAGADA  " : string.Empty) + ((!(Math.Abs(s.Weight - 1f) > 0.001f)) ? string.Empty : ("x" + s.Weight.ToString("0.##", CultureInfo.InvariantCulture) + "  ")) + "FOV " + s.Fov.ToString("0") + "  " + ((!(s.Duration > 0f)) ? "gral" : Sec(s.Duration)),
						Submenu = () => CameraPage(s),
						Help = () => "ENTER para ajustar esta camara"
					});
				}
			}
			list.Add(new MenuItem
			{
				Label = "Importar camaras de Liberty's Legacy",
				OnEnter = delegate
				{
					List<string> log = new List<string>();
					int num = CameraStore.ImportLibertysLegacy(host.GameFolder, Dir.Shots, log);
					if (num > 0 && !Dir.Persist())
					{
						Menu.Toast("No se pudieron guardar las camaras importadas; revisa log.txt");
						return;
					}
					Menu.Toast((num <= 0) ? "No hay camaras nuevas en Liberty's Legacy\\Cameras" : ("Se agregaron " + num + " camaras de Liberty's Legacy"));
				},
				Help = () => "Trae las camaras que guardaste con el trainer (F11). No repite las que ya estan"
			});
			list.Add(new MenuItem
			{
				Label = "Crear camara nueva (camara libre)",
				OnEnter = OpenEditor
			});
			return list;
		});
	}

	private string ChancePercent(CameraShot s)
	{
		if (!s.Active)
		{
			return "apagada";
		}
		double num = 0.0;
		foreach (CameraShot shot in Dir.Shots)
		{
			if (shot.Active && !shot.IsAuto)
			{
				num += Math.Max(0.01, shot.Weight);
			}
		}
		if (num <= 0.0)
		{
			return "-";
		}
		return (100.0 * Math.Max(0.01, s.Weight) / num).ToString("0") + "%";
	}

	private MenuPage CameraPage(CameraShot s)
	{
		return new MenuPage(s.Name, () => new List<MenuItem>
		{
			new MenuItem
			{
				Label = "Ver esta camara ahora",
				OnEnter = delegate
				{
					Dir.GoTo(Dir.Shots.IndexOf(s));
					Menu.Toast("Yendo a " + s.Name);
				}
			},
			new MenuItem
			{
				Label = "Usar en el loop",
				Value = () => (!s.Active) ? "NO" : "SI",
				OnChange = delegate
				{
					s.Active = !s.Active;
					Dir.Persist();
				}
			},
			new MenuItem
			{
				Label = "Duracion",
				Value = () => (!(s.Duration > 0f)) ? ("General (" + Sec(Ini.GetFloat("Director", "DuracionPorDefecto", 20f)) + ")") : Sec(s.Duration),
				OnChange = delegate(int d)
				{
					float num = ((!(s.Duration > 0f)) ? 0f : s.Duration);
					num += (float)(d * 5);
					s.Duration = ((!(num < 5f)) ? Math.Min(600f, num) : (-1f));
					Dir.Persist();
				},
				Help = () => "Izquierda hasta 'General' para usar la duracion de Escenas y transiciones"
			},
			new MenuItem
			{
				Label = "Zoom (FOV)",
				Value = () => s.Fov.ToString("0"),
				OnChange = delegate(int d)
				{
					s.Fov = Clamp(s.Fov + (float)d, 3f, 120f);
					if (s.EndFov > 0f)
					{
						s.EndFov = Clamp(s.EndFov + (float)d, 3f, 120f);
					}
					Dir.Persist();
				},
				Help = () => "Si la camara se esta viendo, el cambio se ve en vivo"
			},
			new MenuItem
			{
				Label = "Chance en modo al azar",
				Value = () => "x" + s.Weight.ToString("0.##", CultureInfo.InvariantCulture) + "  (" + ChancePercent(s) + ")",
				OnChange = delegate(int d)
				{
					int i;
					for (i = 0; i < WeightSteps.Length - 1 && WeightSteps[i] < s.Weight - 0.001f; i++)
					{
					}
					i = Math.Max(0, Math.Min(WeightSteps.Length - 1, i + Math.Sign(d)));
					s.Weight = WeightSteps[i];
					Dir.Persist();
				},
				Help = () => (!Config.Normalize(Ini.Get("Director", "Orden", string.Empty)).StartsWith("alea")) ? "Solo cuenta con 'Orden de las camaras: Al azar' (Escenas y transiciones)" : "Mas alto = sale mas seguido. El % es la chance de que sea la proxima camara"
			},
			new MenuItem
			{
				Label = "Transicion al llegar a esta camara",
				Value = () => (s.Transition == 0) ? "Fundido" : ((s.Transition != 1) ? "General" : "Corte directo"),
				OnChange = delegate
				{
					s.Transition = ((s.Transition < 1) ? (s.Transition + 1) : (-1));
					Dir.Persist();
				}
			},
			new MenuItem
			{
				Label = "Hora del dia",
				Value = () => TimeLabel(TimeSlot(s.Hour, s.Minute)),
				OnChange = delegate(int d)
				{
					int num = NextSlot(TimeSlot(s.Hour, s.Minute), d);
					if (num < 0)
					{
						s.Hour = -1;
						s.Minute = 0;
					}
					else
					{
						s.Hour = num / 2;
						s.Minute = ((num % 2 != 0) ? 30 : 0);
					}
					Dir.Persist();
				},
				Help = () => "Normal = la hora corre como siempre (o la Hora fija general)"
			},
			new MenuItem
			{
				Label = "Clima",
				Value = () => WeatherLabels[s.Weather + 1],
				OnChange = delegate(int d)
				{
					s.Weather = NextWeather(s.Weather, d);
					Dir.Persist();
				}
			},
			new MenuItem
			{
				Label = "Movimiento de camara en mano",
				Value = () => (!(s.Sway >= 0f)) ? "General" : s.Sway.ToString("0.00", CultureInfo.InvariantCulture),
				OnChange = delegate(int d)
				{
					float num = ((!(s.Sway < 0f)) ? s.Sway : (-0.05f));
					num = (float)Math.Round(num + 0.05f * (float)Math.Sign(d), 2);
					s.Sway = ((!(num < 0f)) ? Math.Min(2f, num) : (-1f));
					Dir.Persist();
				}
			},
			new MenuItem
			{
				Label = "Mover arriba en la lista",
				OnEnter = delegate
				{
					int num = Dir.Shots.IndexOf(s);
					if (num > 0)
					{
						Dir.Shots.RemoveAt(num);
						Dir.Shots.Insert(num - 1, s);
						Dir.Persist();
						Menu.Toast("Ahora es la numero " + num);
					}
				}
			},
			new MenuItem
			{
				Label = "Mover abajo en la lista",
				OnEnter = delegate
				{
					int num = Dir.Shots.IndexOf(s);
					if (num >= 0 && num < Dir.Shots.Count - 1)
					{
						Dir.Shots.RemoveAt(num);
						Dir.Shots.Insert(num + 1, s);
						Dir.Persist();
						Menu.Toast("Ahora es la numero " + (num + 2));
					}
				}
			},
			new MenuItem
			{
				Label = "Borrar esta camara",
				OnEnter = delegate
				{
					if (Confirm(s, "borrar '" + s.Name + "'"))
					{
						Dir.Shots.Remove(s);
						Dir.Persist();
						Menu.Back();
						Menu.Toast("Camara borrada");
					}
				}
			}
		});
	}

	private MenuPage Events()
	{
		return new MenuPage("Eventos de Kick", delegate
		{
			List<MenuItem> list = new List<MenuItem>();
			List<string> list2 = new List<string>();
			for (int i = 0; i < EventDefs.GetLength(0); i++)
			{
				string text = EventDefs[i, 0];
				string label = EventDefs[i, 1];
				list2.Add(text);
				list.Add(EventItem(text, label));
			}
			foreach (string item in host.SeenOtherEvents())
			{
				if (!list2.Contains(item))
				{
					list2.Add(item);
					list.Add(EventItem(item, "Otro: " + item));
				}
			}
			foreach (KeyValuePair<string, List<ActionStep>> item2 in Cfg.EventMap)
			{
				if (!list2.Contains(item2.Key))
				{
					list2.Add(item2.Key);
					list.Add(EventItem(item2.Key, item2.Key));
				}
			}
			return list;
		});
	}

	private MenuItem EventItem(string key, string label)
	{
		MenuItem menuItem = new MenuItem();
		menuItem.Label = label;
		menuItem.Value = () => (!Cfg.EventMap.TryGetValue(key, out var value)) ? "Nada" : Steps(value);
		menuItem.Submenu = () => EventPage(key, label);
		return menuItem;
	}

	private MenuPage EventPage(string key, string label)
	{
		return new MenuPage(label, delegate
		{
			Cfg.EventMap.TryGetValue(key, out var _);
			List<MenuItem> list = new List<MenuItem>();
			for (int i = 0; i < 3; i++)
			{
				int slot = i;
				list.Add(new MenuItem
				{
					Label = "Accion " + (slot + 1),
					Value = delegate
					{
						Cfg.EventMap.TryGetValue(key, out var value2);
						return SlotName(Slots(value2)[slot]);
					},
					OnChange = delegate(int d)
					{
						Cfg.EventMap.TryGetValue(key, out var value2);
						string[] array = Slots(value2);
						array[slot] = CycleAction(array[slot], d);
						Set("Eventos", key, Join(array));
					},
					Help = () => "Izquierda/derecha para elegir. Se pueden poner hasta 3 cosas a la vez"
				});
			}
			if (key == "RegaloSubs")
			{
				list.Add(Bool("Repetir por cada sub regalada", "Reglas", "RepetirPorCadaRegalo", def: true, "5 subs regaladas = 5 veces (hasta el maximo de repeticiones)"));
			}
			if (key == "Mensaje")
			{
				list.Add(new MenuItem
				{
					Info = true,
					Label = "Respeta el cooldown por persona y el de la accion"
				});
			}
			if (key == "PrimerMensaje")
			{
				list.Add(new MenuItem
				{
					Info = true,
					Label = "Una vez por persona desde que abriste el juego"
				});
			}
			if (key == "ChatAFull")
			{
				list.Add(Num("Cuantos mensajes", "Reglas", "ChatAFullMensajes", 15f, 2f, 200f, 1f, (float v) => v.ToString("0"), "Mensajes que tienen que llegar..."));
				list.Add(Num("En cuantos segundos", "Reglas", "ChatAFullSegundos", 20f, 3f, 120f, 1f, Sec, "...dentro de este tiempo"));
				list.Add(Num("Esperar entre una y otra", "Reglas", "ChatAFullCooldown", 120f, 0f, 1800f, 10f, Sec, "Para que no se dispare todo el tiempo"));
			}
			if (key == "RegaloGrande")
			{
				list.Add(Num("Desde cuantas subs", "Reglas", "RegaloGrandeDesde", 5f, 2f, 100f, 1f, (float v) => v.ToString("0") + " subs", "Regalos de esta cantidad o mas usan este evento en vez de 'Subs regaladas' (pasa una vez, no por cada sub)"));
			}
			if (key == "KicksGrandes")
			{
				list.Add(Num("Desde cuantos kicks", "Reglas", "KicksGrandesDesde", 100f, 1f, 100000f, 10f, (float v) => v.ToString("0"), "Regalos de kicks de esta cantidad o mas usan este evento en vez de 'Kicks'"));
			}
			if (key == "RewardRedeemedEvent")
			{
				list.Add(new MenuItem
				{
					Info = true,
					Label = "Si Kick lo manda con otro nombre, aparece abajo como 'Otro'"
				});
			}
			list.Add(new MenuItem
			{
				Label = "Probar este evento",
				OnEnter = delegate
				{
					string text = ((key == "RegaloSubs") ? ":5" : ((key == "RegaloGrande") ? ":10" : ((!(key == "KicksGrandes")) ? string.Empty : ":500")));
					host.RunTest("evento:" + key + text);
					Menu.Toast("Evento simulado");
				}
			});
			return list;
		});
	}

	private MenuPage Commands()
	{
		return new MenuPage("Comandos del chat", delegate
		{
			List<MenuItem> list = new List<MenuItem>
			{
				new MenuItem
				{
					Label = "+ Agregar palabra o comando",
					OnEnter = delegate
					{
						Menu.StartTyping("Escribi la palabra (con ! adelante si es comando, ej: !boom)", "!", delegate(string text)
						{
							string text2 = text.Trim();
							if (text2.Length != 0 && !(text2 == "!"))
							{
								ChatTrigger chatTrigger = FindTrigger(text2);
								if (chatTrigger != null)
								{
									Menu.Toast("'" + text2 + "' ya existe");
									Menu.Push(CommandPage(chatTrigger.RawKey));
								}
								else
								{
									Set("Comandos", text2, "Explosion");
									ChatTrigger chatTrigger2 = FindTrigger(text2);
									if (chatTrigger2 != null)
									{
										Menu.Push(CommandPage(text2));
									}
									Menu.Toast("Agregado '" + text2 + "'. Elegi que hace");
								}
							}
						});
					},
					Help = () => "Con ! adelante: el mensaje tiene que empezar asi. Sin !: alcanza con que la palabra aparezca"
				}
			};
			foreach (ChatTrigger trigger in Cfg.Triggers)
			{
				string raw = trigger.RawKey;
				list.Add(new MenuItem
				{
					Label = raw,
					Value = delegate
					{
						ChatTrigger chatTrigger = FindTrigger(raw);
						return (chatTrigger != null) ? (Steps(chatTrigger.Steps) + RoleTag(chatTrigger.RequiredLevel)) : string.Empty;
					},
					Submenu = () => CommandPage(raw)
				});
			}
			return list;
		});
	}

	private ChatTrigger FindTrigger(string raw)
	{
		foreach (ChatTrigger trigger in Cfg.Triggers)
		{
			if (string.Equals(trigger.RawKey, raw, StringComparison.OrdinalIgnoreCase))
			{
				return trigger;
			}
		}
		return null;
	}

	private static string RoleTag(int lvl)
	{
		return (lvl <= 0) ? string.Empty : ("  (" + RoleLabels[Math.Min(4, lvl)] + ")");
	}

	private void SaveTrigger(string raw, string[] slots, int level, float cd)
	{
		string text = Join(slots);
		if (level > 0 || cd >= 0f)
		{
			text = text + " | " + RoleWords[Math.Max(0, Math.Min(4, level))];
		}
		if (cd >= 0f)
		{
			text = text + " | " + IniFile.F(cd);
		}
		Set("Comandos", raw, text);
	}

	private MenuPage CommandPage(string rawKey)
	{
		return new MenuPage(rawKey, delegate
		{
			List<MenuItem> list = new List<MenuItem>
			{
				new MenuItem
				{
					Info = true,
					DynamicLabel = () => (!rawKey.StartsWith("!")) ? ("Se activa si '" + rawKey + "' aparece en el mensaje") : ("El mensaje tiene que empezar con " + rawKey)
				},
				new MenuItem
				{
					Label = "Cambiar la palabra",
					Value = () => rawKey,
					OnEnter = delegate
					{
						Menu.StartTyping("Nueva palabra para '" + rawKey + "'", rawKey, delegate(string text)
						{
							string text2 = text.Trim();
							if (text2.Length == 0 || FindTrigger(text2) != null)
							{
								Menu.Toast("Esa palabra ya existe");
							}
							else
							{
								IniDoc iniDoc = IniDoc.Load(Path.Combine(host.Folder, "config.ini"));
								iniDoc.RenameKey("Comandos", rawKey, text2);
								iniDoc.Save();
								host.SoftReload();
								rawKey = text2;
								Menu.Back();
								Menu.Push(CommandPage(text2));
							}
						});
					}
				}
			};
			for (int num = 0; num < 3; num++)
			{
				int slot = num;
				list.Add(new MenuItem
				{
					Label = "Accion " + (slot + 1),
					Value = delegate
					{
						ChatTrigger chatTrigger = FindTrigger(rawKey);
						return (chatTrigger != null) ? SlotName(Slots(chatTrigger.Steps)[slot]) : string.Empty;
					},
					OnChange = delegate(int d)
					{
						ChatTrigger chatTrigger = FindTrigger(rawKey);
						if (chatTrigger != null)
						{
							string[] array = Slots(chatTrigger.Steps);
							array[slot] = CycleAction(array[slot], d);
							SaveTrigger(rawKey, array, chatTrigger.RequiredLevel, chatTrigger.CooldownSeconds);
						}
					}
				});
			}
			list.Add(new MenuItem
			{
				Label = "Quien puede usarlo",
				Value = delegate
				{
					ChatTrigger chatTrigger = FindTrigger(rawKey);
					return (chatTrigger != null) ? RoleLabels[Math.Min(4, chatTrigger.RequiredLevel)] : string.Empty;
				},
				OnChange = delegate(int d)
				{
					ChatTrigger chatTrigger = FindTrigger(rawKey);
					if (chatTrigger != null)
					{
						int level = ((chatTrigger.RequiredLevel + Math.Sign(d)) % 5 + 5) % 5;
						SaveTrigger(rawKey, Slots(chatTrigger.Steps), level, chatTrigger.CooldownSeconds);
					}
				},
				Help = () => "Subs incluye a VIP, mods y vos"
			});
			list.Add(new MenuItem
			{
				Label = "Cooldown propio",
				Value = delegate
				{
					ChatTrigger chatTrigger = FindTrigger(rawKey);
					return (chatTrigger == null) ? string.Empty : ((!(chatTrigger.CooldownSeconds >= 0f)) ? "El de la accion" : Sec(chatTrigger.CooldownSeconds));
				},
				OnChange = delegate(int d)
				{
					ChatTrigger chatTrigger = FindTrigger(rawKey);
					if (chatTrigger != null)
					{
						float num2 = ((!(chatTrigger.CooldownSeconds < 0f)) ? chatTrigger.CooldownSeconds : (-5f));
						num2 += (float)(5 * d);
						SaveTrigger(rawKey, Slots(chatTrigger.Steps), chatTrigger.RequiredLevel, (!(num2 < 0f)) ? Math.Min(3600f, num2) : (-1f));
					}
				},
				Help = () => "Segundos hasta que el chat lo pueda volver a usar. Izquierda hasta el final = el de la accion"
			});
			list.Add(new MenuItem
			{
				Label = "Probar (como si lo escribiera alguien)",
				OnEnter = delegate
				{
					host.SimulateChat("prueba", rawKey);
					Menu.Toast("Mensaje simulado: " + rawKey);
				}
			});
			list.Add(new MenuItem
			{
				Label = "Borrar este comando",
				OnEnter = delegate
				{
					if (Confirm(rawKey, "borrar '" + rawKey + "'"))
					{
						IniDoc iniDoc = IniDoc.Load(Path.Combine(host.Folder, "config.ini"));
						iniDoc.Remove("Comandos", rawKey);
						iniDoc.Save();
						host.SoftReload();
						Menu.Back();
						Menu.Toast("Borrado");
					}
				}
			});
			return list;
		});
	}

	private bool EventHasNpc(string key)
	{
		if (!Cfg.EventMap.TryGetValue(key, out var value))
		{
			return false;
		}
		foreach (ActionStep item in value)
		{
			if (ActionNames.Canonical(item.Name) == "Npc")
			{
				return true;
			}
		}
		return false;
	}

	private void ToggleEventNpc(string key)
	{
		Cfg.EventMap.TryGetValue(key, out var value);
		List<string> list = new List<string>(Slots(value));
		if (list.RemoveAll((string x) => ((x.IndexOf('*') <= 0) ? x : x.Substring(0, x.IndexOf('*'))) == "Npc") <= 0)
		{
			list.Insert(0, "Npc");
		}
		Set("Eventos", key, Join(list.ToArray()));
	}

	private string NpcSummary()
	{
		string text = ((!EventHasNpc("Suscripcion")) ? "APAGADO" : "PRENDIDO");
		int num = ((host.Npcs != null) ? host.Npcs.AliveCount : 0);
		return (num <= 0) ? text : (text + " (" + num + " en la calle)");
	}

	private static string BehaviorHelp()
	{
		return "Pasea: camina. Pinas: se pelea con la gente y cae la poli. Persecucion: escapa en auto con la policia atras. Tiroteo: contra la policia. Arrasa: contra todos. Roba un auto: le saca el auto a alguien y sale a dar vueltas";
	}

	private MenuItem BehaviorItem(Func<string> label, string key, string def)
	{
		Func<int> cur = delegate
		{
			NpcBehavior? npcBehavior = NpcRules.Parse(Ini.Get("Suscriptor", key, def));
			return (!npcBehavior.HasValue) ? (NpcRules.BehaviorKeys.Length - 1) : ((int)npcBehavior.Value);
		};
		MenuItem menuItem = new MenuItem();
		menuItem.DynamicLabel = label;
		menuItem.Value = () => NpcRules.BehaviorLabels[cur()];
		menuItem.OnChange = delegate(int d)
		{
			int num = NpcRules.BehaviorKeys.Length;
			Set("Suscriptor", key, NpcRules.BehaviorKeys[((cur() + Math.Sign(d)) % num + num) % num]);
		};
		menuItem.Help = BehaviorHelp;
		return menuItem;
	}

	private static string Months(int n)
	{
		return (n != 1) ? (n + " meses") : "1 mes";
	}

	private string TierLabel(int tier)
	{
		int[] array = ((host.Npcs != null) ? host.Npcs.TierFrom : new int[4] { 1, 2, 6, 12 });
		if (tier == 3)
		{
			return "Sub de " + Months(array[3]) + " o mas";
		}
		int num = array[tier];
		int num2 = array[tier + 1] - 1;
		return (num < num2) ? ("Sub de " + num + " a " + Months(num2)) : ("Sub de " + Months(num));
	}

	private MenuPage NpcPage()
	{
		return new MenuPage("NPC de suscriptores", () => new List<MenuItem>
		{
			new MenuItem
			{
				Info = true,
				DynamicLabel = () => "En la calle: " + ((host.Npcs == null) ? "-" : host.Npcs.Describe())
			},
			new MenuItem
			{
				Label = "Aparece con cada sub",
				Value = () => (!EventHasNpc("Suscripcion")) ? "NO" : "SI",
				OnChange = delegate
				{
					ToggleEventNpc("Suscripcion");
				},
				Help = () => "Agrega o saca 'NPC con su nombre' en Eventos > Nueva suscripcion. Las otras acciones de ese evento pasan cerca de el"
			},
			new MenuItem
			{
				Label = "Aparece el que regala subs",
				Value = () => (!EventHasNpc("RegaloSubs")) ? "NO" : "SI",
				OnChange = delegate
				{
					ToggleEventNpc("RegaloSubs");
				},
				Help = () => "Aparece una sola vez por regalo (aunque regale 10 subs)"
			},
			BehaviorItem(() => TierLabel(0), "Nivel1", "Pelea"),
			BehaviorItem(() => TierLabel(1), "Nivel2", "Huir"),
			BehaviorItem(() => TierLabel(2), "Nivel3", "Tiroteo"),
			BehaviorItem(() => TierLabel(3), "Nivel4", "Arrasar"),
			new MenuItem
			{
				Label = "Cambiar los meses de cada nivel",
				Submenu = NpcTiersPage
			},
			BehaviorItem(() => "El que regala subs", "Regalo", "Arrasar"),
			BehaviorItem(() => "Comando, follow u otro", "Otros", "Pasear"),
			new MenuItem
			{
				Label = "Bandas, tiroteos y comida",
				Value = () => GangSummary(),
				Submenu = NpcGangPage
			},
			new MenuItem
			{
				Label = "Control desde el chat (!)",
				Value = () => ControlSummary(),
				Submenu = NpcControlPage
			},
			new MenuItem
			{
				Label = "Cartel arriba de la cabeza",
				Submenu = NpcTagPage
			},
			new MenuItem
			{
				Label = "Camara que lo sigue",
				Submenu = NpcCameraPage
			},
			Num("Maximo a la vez", "Suscriptor", "Maximo", 3f, 1f, 7f, 1f, (float v) => v.ToString("0"), "Bandas a la vez. Si aparece otra, se va la mas vieja"),
			Num("Tiempo maximo", "Suscriptor", "TiempoMaximo", 180f, 30f, 900f, 10f, Sec, "Si sigue vivo despues de esto, se va (cada muerte le suma tiempo)"),
			Num("Tiempo paseando", "Suscriptor", "DuracionPasear", 70f, 10f, 600f, 10f, Sec, "Cuanto dura el que solo pasea"),
			Num("Patrulleros extra", "Suscriptor", "Patrulleros", 2f, 0f, 6f, 1f, (float v) => (!(v <= 2f)) ? ("+" + (v - 2f).ToString("0")) : "No", "La policia viene sola segun las estrellas: 1 patrullero por estrella (4: NOOSE, 5: FBI). Con esto llegan mas"),
			Num("Resistencia", "Suscriptor", "Vida", 1f, 0.3f, 5f, 0.1f, (float v) => "x" + v.ToString("0.0", CultureInfo.InvariantCulture), "Multiplica su vida"),
			new MenuItem
			{
				Label = "Probar: sub nueva (1 mes)",
				OnEnter = delegate
				{
					host.TestNpc("Suscripcion", 1);
					Menu.Toast("NPC en cola");
				}
			},
			new MenuItem
			{
				DynamicLabel = () => "Probar: sub de " + Months(host.Npcs.TierFrom[1]),
				OnEnter = delegate
				{
					host.TestNpc("Suscripcion", host.Npcs.TierFrom[1]);
					Menu.Toast("NPC en cola");
				}
			},
			new MenuItem
			{
				DynamicLabel = () => "Probar: sub de " + Months(host.Npcs.TierFrom[2]),
				OnEnter = delegate
				{
					host.TestNpc("Suscripcion", host.Npcs.TierFrom[2]);
					Menu.Toast("NPC en cola");
				}
			},
			new MenuItem
			{
				DynamicLabel = () => "Probar: sub de " + Months(host.Npcs.TierFrom[3]),
				OnEnter = delegate
				{
					host.TestNpc("Suscripcion", host.Npcs.TierFrom[3]);
					Menu.Toast("NPC en cola");
				}
			},
			new MenuItem
			{
				Label = "Probar: regalo de 5 subs",
				OnEnter = delegate
				{
					host.TestNpc("RegaloSubs", 5);
					Menu.Toast("NPC en cola");
				}
			},
			new MenuItem
			{
				Label = "Sacar a todos los NPC",
				OnEnter = delegate
				{
					host.Npcs.ReleaseAll();
					Menu.Toast("Listo: se van solos");
				},
				Help = () => "Dejan de ser del mod y el juego los saca cuando no se ven"
			}
		});
	}

	private string GangSummary()
	{
		int num = Math.Max(1, Math.Min(6, Ini.GetInt("Suscriptor", "BandaMaximo", 4)));
		return "hasta " + num;
	}

	private string ControlSummary()
	{
		int num = GangRules.ParseUnlock(Ini.Get("Suscriptor", "ControlDelChat", "AlDuplicarse"));
		if (num == 2)
		{
			return "NUNCA";
		}
		return GangRules.ModeLabels[GangRules.ParseMode(Ini.Get("Suscriptor", "QuienDecide", "Suscriptor"))];
	}

	private MenuPage NpcGangPage()
	{
		return new MenuPage("Bandas, tiroteos y comida", () => new List<MenuItem>
		{
			Bool("Se pelean entre ellos", "Suscriptor", "PelearEntreEllos", def: true, "Si hay dos o mas bandas, se buscan y tratan de matarse (los de la misma banda no se pelean)"),
			Bool("Van a buscar a la otra banda", "Suscriptor", "TraerBandas", def: true, "Si la otra banda esta lejos (mas de 350 m), aparecen cerca de ella fuera de camara: ya saben donde esta"),
			Choice("Se suma uno a la banda al", "Suscriptor", "SeDuplicaCon", GangRules.GrowKeys, GangRules.GrowLabels, "Policias", "Cada vez que mata a uno aparece otro de su banda al lado (la primera vez se desbloquea el control por chat)"),
			Num("Maximo por banda", "Suscriptor", "BandaMaximo", 4f, 1f, 6f, 1f, (float v) => v.ToString("0"), "Se llaman 'la banda de ...'. Entre todas las bandas, como mucho 16 personajes"),
			Num("Tiempo extra por muerte", "Suscriptor", "SegundosPorMuerte", 20f, 0f, 120f, 5f, (float v) => (!(v <= 0f)) ? ("+" + Sec(v)) : "Nada", "Cada vez que mata a alguien se suma este tiempo a su reloj"),
			Num("Vida para escapar", "Suscriptor", "VidaParaHuir", 35f, 0f, 80f, 5f, (float v) => (!(v <= 0f)) ? (v.ToString("0") + "%") : "Nunca", "Con menos vida busca un auto y se escapa (la policia lo persigue tirando). Si los pierde, se va a comprar comida y vuelve con toda la vida"),
			Num("Tiempo comprando comida", "Suscriptor", "SegundosComiendo", 7f, 2f, 30f, 1f, Sec, "Si le pegan mientras come, sigue escapando"),
			Num("Velocidad en auto", "Suscriptor", "VelocidadAuto", 30f, 10f, 60f, 5f, (float v) => v.ToString("0"), "Cuando escapa va un poco mas rapido"),
			Bool("Cobertura del juego", "Suscriptor", "CoberturaDelJuego", def: true, "Si no hay autos cerca para cubrirse, usa la cobertura del juego (paredes, columnas)"),
			Bool("Detalle en log.txt", "Suscriptor", "Diagnostico", def: true, "Anota cada 3 segundos que hace cada uno (los primeros 2 minutos y medio). Sirve para mandarme el log"),
			new MenuItem
			{
				Label = "Probar: sumar uno a la banda",
				OnEnter = delegate
				{
					Menu.Toast((!host.Npcs.TestGrow()) ? "No hay banda (o ya esta completa)" : "Se sumo uno a la banda");
				},
				Help = () => "Como si hubiera matado a un policia (la primera vez desbloquea el control por chat)"
			}
		});
	}

	private MenuPage NpcControlPage()
	{
		return new MenuPage("Control desde el chat", () => new List<MenuItem>
		{
			new MenuItem
			{
				Info = true,
				DynamicLabel = () => "Arriba del NPC: " + GangRules.ChoicesLine(host.Npcs.Choices) + "  (o !1 !2 !3)"
			},
			Choice("Se activa", "Suscriptor", "ControlDelChat", GangRules.UnlockKeys, GangRules.UnlockLabels, "AlDuplicarse", "Cuando mata a uno y se duplica, el chat puede darle ordenes con ! (aparecen arriba de su cabeza)"),
			Choice("Quien decide", "Suscriptor", "QuienDecide", GangRules.ModeKeys, GangRules.ModeLabels, "Suscriptor", "Solo el suscriptor: sus ordenes se cumplen al toque. Vota el chat: cuenta los votos un rato y gana la mas votada. El chat, el primero: vale la primera orden de cualquiera"),
			Num("Tiempo de votacion", "Suscriptor", "TiempoVotacion", 15f, 5f, 60f, 5f, Sec, "Arranca con el primer voto. Cada persona vota una vez (vale el ultimo)"),
			Num("Cuanto dura la orden", "Suscriptor", "DuracionOrden", 30f, 10f, 120f, 5f, Sec, "Despues vuelve a hacer lo suyo (!banda dura al menos 60 s)"),
			Num("Espera entre ordenes", "Suscriptor", "EsperaEntreOrdenes", 4f, 0f, 30f, 1f, Sec, "Para que no lo vuelvan loco"),
			SlotItem(0),
			SlotItem(1),
			SlotItem(2),
			new MenuItem
			{
				Label = "Probar: sumar uno (desbloquea)",
				OnEnter = delegate
				{
					Menu.Toast((!host.Npcs.TestGrow()) ? "No hay banda (o ya esta completa)" : "Se sumo uno a la banda");
				}
			},
			new MenuItem
			{
				Label = "Probar: el sub escribe en el chat",
				OnEnter = delegate
				{
					string text = host.Npcs.NewestUser();
					if (text == null)
					{
						Menu.Toast("No hay ningun NPC en la calle");
					}
					else
					{
						host.SimulateChat(text, "hola chat [emote:37226:KEKW] miren lo que hago con mi banda");
						Menu.Toast("Mensaje de " + text);
					}
				}
			},
			new MenuItem
			{
				DynamicLabel = () => "Probar: el sub escribe !" + GangRules.CmdKeys[host.Npcs.Choices[0]],
				OnEnter = delegate
				{
					string text = host.Npcs.NewestUser();
					if (text == null)
					{
						Menu.Toast("No hay ningun NPC en la calle");
					}
					else
					{
						host.SimulateChat(text, "!" + GangRules.CmdKeys[host.Npcs.Choices[0]]);
						Menu.Toast("Orden de " + text + " (si el control esta desbloqueado)");
					}
				}
			}
		});
	}

	private MenuItem SlotItem(int i)
	{
		string key = "Opcion" + (i + 1);
		Func<int, int> slot = delegate(int j)
		{
			int num = GangRules.ParseKey(Ini.Get("Suscriptor", "Opcion" + (j + 1), GangRules.DefaultSlots[j]));
			return (num < 0) ? GangRules.ParseKey(GangRules.DefaultSlots[j]) : num;
		};
		MenuItem menuItem = new MenuItem();
		menuItem.Label = "Opcion " + (i + 1);
		menuItem.Value = () => GangRules.CmdLabels[slot(i)];
		menuItem.OnChange = delegate(int d)
		{
			int num = GangRules.CmdKeys.Length;
			int num2 = slot(i);
			HashSet<int> hashSet = new HashSet<int>();
			for (int j = 0; j < 3; j++)
			{
				if (j != i)
				{
					hashSet.Add(slot(j));
				}
			}
			for (int k = 0; k < num; k++)
			{
				num2 = ((num2 + Math.Sign(d)) % num + num) % num;
				if (!hashSet.Contains(num2))
				{
					break;
				}
			}
			Set("Suscriptor", key, GangRules.CmdKeys[num2]);
		};
		menuItem.Help = () => "Las 3 ordenes que se pueden escribir en el chat (no se repiten). Tambien valen !1 !2 !3";
		return menuItem;
	}

	private MenuPage NpcTagPage()
	{
		return new MenuPage("Cartel arriba de la cabeza", () => new List<MenuItem>
		{
			Bool("Mostrar el nombre arriba", "Suscriptor", "MostrarNombre", def: true, "Nombre en blanco y los meses (o 'LA BANDA DE ...') en verde Kick"),
			Bool("Mensajes del sub", "Suscriptor", "MostrarMensajes", def: true, "Lo que escribe en el chat (sin !) aparece arriba de su personaje unos segundos"),
			Bool("Tiempo que le queda", "Suscriptor", "MostrarTiempo", def: true, "Reloj al lado de la barra de vida (cada muerte le suma tiempo)"),
			Bool("Barra de vida", "Suscriptor", "BarraDeVida", def: true, "Se va vaciando cuando le pegan"),
			Num("Tamano del cartel", "Suscriptor", "TamanoNombre", 1f, 0.5f, 2.5f, 0.1f, (float v) => "x" + v.ToString("0.0", CultureInfo.InvariantCulture), string.Empty),
			Bool("Cartel del juego (online)", "Suscriptor", "NombreDelJuego", def: false, "En vez del cartel propio usa el de los nombres del modo online de GTA IV (sin meses, barra, reloj ni mensajes)")
		});
	}

	private MenuPage NpcCameraPage()
	{
		return new MenuPage("Camara que lo sigue", () => new List<MenuItem>
		{
			Bool("Ir a su camara al aparecer", "Suscriptor", "IrAlAparecer", def: true, "La camara se va con el apenas aparece (con fundido)"),
			Num("Chance de su camara", "Suscriptor", "CamaraChance", 50f, 0f, 100f, 5f, (float v) => v.ToString("0") + "%", "Mientras este vivo, en cada cambio de camara hay esta chance de que la proxima sea la suya (prefiere a los que estan a los tiros). Si no, se elige una de tus camaras, con mas chance las que estan cerca del lio"),
			Num("Duracion de su toma", "Suscriptor", "CamaraDuracion", 30f, 5f, 300f, 5f, Sec, "Si muere antes, la toma termina unos segundos despues"),
			Num("Distancia de la camara", "Suscriptor", "CamaraDistancia", 22f, 5f, 150f, 1f, (float v) => v.ToString("0") + " m", "En auto se aleja un poco mas"),
			Num("Altura de la camara", "Suscriptor", "CamaraAltura", 6f, 1f, 80f, 1f, (float v) => v.ToString("0") + " m", "Si un edificio tapa, sube sola"),
			Num("Zoom de la camara (FOV)", "Suscriptor", "CamaraFOV", 30f, 5f, 90f, 1f, (float v) => v.ToString("0"), "FOV bajo = teleobjetivo")
		});
	}

	private MenuPage NpcTiersPage()
	{
		return new MenuPage("Meses de cada nivel", () => new List<MenuItem>
		{
			new MenuItem
			{
				Info = true,
				DynamicLabel = () => TierLabel(0) + " / " + TierLabel(1)
			},
			Num("Nivel 2 desde", "Suscriptor", "Nivel2Desde", 2f, 2f, 120f, 1f, (float v) => Months((int)v), string.Empty),
			Num("Nivel 3 desde", "Suscriptor", "Nivel3Desde", 6f, 3f, 120f, 1f, (float v) => Months((int)v), string.Empty),
			Num("Nivel 4 desde", "Suscriptor", "Nivel4Desde", 12f, 4f, 120f, 1f, (float v) => Months((int)v), "Cada nivel tiene que empezar despues del anterior")
		});
	}

	private string RepairSummary()
	{
		float num = Ini.GetFloat("Arreglar", "Tras", 60f);
		return (!(num <= 0f)) ? ("a los " + Sec(num)) : "NUNCA";
	}

	private MenuPage RepairPage()
	{
		return new MenuPage("Arreglar la calle", () => new List<MenuItem>
		{
			new MenuItem
			{
				Label = "Arreglar despues de",
				Value = delegate
				{
					float num = Ini.GetFloat("Arreglar", "Tras", 60f);
					return (!(num <= 0f)) ? Sec(num) : "Nunca";
				},
				OnChange = delegate(int d)
				{
					Set("Arreglar", "Tras", IniFile.F(Clamp(Snap(Ini.GetFloat("Arreglar", "Tras", 60f) + (float)(d * 5), 5f), 0f, 900f)));
				},
				Help = () => "Segundos desde la ultima accion (sub, explosion...). Si el chat no para, igual arregla cada tanto. 0 = nunca"
			},
			Choice("Como arreglarla", "Arreglar", "Forma", new string[3] { "fundido", "cambio", "directo" }, new string[3] { "Fundido y vuelve a la misma camara", "En el proximo cambio de camara", "Al instante (se ve)" }, "Fundido: se va a negro un momento, se arregla y vuelve la misma toma"),
			Num("Radio", "Arreglar", "Radio", 70f, 20f, 250f, 10f, (float v) => v.ToString("0") + " m", "Distancia alrededor de donde paso la accion"),
			Bool("Sacar escombros", "Arreglar", "Escombros", def: true, "Objetos rotos o tirados por las explosiones"),
			Bool("Sacar a la policia que quedo", "Arreglar", "Policias", def: true, "Patrulleros y policias que llegaron por las acciones"),
			new MenuItem
			{
				Info = true,
				DynamicLabel = delegate
				{
					double num = Dir.Repair.SecondsLeft();
					return (!(num < 0.0)) ? ("Proximo arreglo en " + Math.Ceiling(num) + " s") : ("Todo limpio (arreglos hechos: " + Dir.Repair.Repairs + ")");
				}
			},
			new MenuItem
			{
				Label = "Arreglar ahora",
				OnEnter = delegate
				{
					if (Dir.Repair.IsDirty)
					{
						Dir.RepairSoon();
						Menu.Toast("Arreglando la calle...");
					}
					else
					{
						Menu.Toast("No hay nada para arreglar");
					}
				}
			}
		});
	}

	private MenuPage Tests()
	{
		return new MenuPage("Probar acciones", delegate
		{
			List<MenuItem> list = new List<MenuItem>
			{
				new MenuItem
				{
					Label = "Simular una suscripcion",
					OnEnter = delegate
					{
						host.RunTest("evento:Suscripcion");
					}
				},
				new MenuItem
				{
					Label = "Simular 5 subs regaladas",
					OnEnter = delegate
					{
						host.RunTest("evento:RegaloSubs:5");
					}
				},
				new MenuItem
				{
					Label = "Simular un follow",
					OnEnter = delegate
					{
						host.RunTest("evento:Follow");
					}
				},
				new MenuItem
				{
					Label = "Simular un host / raid",
					OnEnter = delegate
					{
						host.RunTest("evento:Host");
					}
				},
				new MenuItem
				{
					Label = "NPC de suscriptor (6 meses)",
					OnEnter = delegate
					{
						host.TestNpc("Suscripcion", 6);
						Menu.Toast("NPC en cola");
					}
				}
			};
			string[] choices = ActionNames.Choices;
			foreach (string text in choices)
			{
				if (!(text == "Nada"))
				{
					string act = text;
					list.Add(new MenuItem
					{
						Label = ActionNames.Pretty(act),
						OnEnter = delegate
						{
							host.RunTest(act);
							Menu.Toast(ActionNames.Pretty(act) + " en cola");
						}
					});
				}
			}
			list.Add(new MenuItem
			{
				Label = "Vaciar la cola de acciones",
				Value = () => host.QueueCount() + " en espera",
				OnEnter = delegate
				{
					host.ClearQueue();
				}
			});
			return list;
		});
	}

	private MenuPage Rules()
	{
		return new MenuPage("Reglas y cooldowns", () => new List<MenuItem>
		{
			Num("Cooldown por persona", "Reglas", "CooldownPorUsuario", 15f, 0f, 600f, 1f, Sec, "Cada persona del chat puede disparar algo cada X segundos (vos no tenes limite)"),
			Num("Tiempo entre acciones", "Reglas", "SeparacionEntreAcciones", 2f, 0f, 60f, 0.5f, Sec, "Separacion entre una accion y la siguiente de la cola"),
			Num("Maximo de acciones en espera", "Reglas", "ColaMaxima", 15f, 1f, 100f, 1f, (float v) => v.ToString("0"), "Si se llena, se ignoran los pedidos del chat (los subs entran igual)"),
			Num("Maximo de repeticiones", "Reglas", "MaxRepeticiones", 10f, 1f, 50f, 1f, (float v) => v.ToString("0"), "Tope para Explosion*N y para las subs regaladas"),
			Bool("Subs y eventos ignoran cooldowns", "Reglas", "EventosIgnoranCooldown", def: true, string.Empty),
			Bool("Una accion por mensaje", "Reglas", "UnaAccionPorMensaje", def: true, "Si un mensaje tiene varias palabras clave, solo cuenta la primera"),
			Bool("Mostrar 'usuario -> accion' en el juego", "Reglas", "AvisoEnJuego", def: false, "Subtitulo dentro del juego. El texto para OBS se escribe siempre"),
			new MenuItem
			{
				Label = "Cooldown de cada accion",
				Submenu = ActionCooldowns
			}
		});
	}

	private MenuPage ActionCooldowns()
	{
		return new MenuPage("Cooldown de cada accion", delegate
		{
			List<MenuItem> list = new List<MenuItem>();
			string[] choices = ActionNames.Choices;
			foreach (string text in choices)
			{
				if (!(text == "Nada"))
				{
					list.Add(Num(ActionNames.Pretty(text), "Cooldowns", text, 8f, 0f, 3600f, 5f, Sec, "Segundos hasta que el chat la pueda repetir"));
				}
			}
			return list;
		});
	}

	private MenuPage World()
	{
		return new MenuPage("Hora, clima y trafico", () => new List<MenuItem>
		{
			new MenuItem
			{
				Label = "Hora fija",
				Value = delegate
				{
					CameraStore.ParseTime(Ini.Get("Director", "HoraFija", string.Empty), out var h, out var m);
					return TimeLabel(TimeSlot(h, m));
				},
				OnChange = delegate(int d)
				{
					CameraStore.ParseTime(Ini.Get("Director", "HoraFija", string.Empty), out var h, out var m);
					int num = NextSlot(TimeSlot(h, m), d);
					Set("Director", "HoraFija", (num >= 0) ? TimeLabel(num) : string.Empty);
				},
				Help = () => "Se aplica en cada cambio de camara (las camaras con hora propia usan la suya)"
			},
			new MenuItem
			{
				Label = "Clima fijo",
				Value = () => WeatherLabels[CameraStore.ParseWeather(Ini.Get("Director", "ClimaFijo", string.Empty)) + 1],
				OnChange = delegate(int d)
				{
					int num = NextWeather(CameraStore.ParseWeather(Ini.Get("Director", "ClimaFijo", string.Empty)), d);
					Set("Director", "ClimaFijo", (num >= 0) ? CameraStore.WeatherName(num) : string.Empty);
				}
			},
			Num("Cantidad de peatones", "Director", "DensidadPeatones", 1f, 0f, 1f, 0.1f, (float v) => (v * 100f).ToString("0") + "%", string.Empty),
			Num("Cantidad de trafico", "Director", "DensidadTrafico", 1f, 0f, 1f, 0.1f, (float v) => (v * 100f).ToString("0") + "%", "Menos trafico = menos choques"),
			Bool("Sacar autos chocados sin que se note", "Trafico", "SacarChocados", def: true, "Saca autos chocados, dados vuelta, en llamas o trabados cuando la camara no los ve"),
			Choice("Al cambiar de camara", "Trafico", "AlCambiarCamara", new string[3] { "chocados", "todo", "nada" }, new string[3] { "Sacar chocados y trabados", "Vaciar la calle", "No tocar nada" }, "Se hace mientras la pantalla esta en negro. 'Vaciar la calle' saca todos los autos y el trafico vuelve a entrar"),
			Num("Radio de limpieza", "Trafico", "Radio", 90f, 20f, 250f, 10f, (float v) => v.ToString("0") + " m", "Distancia alrededor del lugar de la escena"),
			new MenuItem
			{
				Info = true,
				DynamicLabel = () => "Autos sacados en esta sesion: " + Dir.Traffic.Removed
			},
			Num("Altura de Niko escondido", "Director", "AlturaJugadorOculto", 20f, 3f, 80f, 1f, (float v) => v.ToString("0") + " m", "Niko flota invisible arriba del lugar de las acciones para que haya autos y gente")
		});
	}

	private MenuPage Connection()
	{
		return new MenuPage("Conexion con Kick", () => new List<MenuItem>
		{
			new MenuItem
			{
				Info = true,
				DynamicLabel = () => "Estado: " + host.KickStatus()
			},
			new MenuItem
			{
				Info = true,
				DynamicLabel = () => "Canal: " + Cfg.Channel + "   (chat " + Cfg.ChatroomId + ")"
			},
			new MenuItem
			{
				Label = "Conectarse al chat",
				Value = () => (!Ini.GetBool("Kick", "Conectar", def: true)) ? "NO" : "SI",
				OnChange = delegate
				{
					IniDoc iniDoc = IniDoc.Load(Path.Combine(host.Folder, "config.ini"));
					iniDoc.Set("Kick", "Conectar", (!Ini.GetBool("Kick", "Conectar", def: true)) ? "si" : "no");
					iniDoc.Save();
					host.FullReload();
				}
			},
			new MenuItem
			{
				Label = "Reconectar ahora",
				OnEnter = delegate
				{
					host.FullReload();
					Menu.Toast("Reconectando...");
				}
			}
		});
	}

	private MenuItem KeyItem(string label, string key, Keys def, string help)
	{
		MenuItem menuItem = new MenuItem();
		menuItem.Label = label;
		menuItem.Value = () => ((object)Config.ParseKey(Ini.Get("Teclas", key), def)).ToString();
		menuItem.OnChange = delegate(int d)
		{
			Keys value = Config.ParseKey(Ini.Get("Teclas", key), def);
			int num = Array.IndexOf(KeyChoices, value);
			if (num < 0)
			{
				num = 0;
			}
			int num2 = KeyChoices.Length;
			Set("Teclas", key, KeyChoices[((num + Math.Sign(d)) % num2 + num2) % num2].ToString());
		};
		menuItem.Help = () => help;
		return menuItem;
	}

	private MenuPage KeysPage()
	{
		return new MenuPage("Teclas", () => new List<MenuItem>
		{
			KeyItem("Abrir este menu", "Menu", (Keys)119, "Liberty's Legacy usa F11 y TrafficKeeperLite F10: no elijas esas"),
			KeyItem("Prender / apagar el director", "Director", (Keys)120, string.Empty),
			KeyItem("Camara libre (crear camaras)", "Editor", (Keys)118, string.Empty),
			KeyItem("Siguiente camara", "SiguienteCamara", (Keys)117, string.Empty),
			KeyItem("Recargar archivos", "Recargar", (Keys)116, string.Empty),
			new MenuItem
			{
				Info = true,
				Label = "Menu: flechas, ENTER y RETROCESO (SHIFT = cambiar de a 10)"
			}
		});
	}

	static KickMenu()
	{
		KeyChoices = new Keys[] { Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12,
			Keys.Insert, Keys.Home, Keys.PageUp, Keys.Delete, Keys.End, Keys.PageDown, Keys.Pause, Keys.Scroll };
	}
}
