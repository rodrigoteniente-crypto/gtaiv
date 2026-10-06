using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using GTA;

namespace KickChaos;

public class Menu
{
	public string Header = "KICKCHAOS IV";

	public const int VisibleRows = 13;

	private readonly Stack<MenuPage> stack = new Stack<MenuPage>();

	private readonly Func<MenuPage> rootFactory;

	private volatile MenuSnapshot snapshot;

	private readonly Dictionary<Keys, double> nextRepeat = new Dictionary<Keys, double>();

	private readonly HashSet<Keys> down = new HashSet<Keys>();

	private string toast;

	private double toastUntil;

	private string typingPrompt;

	private string typingBuffer;

	private Action<string> typingDone;

	private static readonly Keys[] WatchedKeys;

	private Font fontHeader;

	private Font fontItem;

	private Font fontSmall;

	public bool IsOpen { get; private set; }

	public static float Aspect
	{
		get
		{
			return MenuText.Aspect;
		}
		set
		{
			MenuText.Aspect = value;
		}
	}

	public MenuSnapshot Snapshot => snapshot;

	public Menu(Func<MenuPage> rootFactory)
	{
		this.rootFactory = rootFactory;
	}

	public void Toggle()
	{
		if (IsOpen)
		{
			Close();
		}
		else
		{
			Open();
		}
	}

	public void Open()
	{
		stack.Clear();
		MenuPage menuPage = rootFactory();
		menuPage.Refresh();
		stack.Push(menuPage);
		IsOpen = true;
		down.Clear();
		double now = G.Now;
		Keys[] watchedKeys = WatchedKeys;
		foreach (Keys val in watchedKeys)
		{
			if (G.KeyDown(val))
			{
				down.Add(val);
				nextRepeat[val] = now + 0.35;
			}
		}
	}

	public void Close()
	{
		IsOpen = false;
		typingDone = null;
		snapshot = null;
	}

	public void Push(MenuPage p)
	{
		p.Refresh();
		stack.Push(p);
	}

	public void Back()
	{
		if (stack.Count > 1)
		{
			stack.Pop();
			stack.Peek().Refresh();
		}
		else
		{
			Close();
		}
	}

	public void RefreshCurrent()
	{
		if (stack.Count > 0)
		{
			stack.Peek().Refresh();
		}
	}

	public void Toast(string text)
	{
		toast = text;
		toastUntil = G.Now + 3.0;
	}

	public void StartTyping(string prompt, string initial, Action<string> done)
	{
		typingPrompt = prompt;
		typingBuffer = initial ?? string.Empty;
		typingDone = done;
	}

	private bool Pressed(Keys k, double now, bool repeat)
	{
		if (!G.KeyDown(k))
		{
			down.Remove(k);
			return false;
		}
		if (!down.Contains(k))
		{
			down.Add(k);
			nextRepeat[k] = now + 0.35;
			return true;
		}
		if (repeat && nextRepeat.TryGetValue(k, out var value) && now >= value)
		{
			nextRepeat[k] = now + 0.06;
			return true;
		}
		return false;
	}

	public void Update(bool focused)
	{
		if (!IsOpen)
		{
			return;
		}
		double now = G.Now;
		if (focused)
		{
			if (typingDone != null)
			{
				UpdateTyping(now);
			}
			else
			{
				UpdateNavigation(now);
			}
		}
		if (IsOpen)
		{
			BuildSnapshot(now);
		}
	}

	private void UpdateTyping(double now)
	{
		bool flag = G.KeyDown((Keys)16);
		if (Pressed((Keys)13, now, repeat: false))
		{
			Action<string> action = typingDone;
			typingDone = null;
			string text = typingBuffer.Trim();
			if (text.Length > 0)
			{
				action(text);
			}
			RefreshCurrent();
			return;
		}
		if (Pressed((Keys)8, now, repeat: true))
		{
			if (typingBuffer.Length == 0)
			{
				typingDone = null;
				return;
			}
			typingBuffer = typingBuffer.Substring(0, typingBuffer.Length - 1);
		}
		if (typingBuffer.Length >= 30)
		{
			return;
		}
		for (Keys val = (Keys)65; (int)val <= 90; val = (Keys)(val + 1))
		{
			if (Pressed(val, now, repeat: true))
			{
				typingBuffer += (char)(97 + (val - 65));
			}
		}
		for (Keys val2 = (Keys)48; (int)val2 <= 57; val2 = (Keys)(val2 + 1))
		{
			if (Pressed(val2, now, repeat: true))
			{
				typingBuffer += ((!flag || (int)val2 != 49) ? ((char)(48 + (val2 - 48))).ToString() : "!");
			}
		}
		if (Pressed((Keys)32, now, repeat: true))
		{
			typingBuffer += " ";
		}
		if (Pressed((Keys)189, now, repeat: true))
		{
			typingBuffer += "_";
		}
	}

	private void UpdateNavigation(double now)
	{
		MenuPage menuPage = stack.Peek();
		int count = menuPage.Items.Count;
		if (Pressed((Keys)38, now, repeat: true) && count > 0)
		{
			menuPage.Selected = (menuPage.Selected - 1 + count) % count;
			menuPage.MoveToSelectable(-1);
		}
		if (Pressed((Keys)40, now, repeat: true) && count > 0)
		{
			menuPage.Selected = (menuPage.Selected + 1) % count;
			menuPage.MoveToSelectable(1);
		}
		MenuItem menuItem = ((count <= 0) ? null : menuPage.Items[menuPage.Selected]);
		int num = ((!G.KeyDown((Keys)16)) ? 1 : 10);
		if (menuItem != null && !menuItem.Info && menuItem.OnChange != null)
		{
			if (Pressed((Keys)37, now, repeat: true))
			{
				menuItem.OnChange(-num);
				menuPage.Refresh();
			}
			if (Pressed((Keys)39, now, repeat: true))
			{
				menuItem.OnChange(num);
				menuPage.Refresh();
			}
		}
		else
		{
			Pressed((Keys)37, now, repeat: false);
			Pressed((Keys)39, now, repeat: false);
		}
		if (Pressed((Keys)13, now, repeat: false) && menuItem != null && !menuItem.Info)
		{
			if (menuItem.Submenu != null)
			{
				Push(menuItem.Submenu());
			}
			else if (menuItem.OnEnter != null)
			{
				menuItem.OnEnter();
				if (IsOpen && stack.Count > 0)
				{
					stack.Peek().Refresh();
				}
			}
			else if (menuItem.OnChange != null)
			{
				menuItem.OnChange(1);
				menuPage.Refresh();
			}
		}
		if (Pressed((Keys)8, now, repeat: false))
		{
			Back();
		}
		if (stack.Count > 0)
		{
			menuPage = stack.Peek();
			if (menuPage.Selected < menuPage.Scroll)
			{
				menuPage.Scroll = menuPage.Selected;
			}
			if (menuPage.Selected >= menuPage.Scroll + 13)
			{
				menuPage.Scroll = menuPage.Selected - 13 + 1;
			}
		}
	}

	private void BuildSnapshot(double now)
	{
		if (stack.Count == 0)
		{
			snapshot = null;
			return;
		}
		MenuPage menuPage = stack.Peek();
		MenuSnapshot menuSnapshot = new MenuSnapshot();
		menuSnapshot.Header = Header;
		menuSnapshot.Title = menuPage.Title;
		MenuSnapshot menuSnapshot2 = menuSnapshot;
		int num = Math.Min(menuPage.Items.Count, menuPage.Scroll + 13);
		for (int i = menuPage.Scroll; i < num; i++)
		{
			MenuItem menuItem = menuPage.Items[i];
			string empty = string.Empty;
			try
			{
				empty = ((menuItem.Value != null) ? menuItem.Value() : ((menuItem.Submenu == null) ? string.Empty : ">"));
			}
			catch
			{
				empty = "?";
			}
			bool flag = i == menuPage.Selected;
			if (flag && menuItem.OnChange != null && !menuItem.Info)
			{
				empty = "<  " + empty + "  >";
			}
			string text;
			try
			{
				text = menuItem.GetLabel();
			}
			catch
			{
				text = "?";
			}
			menuSnapshot2.Rows.Add(new string[3]
			{
				text,
				empty,
				((!flag) ? string.Empty : "s") + ((!menuItem.Info) ? string.Empty : "i")
			});
		}
		menuSnapshot2.MoreAbove = menuPage.Scroll > 0;
		menuSnapshot2.MoreBelow = num < menuPage.Items.Count;
		int num2 = 0;
		int num3 = 0;
		for (int j = 0; j < menuPage.Items.Count; j++)
		{
			if (!menuPage.Items[j].Info)
			{
				num2++;
				if (j <= menuPage.Selected)
				{
					num3 = num2;
				}
			}
		}
		menuSnapshot2.Footer = ((num2 <= 0) ? string.Empty : (num3 + " / " + num2));
		MenuItem menuItem2 = ((menuPage.Items.Count <= 0) ? null : menuPage.Items[menuPage.Selected]);
		string text2 = null;
		try
		{
			text2 = ((menuItem2 == null || menuItem2.Help == null) ? null : menuItem2.Help());
		}
		catch
		{
		}
		if (now < toastUntil)
		{
			text2 = toast;
		}
		menuSnapshot2.Help = text2 ?? "Flechas: elegir y cambiar   ENTER: aceptar   RETROCESO: volver";
		if (typingDone != null)
		{
			menuSnapshot2.Typing = typingPrompt + "\n> " + typingBuffer + "_\n(ENTER confirma, RETROCESO borra / cancela)";
		}
		snapshot = menuSnapshot2;
	}

	public void Draw(Graphics g, MenuTheme th)
	{
		MenuSnapshot menuSnapshot = snapshot;
		if (menuSnapshot == null)
		{
			return;
		}
		g.Scaling = (FontScaling)1;
		if (fontHeader == null)
		{
			fontHeader = new Font("Arial", 0.04f, (FontScaling)1, true, false);
			fontItem = new Font("Arial", 0.026f, (FontScaling)1, false, false);
			fontSmall = new Font("Arial", 0.022f, (FontScaling)1, false, false);
		}
		float x = th.X;
		float num = 0.31f;
		float y = th.Y;
		float num2 = 0.034f;
		float num3 = 0.008f;
		g.DrawRectangle(new RectangleF(x, y, num, 0.065f), th.Footer);
		g.DrawText(menuSnapshot.Header, new RectangleF(x, y, num, 0.065f), (TextAlignment)37, th.Selected, fontHeader);
		y += 0.065f;
		g.DrawRectangle(new RectangleF(x, y, num, num2), th.Footer);
		g.DrawText(menuSnapshot.Title.ToUpperInvariant(), new RectangleF(x + num3, y, num - 2f * num3, num2), (TextAlignment)36, th.Title, fontItem);
		g.DrawText(menuSnapshot.Footer, new RectangleF(x + num3, y, num - 2f * num3, num2), (TextAlignment)38, th.Title, fontItem);
		y += num2;
		int num4 = Math.Max(1, menuSnapshot.Rows.Count);
		g.DrawRectangle(new RectangleF(x, y, num, num2 * (float)num4), th.Background);
		if (menuSnapshot.MoreAbove)
		{
			g.DrawText("^", new RectangleF(x, y - 0.012f, num, 0.012f), (TextAlignment)1, th.Text, fontSmall);
		}
		foreach (string[] row in menuSnapshot.Rows)
		{
			bool flag = row[2].Contains("s");
			bool flag2 = row[2].Contains("i");
			Color color = (flag ? th.Selected : ((!flag2) ? th.Text : Color.FromArgb(th.Text.A, Math.Min(255, th.Text.R + 60), Math.Min(255, th.Text.G + 60), Math.Min(255, th.Text.B + 60))));
			if (flag)
			{
				g.DrawRectangle(new RectangleF(x, y, num, num2), th.SelectedBack);
			}
			RectangleF rectangleF = new RectangleF(x + num3, y, num - 2f * num3, num2);
			string label = row[0];
			string value = row[1];
			MenuText.FitRow(ref label, ref value, num - 2f * num3);
			g.DrawText(label, rectangleF, (TextAlignment)36, color, fontItem);
			if (value.Length > 0)
			{
				g.DrawText(value, rectangleF, (TextAlignment)38, color, fontItem);
			}
			y += num2;
		}
		if (menuSnapshot.MoreBelow)
		{
			g.DrawText("v", new RectangleF(x, y, num, 0.014f), (TextAlignment)1, th.Text, fontSmall);
		}
		y += 0.016f;
		string text = menuSnapshot.Typing ?? menuSnapshot.Help;
		if (!string.IsNullOrEmpty(text))
		{
			int num5 = 0;
			string[] array = text.Split(new char[1] { '\n' });
			foreach (string text2 in array)
			{
				num5 += Math.Max(1, (text2.Length + 47) / 48);
			}
			float num6 = 0.014f + (float)num5 * 0.024f;
			g.DrawRectangle(new RectangleF(x, y, num, num6), th.Footer);
			g.DrawText(text, new RectangleF(x + num3, y + 0.004f, num - 2f * num3, num6 - 0.008f), (TextAlignment)16, (menuSnapshot.Typing == null) ? th.Title : th.Selected, fontSmall);
		}
	}

	public void DisposeFonts()
	{
		try
		{
			if (fontHeader != null)
			{
				fontHeader.Dispose();
			}
		}
		catch
		{
		}
		try
		{
			if (fontItem != null)
			{
				fontItem.Dispose();
			}
		}
		catch
		{
		}
		try
		{
			if (fontSmall != null)
			{
				fontSmall.Dispose();
			}
		}
		catch
		{
		}
		fontHeader = (fontItem = (fontSmall = null));
	}

	static Menu()
	{
		WatchedKeys = new Keys[] { Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Enter, Keys.Back, Keys.Space, Keys.OemMinus,
			Keys.A, Keys.B, Keys.C, Keys.D, Keys.E, Keys.F, Keys.G, Keys.H, Keys.I, Keys.J, Keys.K, Keys.L, Keys.M,
			Keys.N, Keys.O, Keys.P, Keys.Q, Keys.R, Keys.S, Keys.T, Keys.U, Keys.V, Keys.W, Keys.X, Keys.Y, Keys.Z,
			Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9 };
	}
}
