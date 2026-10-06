using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace KickChaos
{
    /// <summary>Una opcion del menu.</summary>
    public class MenuItem
    {
        public string Label = "";
        public Func<string> Value;          // texto a la derecha (null = nada)
        public Action OnEnter;              // ENTER
        public Action<int> OnChange;        // izquierda/derecha (-1 / +1; con SHIFT llega -10 / +10)
        public Func<MenuPage> Submenu;      // ENTER abre otra pagina
        public Func<string> Help;           // descripcion abajo del menu
        public bool Info;                   // solo informativo (no se puede elegir)
        public Func<string> DynamicLabel;   // etiqueta que cambia

        public string GetLabel() { return DynamicLabel != null ? DynamicLabel() : Label; }
    }

    /// <summary>Una pagina (submenu). Build arma las opciones; se vuelve a llamar con Refresh().</summary>
    public class MenuPage
    {
        public string Title;
        public Func<List<MenuItem>> Build;
        public List<MenuItem> Items = new List<MenuItem>();
        public int Selected;
        public int Scroll;
        public string Search = "";

        public MenuPage(string title, Func<List<MenuItem>> build) { Title = title; Build = build; }

        public void Refresh()
        {
            Items = Build() ?? new List<MenuItem>();
            if (!string.IsNullOrWhiteSpace(Search))
                Items.RemoveAll(i => !StreamHudPolicy.Matches(i.GetLabel(), Search));
            if (Selected >= Items.Count) Selected = Math.Max(0, Items.Count - 1);
            Scroll = Math.Max(0, Math.Min(Scroll, Math.Max(0, Items.Count - Menu.VisibleRows)));
            if (Items.Count > 0 && Items[Selected].Info) MoveToSelectable(1);
        }

        public void MoveToSelectable(int dir)
        {
            if (Items.Count == 0) return;
            for (int i = 0; i < Items.Count; i++)
            {
                if (!Items[Selected].Info) return;
                Selected = (Selected + dir + Items.Count) % Items.Count;
            }
        }
    }

    /// <summary>Lo que se dibuja (se arma en el Tick y se dibuja en el hilo de dibujo).</summary>
    public class MenuSnapshot
    {
        public string Header, Title, Footer, Help;
        public List<string[]> Rows = new List<string[]>(); // [label, value, flags] flags: "s"=seleccionado, "i"=info
        public bool MoreAbove, MoreBelow;
        public string Typing; // != null: escribiendo texto
    }

    /// <summary>Colores del menu (por defecto, el tema "Grand Theft Auto IV" de Liberty's Legacy).</summary>
    public class MenuTheme
    {
        public Color Title = Color.FromArgb(255, 140, 140, 140);
        public Color Text = Color.FromArgb(255, 140, 140, 140);
        public Color Selected = Color.FromArgb(255, 240, 160, 0);
        public Color SelectedBack = Color.FromArgb(200, 62, 62, 62);
        public Color Background = Color.FromArgb(180, 0, 0, 0);
        public Color Footer = Color.FromArgb(255, 0, 0, 0);
        public float X = 0.02f, Y = 0.05f;

        static Color Read(IniFile ini, string name, Color def)
        {
            int r = ini.GetInt("menu_colors", name + " Red", def.R);
            int g = ini.GetInt("menu_colors", name + " Green", def.G);
            int b = ini.GetInt("menu_colors", name + " Blue", def.B);
            int a = ini.GetInt("menu_colors", name + " Alpha", def.A);
            Func<int, int> c = v => Math.Max(0, Math.Min(255, v));
            return Color.FromArgb(c(a), c(r), c(g), c(b));
        }

        /// <summary>Usa los mismos colores que Liberty's Legacy si esta instalado.</summary>
        public static MenuTheme FromLibertysLegacy(string gameFolder)
        {
            var t = new MenuTheme();
            try
            {
                string path = Path.Combine(Path.Combine(gameFolder, "Liberty's Legacy"), "Liberty's Legacy.ini");
                if (!File.Exists(path)) return t;
                var ini = IniFile.Load(path);
                t.Title = Read(ini, "Title", t.Title);
                t.Text = Read(ini, "Text", t.Text);
                t.Selected = Read(ini, "On Select", t.Selected);
                t.SelectedBack = Read(ini, "On Back", t.SelectedBack);
                t.Background = Read(ini, "Background", t.Background);
                t.Footer = Read(ini, "Footer", t.Footer);
            }
            catch { }
            return t;
        }
    }

    /// <summary>
    /// Menu con flechas: arriba/abajo elegir, izquierda/derecha cambiar valor, ENTER aceptar,
    /// RETROCESO volver. Las teclas se leen en el Tick (con repeticion al mantener apretado).
    /// </summary>
    public class Menu
    {
        public bool IsOpen { get; private set; }
        /// <summary>Ancho / alto de la pantalla (para calcular cuanto texto entra en una fila).</summary>
        public static float Aspect { get { return MenuText.Aspect; } set { MenuText.Aspect = value; } }
        public string Header = "KICKCHAOS IV";
        public const int VisibleRows = 13;

        readonly Stack<MenuPage> stack = new Stack<MenuPage>();
        readonly Func<MenuPage> rootFactory;
        volatile MenuSnapshot snapshot;
        readonly Dictionary<Keys, double> nextRepeat = new Dictionary<Keys, double>();
        readonly HashSet<Keys> down = new HashSet<Keys>();
        string toast;
        double toastUntil;

        // escribir texto
        string typingPrompt, typingBuffer;
        Action<string> typingDone;
        bool typingAllowEmpty;

        public Menu(Func<MenuPage> rootFactory) { this.rootFactory = rootFactory; }

        public MenuSnapshot Snapshot { get { return snapshot; } }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            stack.Clear();
            var root = rootFactory();
            root.Refresh();
            stack.Push(root);
            IsOpen = true;
            down.Clear();
            // las teclas que ya estaban apretadas no cuentan
            double now = G.Now;
            foreach (Keys k in WatchedKeys) if (G.KeyDown(k)) { down.Add(k); nextRepeat[k] = now + 0.35; }
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
            if (stack.Count > 1) { stack.Pop(); stack.Peek().Refresh(); }
            else Close();
        }

        public void RefreshCurrent() { if (stack.Count > 0) stack.Peek().Refresh(); }

        public void Toast(string text) { toast = text; toastUntil = G.Now + 3.0; }

        public void StartTyping(string prompt, string initial, Action<string> done, bool allowEmpty = false)
        {
            typingPrompt = prompt;
            typingBuffer = initial ?? "";
            typingDone = done;
            typingAllowEmpty = allowEmpty;
        }

        static readonly Keys[] WatchedKeys =
        {
            Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Return, Keys.Back, Keys.Space, Keys.OemMinus, Keys.Escape,
            Keys.A, Keys.B, Keys.C, Keys.D, Keys.E, Keys.F, Keys.G, Keys.H, Keys.I, Keys.J, Keys.K, Keys.L, Keys.M,
            Keys.N, Keys.O, Keys.P, Keys.Q, Keys.R, Keys.S, Keys.T, Keys.U, Keys.V, Keys.W, Keys.X, Keys.Y, Keys.Z,
            Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9
        };

        /// <summary>Tecla recien apretada (o repitiendose si se mantiene).</summary>
        bool Pressed(Keys k, double now, bool repeat)
        {
            bool isDown = G.KeyDown(k);
            if (!isDown) { down.Remove(k); return false; }
            if (!down.Contains(k))
            {
                down.Add(k);
                nextRepeat[k] = now + 0.35;
                return true;
            }
            double t;
            if (repeat && nextRepeat.TryGetValue(k, out t) && now >= t)
            {
                nextRepeat[k] = now + 0.06;
                return true;
            }
            return false;
        }

        public void Update(bool focused)
        {
            if (!IsOpen) return;
            double now = G.Now;
            if (focused)
            {
                if (typingDone != null) UpdateTyping(now);
                else UpdateNavigation(now);
            }
            if (IsOpen) BuildSnapshot(now);
        }

        void UpdateTyping(double now)
        {
            bool shift = G.KeyDown(Keys.ShiftKey);
            if (Pressed(Keys.Escape, now, false)) { typingDone = null; return; }
            if (Pressed(Keys.Return, now, false))
            {
                var done = typingDone;
                typingDone = null;
                string text = typingBuffer.Trim();
                if (text.Length > 0 || typingAllowEmpty) done(text);
                RefreshCurrent();
                return;
            }
            if (Pressed(Keys.Back, now, true))
            {
                if (typingBuffer.Length == 0) { typingDone = null; return; } // cancelar
                typingBuffer = typingBuffer.Substring(0, typingBuffer.Length - 1);
            }
            if (typingBuffer.Length >= 30) return;
            for (Keys k = Keys.A; k <= Keys.Z; k++)
                if (Pressed(k, now, true)) typingBuffer += ((char)('a' + (k - Keys.A))).ToString();
            for (Keys k = Keys.D0; k <= Keys.D9; k++)
                if (Pressed(k, now, true))
                    typingBuffer += (shift && k == Keys.D1) ? "!" : ((char)('0' + (k - Keys.D0))).ToString();
            if (Pressed(Keys.Space, now, true)) typingBuffer += " ";
            if (Pressed(Keys.OemMinus, now, true)) typingBuffer += "_";
        }

        void UpdateNavigation(double now)
        {
            MenuPage page = stack.Peek();
            bool searchPressed = Pressed(Keys.F, now, false);
            if (G.KeyDown(Keys.ControlKey) && searchPressed)
            {
                StartTyping("Filtrar esta pagina (vacio quita filtro)", page.Search, query =>
                {
                    page.Search = query; page.Selected = page.Scroll = 0; page.Refresh();
                }, true);
                return;
            }
            int n = page.Items.Count;
            if (Pressed(Keys.Up, now, true) && n > 0)
            {
                page.Selected = (page.Selected - 1 + n) % n;
                page.MoveToSelectable(-1);
            }
            if (Pressed(Keys.Down, now, true) && n > 0)
            {
                page.Selected = (page.Selected + 1) % n;
                page.MoveToSelectable(1);
            }
            MenuItem it = n > 0 ? page.Items[page.Selected] : null;
            int step = G.KeyDown(Keys.ShiftKey) ? 10 : 1;
            if (it != null && !it.Info && it.OnChange != null)
            {
                if (Pressed(Keys.Left, now, true)) { it.OnChange(-step); page.Refresh(); }
                if (Pressed(Keys.Right, now, true)) { it.OnChange(step); page.Refresh(); }
            }
            else { Pressed(Keys.Left, now, false); Pressed(Keys.Right, now, false); }

            if (Pressed(Keys.Return, now, false) && it != null && !it.Info)
            {
                if (it.Submenu != null) Push(it.Submenu());
                else if (it.OnEnter != null) { it.OnEnter(); if (IsOpen && stack.Count > 0) stack.Peek().Refresh(); }
                else if (it.OnChange != null) { it.OnChange(1); page.Refresh(); }
            }
            if (Pressed(Keys.Back, now, false)) Back();

            if (stack.Count > 0)
            {
                page = stack.Peek();
                if (page.Selected < page.Scroll) page.Scroll = page.Selected;
                if (page.Selected >= page.Scroll + VisibleRows) page.Scroll = page.Selected - VisibleRows + 1;
            }
        }

        void BuildSnapshot(double now)
        {
            if (stack.Count == 0) { snapshot = null; return; }
            MenuPage page = stack.Peek();
            var s = new MenuSnapshot { Header = Header, Title = page.Title + (string.IsNullOrWhiteSpace(page.Search) ? "" : " / " + page.Search) };
            int end = Math.Min(page.Items.Count, page.Scroll + VisibleRows);
            for (int i = page.Scroll; i < end; i++)
            {
                MenuItem it = page.Items[i];
                string val = "";
                try { val = it.Value != null ? it.Value() : (it.Submenu != null ? ">" : ""); } catch { val = "?"; }
                bool sel = i == page.Selected;
                if (sel && it.OnChange != null && !it.Info) val = "<  " + val + "  >";
                string lbl;
                try { lbl = it.GetLabel(); } catch { lbl = "?"; }
                s.Rows.Add(new[] { lbl, val, (sel ? "s" : "") + (it.Info ? "i" : "") });
            }
            s.MoreAbove = page.Scroll > 0;
            s.MoreBelow = end < page.Items.Count;
            int selectable = 0, pos = 0;
            for (int i = 0; i < page.Items.Count; i++)
                if (!page.Items[i].Info) { selectable++; if (i <= page.Selected) pos = selectable; }
            s.Footer = selectable > 0 ? pos + " / " + selectable : "";
            MenuItem cur = page.Items.Count > 0 ? page.Items[page.Selected] : null;
            string help = null;
            try { help = cur != null && cur.Help != null ? cur.Help() : null; } catch { }
            if (now < toastUntil) help = toast;
            s.Help = help ?? "Flechas / ENTER / RETROCESO   CTRL+F: buscar en esta pagina";
            if (typingDone != null)
                s.Typing = typingPrompt + "\n> " + typingBuffer + "_\n(ENTER confirma, RETROCESO borra / cancela)";
            snapshot = s;
        }

        // ------------------------------------------------------------------
        // Dibujo (ScriptHookDotNet: evento PerFrameDrawing)
        // ------------------------------------------------------------------
        GTA.Font fontHeader, fontItem, fontSmall;

        public void Draw(GTA.Graphics g, MenuTheme th)
        {
            MenuSnapshot s = snapshot;
            if (s == null) return;
            g.Scaling = GTA.FontScaling.ScreenUnits;
            if (fontHeader == null)
            {
                fontHeader = new GTA.Font("Arial", 0.040f, GTA.FontScaling.ScreenUnits, true, false);
                fontItem = new GTA.Font("Arial", 0.026f, GTA.FontScaling.ScreenUnits, false, false);
                fontSmall = new GTA.Font("Arial", 0.022f, GTA.FontScaling.ScreenUnits, false, false);
            }

            float x = th.X, w = 0.31f, y = th.Y;
            float rowH = 0.034f, pad = 0.008f;

            // encabezado
            g.DrawRectangle(new RectangleF(x, y, w, 0.065f), th.Footer);
            g.DrawText(s.Header, new RectangleF(x, y, w, 0.065f), GTA.TextAlignment.Center | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine, th.Selected, fontHeader);
            y += 0.065f;
            // titulo de la pagina
            g.DrawRectangle(new RectangleF(x, y, w, rowH), th.Footer);
            g.DrawText(s.Title.ToUpperInvariant(), new RectangleF(x + pad, y, w - 2 * pad, rowH), GTA.TextAlignment.Left | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine, th.Title, fontItem);
            g.DrawText(s.Footer, new RectangleF(x + pad, y, w - 2 * pad, rowH), GTA.TextAlignment.Right | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine, th.Title, fontItem);
            y += rowH;

            // opciones
            int rows = Math.Max(1, s.Rows.Count);
            g.DrawRectangle(new RectangleF(x, y, w, rowH * rows), th.Background);
            if (s.MoreAbove) g.DrawText("^", new RectangleF(x, y - 0.012f, w, 0.012f), GTA.TextAlignment.Center, th.Text, fontSmall);
            foreach (string[] r in s.Rows)
            {
                bool sel = r[2].Contains("s");
                bool info = r[2].Contains("i");
                Color col = sel ? th.Selected : (info ? Color.FromArgb(th.Text.A, Math.Min(255, th.Text.R + 60), Math.Min(255, th.Text.G + 60), Math.Min(255, th.Text.B + 60)) : th.Text);
                if (sel) g.DrawRectangle(new RectangleF(x, y, w, rowH), th.SelectedBack);
                var area = new RectangleF(x + pad, y, w - 2 * pad, rowH);
                string lbl = r[0], val = r[1];
                MenuText.FitRow(ref lbl, ref val, w - 2 * pad);
                g.DrawText(lbl, area, GTA.TextAlignment.Left | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine, col, fontItem);
                if (val.Length > 0)
                    g.DrawText(val, area, GTA.TextAlignment.Right | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine, col, fontItem);
                y += rowH;
            }
            if (s.MoreBelow) g.DrawText("v", new RectangleF(x, y, w, 0.014f), GTA.TextAlignment.Center, th.Text, fontSmall);
            y += 0.016f;

            // ayuda / descripcion
            string help = s.Typing ?? s.Help;
            if (!string.IsNullOrEmpty(help))
            {
                int lines = 0;
                foreach (string part in help.Split('\n')) lines += Math.Max(1, (part.Length + 47) / 48);
                float hh = 0.014f + lines * 0.024f;
                g.DrawRectangle(new RectangleF(x, y, w, hh), th.Footer);
                g.DrawText(help, new RectangleF(x + pad, y + 0.004f, w - 2 * pad, hh - 0.008f), GTA.TextAlignment.Left | GTA.TextAlignment.WordBreak,
                    s.Typing != null ? th.Selected : th.Title, fontSmall);
            }
        }

        public void DisposeFonts()
        {
            try { if (fontHeader != null) fontHeader.Dispose(); } catch { }
            try { if (fontItem != null) fontItem.Dispose(); } catch { }
            try { if (fontSmall != null) fontSmall.Dispose(); } catch { }
            fontHeader = fontItem = fontSmall = null;
        }
    }
}
