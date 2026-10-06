using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace KickChaos;

public static class CameraStore
{
	public const string FileName = "camaras.ini";

	private static readonly string[] WeatherNames = new string[8] { "extrasoleado", "soleado", "ventoso", "nublado", "lluvia", "llovizna", "niebla", "tormenta" };

	public static int ParseWeather(string v)
	{
		if (string.IsNullOrEmpty(v))
		{
			return -1;
		}
		string text = Config.Normalize(v).Trim();
		if (int.TryParse(text, out var result) && result >= 0 && result <= 7)
		{
			return result;
		}
		for (result = 0; result < WeatherNames.Length; result++)
		{
			if (WeatherNames[result] == text)
			{
				return result;
			}
		}
		switch (text)
		{
		case "sol":
		case "sunny":
		case "despejado":
			return 1;
		case "extrasunny":
			return 0;
		case "viento":
		case "windy":
			return 2;
		case "nubes":
		case "cloudy":
			return 3;
		case "rain":
			return 4;
		case "drizzle":
			return 5;
		case "fog":
		case "neblina":
			return 6;
		case "storm":
		case "tormenta electrica":
		case "rayos":
			return 7;
		default:
			return -1;
		}
	}

	public static string WeatherName(int w)
	{
		return (w < 0 || w >= WeatherNames.Length) ? string.Empty : WeatherNames[w];
	}

	public static bool ParseTime(string v, out int h, out int m)
	{
		h = -1;
		m = 0;
		if (string.IsNullOrEmpty(v))
		{
			return false;
		}
		string[] array = v.Trim().Split(':', '.', 'h');
		if (!int.TryParse(array[0].Trim(), out h) || h < 0 || h > 23)
		{
			h = -1;
			return false;
		}
		if (array.Length > 1 && array[1].Trim().Length > 0 && !int.TryParse(array[1].Trim(), out m))
		{
			m = 0;
		}
		m = Math.Max(0, Math.Min(59, m));
		return true;
	}

	private static bool V(IniSection s, string key, out Vector3 v)
	{
		v = Vector3.Zero;
		if (!IniFile.TryVec3(s.Get(key), out var x, out var y, out var z) || !MathX.IsFinite(x) || !MathX.IsFinite(y) || !MathX.IsFinite(z))
		{
			return false;
		}
		v = new Vector3(x, y, z);
		return true;
	}

	public static List<CameraShot> Load(string folder, float defFov, float defDuration, List<string> warnings)
	{
		defFov = MathX.Clamp(defFov, 3f, 120f, 25f);
		List<CameraShot> list = new List<CameraShot>();
		IniFile iniFile = IniFile.Load(Path.Combine(folder, "camaras.ini"));
		foreach (IniSection section in iniFile.Sections)
		{
			CameraShot cameraShot = new CameraShot();
			cameraShot.Name = section.Name;
			cameraShot.Fov = defFov;
			cameraShot.Duration = -1f;
			CameraShot cameraShot2 = cameraShot;
			if (!V(section, "Pos", out cameraShot2.Pos))
			{
				warnings.Add("Camara '" + section.Name + "' sin Pos valida, se ignora");
				continue;
			}
			if (V(section, "MirarA", out var v))
			{
				cameraShot2.Rot = MathX.LookRotation(cameraShot2.Pos, v);
			}
			else if (!V(section, "Rot", out cameraShot2.Rot))
			{
				warnings.Add("Camara '" + section.Name + "' sin Rot ni MirarA, se ignora");
				continue;
			}
			if (IniFile.TryFloat(section.Get("FOV", string.Empty), out var r) && MathX.IsFinite(r))
			{
				cameraShot2.Fov = Clamp(r, 3f, 120f);
			}
			if (IniFile.TryFloat(section.Get("Duracion", string.Empty), out r) && MathX.IsFinite(r) && r > 0f)
			{
				cameraShot2.Duration = Math.Max(3f, r);
			}
			cameraShot2.Active = IniFile.ParseBool(section.Get("Activa", "si"), def: true);
			if (IniFile.TryFloat(section.Get("Chance", string.Empty), out r) && MathX.IsFinite(r) && r > 0f)
			{
				cameraShot2.Weight = Math.Min(100f, r);
			}
			string text = Config.Normalize(section.Get("Transicion", string.Empty));
			cameraShot2.Transition = ((!text.StartsWith("fund")) ? (text.StartsWith("corte") ? 1 : (-1)) : 0);
			if (IniFile.TryFloat(section.Get("Balanceo", string.Empty), out r) && MathX.IsFinite(r))
			{
				cameraShot2.Sway = Math.Max(0f, r);
			}
			if (V(section, "PosFinal", out cameraShot2.EndPos))
			{
				cameraShot2.HasEnd = true;
				if (V(section, "MirarAFinal", out var v2))
				{
					cameraShot2.EndRot = MathX.LookRotation(cameraShot2.EndPos, v2);
				}
				else if (!V(section, "RotFinal", out cameraShot2.EndRot))
				{
					cameraShot2.EndRot = cameraShot2.Rot;
				}
			}
			else if (V(section, "RotFinal", out cameraShot2.EndRot))
			{
				cameraShot2.HasEnd = true;
				cameraShot2.EndPos = cameraShot2.Pos;
			}
			if (IniFile.TryFloat(section.Get("FOVFinal", string.Empty), out r) && MathX.IsFinite(r) && r > 0f)
			{
				cameraShot2.EndFov = Clamp(r, 3f, 120f);
				if (!cameraShot2.HasEnd)
				{
					cameraShot2.HasEnd = true;
					cameraShot2.EndPos = cameraShot2.Pos;
					cameraShot2.EndRot = cameraShot2.Rot;
				}
			}
			cameraShot2.HasTarget = V(section, "Objetivo", out cameraShot2.Target);
			cameraShot2.HasAnchor = V(section, "Ancla", out cameraShot2.Anchor);
			ParseTime(section.Get("Hora", string.Empty), out cameraShot2.Hour, out cameraShot2.Minute);
			cameraShot2.Weather = ParseWeather(section.Get("Clima", string.Empty));
			list.Add(cameraShot2);
		}
		return list;
	}

	private static float Clamp(float v, float a, float b)
	{
		return (v < a) ? a : ((!(v > b)) ? v : b);
	}

	public static string Serialize(CameraShot c)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string name = (c.Name ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("]", ")").Replace("[", "(").Trim();
		stringBuilder.AppendLine("[" + name + "]");
		stringBuilder.AppendLine("Pos = " + IniFile.V3(c.Pos.X, c.Pos.Y, c.Pos.Z));
		stringBuilder.AppendLine("Rot = " + IniFile.V3(c.Rot.X, c.Rot.Y, c.Rot.Z));
		stringBuilder.AppendLine("FOV = " + IniFile.F(c.Fov));
		if (c.Duration > 0f)
		{
			stringBuilder.AppendLine("Duracion = " + IniFile.F(c.Duration));
		}
		if (!c.Active)
		{
			stringBuilder.AppendLine("Activa = no");
		}
		if (Math.Abs(c.Weight - 1f) > 0.001f)
		{
			stringBuilder.AppendLine("Chance = " + IniFile.F(c.Weight));
		}
		if (c.Transition == 0)
		{
			stringBuilder.AppendLine("Transicion = fundido");
		}
		if (c.Transition == 1)
		{
			stringBuilder.AppendLine("Transicion = corte");
		}
		if (c.HasEnd)
		{
			stringBuilder.AppendLine("PosFinal = " + IniFile.V3(c.EndPos.X, c.EndPos.Y, c.EndPos.Z));
			stringBuilder.AppendLine("RotFinal = " + IniFile.V3(c.EndRot.X, c.EndRot.Y, c.EndRot.Z));
			if (c.EndFov > 0f)
			{
				stringBuilder.AppendLine("FOVFinal = " + IniFile.F(c.EndFov));
			}
		}
		if (c.HasTarget)
		{
			stringBuilder.AppendLine("Objetivo = " + IniFile.V3(c.Target.X, c.Target.Y, c.Target.Z));
		}
		if (c.HasAnchor)
		{
			stringBuilder.AppendLine("Ancla = " + IniFile.V3(c.Anchor.X, c.Anchor.Y, c.Anchor.Z));
		}
		if (c.Hour >= 0)
		{
			stringBuilder.AppendLine("Hora = " + c.Hour.ToString("00") + ":" + c.Minute.ToString("00"));
		}
		if (c.Weather >= 0)
		{
			stringBuilder.AppendLine("Clima = " + WeatherName(c.Weather));
		}
		if (c.Sway >= 0f)
		{
			stringBuilder.AppendLine("Balanceo = " + IniFile.F(c.Sway));
		}
		return stringBuilder.ToString();
	}

	public static void Save(string folder, List<CameraShot> shots)
	{
		string path = Path.Combine(folder, "camaras.ini");
		string text = ((!File.Exists(path)) ? string.Empty : IniFile.Load(path).HeaderComment);
		StringBuilder stringBuilder = new StringBuilder(text.TrimEnd());
		if (stringBuilder.Length > 0)
		{
			stringBuilder.AppendLine().AppendLine();
		}
		foreach (CameraShot shot in shots)
		{
			if (!shot.IsAuto)
			{
				stringBuilder.Append(Serialize(shot)).AppendLine();
			}
		}
		// Write on the same volume and replace only once the complete file is on
		// disk. Keep the previous cameras as a backup if saving is interrupted.
		Directory.CreateDirectory(folder);
		string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(stringBuilder.ToString());
			using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				stream.Write(bytes, 0, bytes.Length);
				stream.Flush(true);
			}
			if (File.Exists(path))
			{
				File.Replace(temporary, path, path + ".bak");
			}
			else
			{
				File.Move(temporary, path);
			}
		}
		finally
		{
			if (File.Exists(temporary))
			{
				try
				{
					File.Delete(temporary);
				}
				catch
				{
				}
			}
		}
	}

	public static int ImportLibertysLegacy(string gameFolder, List<CameraShot> shots, List<string> log)
	{
		string path = Path.Combine(Path.Combine(gameFolder, "Liberty's Legacy"), "Cameras");
		if (!Directory.Exists(path))
		{
			return 0;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CameraShot shot in shots)
		{
			hashSet.Add(shot.Name);
		}
		int num = 0;
		string[] files = Directory.GetFiles(path, "*.ini");
		Array.Sort(files, (IComparer<string>)StringComparer.OrdinalIgnoreCase);
		string[] array = files;
		foreach (string path2 in array)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path2);
			if (hashSet.Contains(fileNameWithoutExtension))
			{
				continue;
			}
			IniFile iniFile = IniFile.Load(path2);
			float num2 = iniFile.GetFloat("camera_data", "X", float.NaN);
			float num3 = iniFile.GetFloat("camera_data", "Y", float.NaN);
			float num4 = iniFile.GetFloat("camera_data", "Z", float.NaN);
			if (!MathX.IsFinite(num2) || !MathX.IsFinite(num3) || !MathX.IsFinite(num4))
			{
				log?.Add("sin posicion: " + fileNameWithoutExtension);
				continue;
			}
			CameraShot cameraShot = new CameraShot();
			cameraShot.Name = fileNameWithoutExtension;
			cameraShot.Pos = new Vector3(num2, num3, num4);
			cameraShot.Rot = new Vector3(MathX.Clamp(iniFile.GetFloat("camera_data", "rotX", 0f), -360000f, 360000f, 0f), MathX.Clamp(iniFile.GetFloat("camera_data", "rotY", 0f), -360000f, 360000f, 0f), MathX.Clamp(iniFile.GetFloat("camera_data", "rotZ", 0f), -360000f, 360000f, 0f));
			cameraShot.Fov = MathX.Clamp(iniFile.GetFloat("camera_data", "FOV", 25f), 3f, 120f, 25f);
			CameraShot cameraShot2 = cameraShot;
			int num5 = iniFile.GetInt("camera_data", "Hour", -1);
			if (num5 >= 0 && num5 <= 23)
			{
				cameraShot2.Hour = num5;
				cameraShot2.Minute = Math.Max(0, Math.Min(59, iniFile.GetInt("camera_data", "Minute", 0)));
			}
			int num6 = iniFile.GetInt("camera_data", "Weather", -1);
			if (num6 >= 0 && num6 <= 7)
			{
				cameraShot2.Weather = num6;
			}
			shots.Add(cameraShot2);
			hashSet.Add(fileNameWithoutExtension);
			num++;
		}
		return num;
	}

	public static string NextName(List<CameraShot> shots)
	{
		int i = 1;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CameraShot shot in shots)
		{
			hashSet.Add(shot.Name);
		}
		for (; hashSet.Contains("Camara " + i); i++)
		{
		}
		return "Camara " + i;
	}
}
