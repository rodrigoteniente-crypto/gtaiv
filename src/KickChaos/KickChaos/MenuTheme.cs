using System;
using System.Drawing;
using System.IO;

namespace KickChaos;

public class MenuTheme
{
	public Color Title = Color.FromArgb(255, 140, 140, 140);

	public Color Text = Color.FromArgb(255, 140, 140, 140);

	public Color Selected = Color.FromArgb(255, 240, 160, 0);

	public Color SelectedBack = Color.FromArgb(200, 62, 62, 62);

	public Color Background = Color.FromArgb(180, 0, 0, 0);

	public Color Footer = Color.FromArgb(255, 0, 0, 0);

	public float X = 0.02f;

	public float Y = 0.05f;

	private static Color Read(IniFile ini, string name, Color def)
	{
		int arg = ini.GetInt("menu_colors", name + " Red", def.R);
		int arg2 = ini.GetInt("menu_colors", name + " Green", def.G);
		int arg3 = ini.GetInt("menu_colors", name + " Blue", def.B);
		int arg4 = ini.GetInt("menu_colors", name + " Alpha", def.A);
		Func<int, int> func = (int v) => Math.Max(0, Math.Min(255, v));
		return Color.FromArgb(func(arg4), func(arg), func(arg2), func(arg3));
	}

	public static MenuTheme FromLibertysLegacy(string gameFolder)
	{
		MenuTheme menuTheme = new MenuTheme();
		try
		{
			string path = Path.Combine(Path.Combine(gameFolder, "Liberty's Legacy"), "Liberty's Legacy.ini");
			if (!File.Exists(path))
			{
				return menuTheme;
			}
			IniFile ini = IniFile.Load(path);
			menuTheme.Title = Read(ini, "Title", menuTheme.Title);
			menuTheme.Text = Read(ini, "Text", menuTheme.Text);
			menuTheme.Selected = Read(ini, "On Select", menuTheme.Selected);
			menuTheme.SelectedBack = Read(ini, "On Back", menuTheme.SelectedBack);
			menuTheme.Background = Read(ini, "Background", menuTheme.Background);
			menuTheme.Footer = Read(ini, "Footer", menuTheme.Footer);
		}
		catch
		{
		}
		return menuTheme;
	}
}
