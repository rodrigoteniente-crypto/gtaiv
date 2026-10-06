using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KickChaos.Panel
{
    public sealed class WorldMap : Control
    {
        public StreamSnapshot Snapshot;
        public int SelectedId;
        public float Radius = 1000;
        public event Action<int> NpcClicked;
        readonly Dictionary<int, PointF> points = new Dictionary<int, PointF>();
        readonly Dictionary<int, float> previousDistances = new Dictionary<int, float>();
        readonly HashSet<int> approaching = new HashSet<int>();
        string stamp = "";
        public WorldMap() { DoubleBuffered = true; BackColor = Color.FromArgb(23, 30, 35); ForeColor = Color.Gainsboro; }
        public static Color KindColor(string kind) => kind == "SUB" ? Color.FromArgb(25, 180, 95) : kind == "FOLLOW" ? Color.FromArgb(35, 145, 235) : Color.FromArgb(195, 130, 230);
        PointF Project(float x, float y)
        {
            float scale = Math.Max(1, Math.Min(Width, Height) - 36) / (2f * Radius);
            return new PointF(Width / 2f + (x - Snapshot.CameraX) * scale, Height / 2f - (y - Snapshot.CameraY) * scale);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; points.Clear();
            using (var pen = new Pen(Color.FromArgb(45, 55, 64)))
                for (int n = 1; n < 8; n++) { g.DrawLine(pen, Width * n / 8f, 0, Width * n / 8f, Height); g.DrawLine(pen, 0, Height * n / 8f, Width, Height * n / 8f); }
            using (var brush = new SolidBrush(ForeColor)) g.DrawString("N ↑    SUB ●   FOLLOW ●   DUPLICADO ●", Font, brush, 8, 6);
            if (Snapshot == null) { using (var brush = new SolidBrush(ForeColor)) g.DrawString("Esperando posiciones del juego", Font, brush, 12, Height / 2f); return; }
            var center = Project(Snapshot.CameraX, Snapshot.CameraY);
            float coneDistance = Radius * 0.55f;
            // GTA heading 0 points north, positive headings rotate toward west.
            float horizontalFov = (float)(2 * Math.Atan(Math.Tan(Math.Max(5, Math.Min(120, Snapshot.CameraFov)) * Math.PI / 360) * 16 / 9) * 180 / Math.PI);
            PointF[] cone = { center, ConeEnd(Snapshot.CameraHeading - horizontalFov / 2, coneDistance), ConeEnd(Snapshot.CameraHeading + horizontalFov / 2, coneDistance) };
            using (var fill = new SolidBrush(Color.FromArgb(45, Color.Gold))) g.FillPolygon(fill, cone);
            using (var line = new Pen(Color.Goldenrod, 1.5f)) g.DrawPolygon(line, cone);
            using (var brush = new SolidBrush(Color.Gold)) g.FillRectangle(brush, center.X - 4, center.Y - 4, 8, 8);
            bool changed = stamp != Snapshot.UpdatedUtc;
            if (changed) { approaching.Clear(); stamp = Snapshot.UpdatedUtc; }
            foreach (var marker in Snapshot.Markers)
            {
                PointF p = Project(marker.X, marker.Y);
                using (var pen = new Pen(Color.Orange, 2)) g.DrawRectangle(pen, p.X - 5, p.Y - 5, 10, 10);
                using (var brush = new SolidBrush(Color.Orange)) g.DrawString(marker.Kind, Font, brush, p.X + 6, p.Y - 6);
            }
            foreach (StreamNpcRow npc in Snapshot.Npcs)
            {
                float dx = npc.X - Snapshot.CameraX, dy = npc.Y - Snapshot.CameraY, distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float heading = (float)(Math.Atan2(-dx, dy) * 180 / Math.PI);
                float diff = Math.Abs(((heading - Snapshot.CameraHeading) % 360 + 540) % 360 - 180);
                if (changed)
                {
                    if (previousDistances.TryGetValue(npc.Id, out float old) && old - distance > 0.5 && diff < horizontalFov / 2 + 20 && distance < Radius) approaching.Add(npc.Id);
                    previousDistances[npc.Id] = distance;
                }
                PointF p = Project(npc.X, npc.Y);
                if (p.X < 0 || p.Y < 24 || p.X > Width || p.Y > Height - 28) continue;
                points[npc.Id] = p;
                Color color = KindColor(npc.Kind);
                using (var brush = new SolidBrush(color)) { g.FillEllipse(brush, p.X - 4, p.Y - 4, 8, 8); g.DrawString(npc.Name, Font, brush, p.X + 6, p.Y - 7); }
                if (npc.Id == SelectedId) using (var pen = new Pen(Color.White, 2)) g.DrawEllipse(pen, p.X - 7, p.Y - 7, 14, 14);
                if (approaching.Contains(npc.Id)) using (var pen = new Pen(Color.Gold, 2)) g.DrawEllipse(pen, p.X - 10, p.Y - 10, 20, 20);
            }
            if (changed)
            {
                var live = new HashSet<int>(); foreach (var npc in Snapshot.Npcs) live.Add(npc.Id);
                foreach (int id in new List<int>(previousDistances.Keys)) if (!live.Contains(id)) previousDistances.Remove(id);
            }
            using (var brush = new SolidBrush(ForeColor)) g.DrawString("Radio " + Radius.ToString("0") + "m · aro amarillo: se aproxima\nMapa de posiciones; centro = cámara actual", Font, brush, 8, Height - 35);
        }
        PointF ConeEnd(float heading, float distance)
        { double a = heading * Math.PI / 180; return Project(Snapshot.CameraX - (float)Math.Sin(a) * distance, Snapshot.CameraY + (float)Math.Cos(a) * distance); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            foreach (var pair in points)
                if (Math.Abs(pair.Value.X - e.X) < 14 && Math.Abs(pair.Value.Y - e.Y) < 14) { NpcClicked?.Invoke(pair.Key); break; }
        }
    }
}
