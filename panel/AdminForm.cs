using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace KickChaos.Panel
{
    public sealed class AdminForm : Form
    {
        readonly TextBox folder = new TextBox(), search = new TextBox(), detail = new TextBox();
        readonly Label status = new Label(), camera = new Label();
        readonly CheckBox clickObserve = new CheckBox { Text = "Click en NPC: observar", Checked = true, AutoSize = true };
        readonly CheckBox showMap = new CheckBox { Text = "Minimapa / debug", Checked = true, AutoSize = true };
        readonly DataGridView grid = new DataGridView();
        readonly ComboBox build = new ComboBox(), behavior = new ComboBox(), zone = new ComboBox();
        readonly NumericUpDown seconds = new NumericUpDown { Minimum = 1, Maximum = 600, Value = 15, Width = 58 };
        readonly TextBox x = new TextBox { Width = 68 }, y = new TextBox { Width = 68 }, z = new TextBox { Width = 68 };
        readonly ListBox feed = new ListBox { Dock = DockStyle.Fill };
        readonly WorldMap map = new WorldMap { Dock = DockStyle.Fill };
        readonly Dictionary<string, DateTime> pending = new Dictionary<string, DateTime>();
        readonly Timer poll = new Timer { Interval = 750 };
        StreamSnapshot snapshot;
        string lastSnapshot = "";
        int selectedId;
        bool refreshing, snapshotAvailable;
        string PanelFolder => Path.Combine(folder.Text.Trim(), "scripts", "KickChaos", "panel");

        public AdminForm()
        {
            Text = "KickChaos · Administración de stream 1.9.1";
            Size = new Size(1320, 860); MinimumSize = new Size(960, 620);
            Font = new Font("Segoe UI", 9f);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(10) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            Controls.Add(layout);
            var location = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            location.Controls.Add(new Label { Text = "Carpeta de GTA IV", AutoSize = true, Margin = new Padding(0, 7, 8, 0) });
            folder.Width = 620; folder.Text = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            location.Controls.Add(folder); location.Controls.Add(Button("Elegir…", ChooseFolder));
            location.Controls.Add(Button("Abrir config.ini", OpenConfiguration));
            layout.Controls.Add(location, 0, 0);
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            top.Controls.Add(new Label { Text = "Buscar", AutoSize = true, Margin = new Padding(0, 7, 8, 0) });
            search.Width = 215; top.Controls.Add(search); search.TextChanged += (sender, args) => Populate();
            top.Controls.Add(clickObserve); top.Controls.Add(showMap);
            top.Controls.Add(Button("Volver al loop", () => Send("loop", false)));
            top.Controls.Add(Button("Próxima cámara", () => Send("next", false)));
            camera.AutoSize = true; camera.Margin = new Padding(10, 7, 0, 0); top.Controls.Add(camera);
            layout.Controls.Add(top, 0, 1);
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 850 };
            grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.RowHeadersVisible = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (string name in new[] { "Nombre", "Tipo", "Vida", "Kills", "Personajes", "Estado", "Vehículo", "Objetivo", "Build / mejoras" }) grid.Columns.Add(name, name);
            grid.Columns[0].FillWeight = 100; grid.Columns[1].FillWeight = 65; grid.Columns[2].FillWeight = 50;
            grid.Columns[3].FillWeight = 45; grid.Columns[4].FillWeight = 60; grid.Columns[8].FillWeight = 140;
            grid.CellClick += (sender, args) =>
            {
                if (refreshing || args.RowIndex < 0) return;
                selectedId = (int)grid.Rows[args.RowIndex].Tag;
                SelectedInfo();
                if (clickObserve.Checked) Send("observe", true);
            };
            grid.SelectionChanged += (sender, args) => { if (!refreshing && grid.SelectedRows.Count > 0) { selectedId = (int)grid.SelectedRows[0].Tag; SelectedInfo(); } };
            split.Panel1.Controls.Add(grid);
            var mapLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            mapLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            mapLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            mapLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            var scale = new FlowLayoutPanel { Dock = DockStyle.Fill };
            scale.Controls.Add(new Label { Text = "Radio del mapa (m)", AutoSize = true, Margin = new Padding(0, 5, 0, 0) });
            var radius = new NumericUpDown { Minimum = 150, Maximum = 5000, Value = 1000, Increment = 100, Width = 72 };
            radius.ValueChanged += (sender, args) => { map.Radius = (float)radius.Value; map.Invalidate(); }; scale.Controls.Add(radius);
            mapLayout.Controls.Add(scale, 0, 0); mapLayout.Controls.Add(map, 0, 1); mapLayout.Controls.Add(feed, 0, 2);
            split.Panel2.Controls.Add(mapLayout); layout.Controls.Add(split, 0, 2);
            showMap.CheckedChanged += (sender, args) => split.Panel2Collapsed = !showMap.Checked;
            map.NpcClicked += id => { selectedId = id; Populate(); Send("observe", true); };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            actions.Controls.Add(Button("Observar", () => Send("observe", true)));
            actions.Controls.Add(new Label { Text = "seg", AutoSize = true, Margin = new Padding(3, 7, 0, 0) }); actions.Controls.Add(seconds);
            actions.Controls.Add(Button("Fijar cámara", () => Send("pin", true)));
            actions.Controls.Add(Button("Destrabar / reubicar", () => Send("relocate", true)));
            actions.Controls.Add(Button("Debug", () => Send("debug", true)));
            FillCombo(build, "SUPERVIVIENTE", "CAZADOR", "AGRESIVO"); actions.Controls.Add(build);
            actions.Controls.Add(Button("Aplicar build", () => Send("build", true, build.Text)));
            FillCombo(behavior, "Pasear", "Batalla"); actions.Controls.Add(behavior);
            actions.Controls.Add(Button("Comportamiento", () => Send("behavior", true, behavior.Text)));
            actions.Controls.Add(Button("Matar", () => Destructive("kill", "matar")));
            actions.Controls.Add(Button("Eliminar", () => Destructive("remove", "eliminar")));
            layout.Controls.Add(actions, 0, 3);
            var teleport = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            teleport.Controls.Add(new Label { Text = "Reubicar en", AutoSize = true, Margin = new Padding(0, 7, 5, 0) });
            FillCombo(zone, "cámara", "Broker", "Algonquin", "Alderney", "Bohan", "Coordenadas"); teleport.Controls.Add(zone);
            foreach (var pair in new[] { Tuple.Create("X", x), Tuple.Create("Y", y), Tuple.Create("Z", z) })
            { teleport.Controls.Add(new Label { Text = pair.Item1, AutoSize = true, Margin = new Padding(7, 7, 0, 0) }); teleport.Controls.Add(pair.Item2); }
            teleport.Controls.Add(Button("Teletransportar", Teleport));
            status.AutoSize = true; status.Margin = new Padding(12, 7, 0, 0); teleport.Controls.Add(status);
            layout.Controls.Add(teleport, 0, 4);
            detail.Dock = DockStyle.Fill; detail.Multiline = true; detail.ReadOnly = true; detail.ScrollBars = ScrollBars.Vertical;
            layout.Controls.Add(detail, 0, 5);
            poll.Tick += (sender, args) => Poll(); poll.Start();
            FormClosed += (sender, args) => poll.Dispose();
            folder.TextChanged += (sender, args) => { snapshot = null; snapshotAvailable = false; lastSnapshot = ""; pending.Clear(); selectedId = 0; Populate(); };
            Resize += (sender, args) => folder.Width = Math.Max(260, ClientSize.Width - 400);
            status.Text = "Esperando al mod…";
        }
        static Button Button(string text, Action click)
        { var button = new Button { Text = text, AutoSize = true, Height = 29 }; button.Click += (sender, args) => click(); return button; }
        static void FillCombo(ComboBox box, params string[] items)
        { box.DropDownStyle = ComboBoxStyle.DropDownList; box.Width = 130; box.Items.AddRange(items); box.SelectedIndex = 0; }
        void ChooseFolder()
        {
            using (var browser = new FolderBrowserDialog { Description = "Elegí la carpeta que contiene GTAIV.exe", SelectedPath = folder.Text })
                if (browser.ShowDialog(this) == DialogResult.OK) folder.Text = browser.SelectedPath;
        }
        void OpenConfiguration()
        {
            string path = Path.Combine(folder.Text.Trim(), "scripts", "KickChaos", "config.ini");
            try { if (!File.Exists(path)) throw new IOException("No se encontró config.ini"); Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = false }); }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        void Poll()
        {
            try
            {
                string path = Path.Combine(PanelFolder, "snapshot.json");
                if (!File.Exists(path)) { snapshotAvailable = false; status.Text = "Esperando al mod en la carpeta elegida…"; return; }
                string text = StreamWire.ReadBounded(path, StreamWire.MaxSnapshotBytes);
                if (text != lastSnapshot)
                {
                    if (!StreamWire.TrySnapshot(text, out StreamSnapshot next))
                    { snapshotAvailable = false; status.Text = "Esperando un snapshot válido del mod…"; return; }
                    if (snapshot == null || snapshot.SessionId != next.SessionId)
                    {
                        selectedId = 0; pending.Clear();
                        detail.Text = "Sesión de juego actualizada. Seleccioná un NPC para administrarlo.";
                    }
                    lastSnapshot = text; snapshot = next; Populate();
                }
                snapshotAvailable = true;
                if (!StreamWire.SnapshotFresh(snapshot, DateTime.UtcNow))
                { status.Text = "Sin actualización del juego (cerrado, pausado o mod detenido)"; return; }
                status.Text = "Conectado · " + snapshot.Npcs.Count + " NPCs · " + snapshot.KickStatus;
                foreach (string id in pending.Keys.ToArray())
                {
                    string ack = Path.Combine(PanelFolder, "acks", id + ".json");
                    if (File.Exists(ack))
                    {
                        var value = Json.ParseObject(StreamWire.ReadBounded(ack, 8192));
                        if (value != null) { detail.Text = Json.GetString(value, "Message") ?? ""; pending.Remove(id); }
                    }
                    else if (DateTime.UtcNow - pending[id] > TimeSpan.FromSeconds(12))
                    { detail.Text = "El juego todavía no respondió al comando. Puede estar pausado."; pending.Remove(id); }
                }
            }
            catch (IOException) { snapshotAvailable = false; status.Text = "Esperando acceso al archivo del mod…"; }
            catch (Exception ex) { snapshotAvailable = false; status.Text = "Panel: " + ex.Message; }
        }
        void Populate()
        {
            refreshing = true;
            try
            {
                grid.Rows.Clear();
                if (snapshot == null) { map.Snapshot = null; map.Invalidate(); return; }
                string query = search.Text.Trim();
                foreach (StreamNpcRow npc in snapshot.Npcs)
                {
                    if (query.Length > 0 && (npc.Name + " " + npc.Owner + " " + npc.Kind + " " + npc.State + " " + npc.Objective + " " + npc.Build)
                        .IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int index = grid.Rows.Add(npc.Name, npc.Kind, npc.Health.ToString("0") + "%", npc.Kills, npc.Characters,
                        npc.State, npc.InCar ? (npc.VehicleName.Length > 0 ? npc.VehicleName : "Auto " + npc.Vehicle) : "A pie", npc.Objective,
                        npc.Build + (npc.Perks.Length > 0 ? " · " + npc.Perks : ""));
                    var row = grid.Rows[index]; row.Tag = npc.Id;
                    row.Cells[1].Style.ForeColor = WorldMap.KindColor(npc.Kind);
                    if (npc.Health < 30) row.Cells[2].Style.ForeColor = Color.Firebrick;
                    if (npc.Id == selectedId) row.Selected = true;
                }
                if (selectedId == 0) grid.ClearSelection();
                map.Snapshot = snapshot; map.SelectedId = selectedId; map.Invalidate();
                camera.Text = "📷 " + snapshot.CameraName + " · ciudad " + snapshot.CityRemainingSeconds.ToString("0") + "s";
                feed.Items.Clear();
                foreach (var item in snapshot.Feed.AsEnumerable().Reverse()) feed.Items.Add(item.Kind + " · " + item.User + " · " + item.Text);
            }
            finally { refreshing = false; }
        }
        StreamNpcRow Selected() => snapshot?.Npcs.Find(npc => npc.Id == selectedId);
        void SelectedInfo()
        {
            var npc = Selected(); if (npc == null) return;
            map.SelectedId = npc.Id; map.Invalidate();
            x.Text = npc.X.ToString("0.0", CultureInfo.InvariantCulture); y.Text = npc.Y.ToString("0.0", CultureInfo.InvariantCulture); z.Text = npc.Z.ToString("0.0", CultureInfo.InvariantCulture);
            detail.Text = npc.Name + " · dueño " + npc.Owner + " · ID " + npc.Id + " · " + npc.State + " · " + npc.Objective + Environment.NewLine +
                "Vida " + npc.Health.ToString("0") + "% · kills " + npc.Kills + " · " + npc.Build + " · " + npc.Perks +
                " · !npc " + (npc.CameraReadyIn > 0 ? "disponible en " + npc.CameraReadyIn.ToString("0") + "s" : "disponible");
        }
        bool Connected()
        {
            return snapshotAvailable && StreamWire.SnapshotFresh(snapshot, DateTime.UtcNow);
        }
        void Send(string action, bool requiresNpc, string value = "", string targetZone = "", float tx = 0, float ty = 0, float tz = 0)
        {
            if (!Connected()) { detail.Text = "Esperá a que el panel reciba datos del GTA IV."; return; }
            if (requiresNpc && Selected() == null) { detail.Text = "Seleccioná un NPC vivo."; return; }
            if (pending.Count >= 32) { detail.Text = "Hay demasiados comandos pendientes; esperá la respuesta del juego."; return; }
            var command = new StreamAdminCommand { SessionId = snapshot.SessionId, Id = Guid.NewGuid().ToString("D"), Action = action, NpcId = selectedId, Value = value,
                Zone = targetZone, X = tx, Y = ty, Z = tz, Seconds = (double)seconds.Value };
            try
            {
                StreamWire.WriteAtomic(Path.Combine(PanelFolder, "commands", command.Id + ".json"), StreamWire.Serialize(command));
                pending.Add(command.Id, DateTime.UtcNow); detail.Text = "Enviando " + action + "…";
            }
            catch (Exception ex) { detail.Text = "No se pudo enviar: " + ex.Message; }
        }
        void Teleport()
        {
            if (zone.Text != "Coordenadas") { Send("teleport", true, "", zone.Text); return; }
            if (!float.TryParse(x.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float tx) ||
                !float.TryParse(y.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float ty) ||
                !float.TryParse(z.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float tz) || !StreamWire.ValidPosition(tx, ty, tz))
            { detail.Text = "Usá coordenadas válidas con punto decimal."; return; }
            Send("teleport", true, "", "", tx, ty, tz);
        }
        void Destructive(string action, string verb)
        {
            var npc = Selected(); if (npc == null) { detail.Text = "Seleccioná un NPC vivo."; return; }
            string session = snapshot.SessionId;
            int confirmedId = npc.Id;
            if (MessageBox.Show(this, "¿Querés " + verb + " a " + npc.Name + "? Esta acción termina su vida actual.", "KickChaos", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // Poll continues during a modal dialog. Do not apply approval for an old NPC to a new session/selection.
                if (!Connected() || snapshot.SessionId != session || selectedId != confirmedId || Selected() == null)
                { detail.Text = "La sesión o el NPC cambió mientras confirmabas. Volvé a seleccionarlo."; return; }
                Send(action, true);
            }
        }
    }
}
