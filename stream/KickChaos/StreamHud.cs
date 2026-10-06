using System;
using System.Collections.Generic;
using System.Drawing;

namespace KickChaos
{
    // Drawn in game capture: OBS needs no extra browser source or network service.
    public static class StreamHud
    {
        static bool visible = true, feedVisible = true, mapVisible;
        static int maxRows = 6, feedRows = 3;
        static float x = 0.72f, y = 0.055f, width = 0.26f, range = 600, mapX = 0.025f, mapY = 0.72f, mapSize = 0.21f;
        static double spotlightSeconds = 8;
        static GTA.Font textFont, smallFont, titleFont;
        static readonly Color back = Color.FromArgb(175, 8, 12, 14);
        static readonly Color green = Color.FromArgb(255, 83, 252, 24);
        static readonly Color gray = Color.FromArgb(220, 205, 210, 215);
        const GTA.TextAlignment Align = GTA.TextAlignment.Left | GTA.TextAlignment.VerticalCenter | GTA.TextAlignment.SingleLine;
        public static bool Enabled { get { return visible; } }
        public static bool FeedVisible { get { return feedVisible; } }
        public static bool ToggleFeed() { feedVisible = !feedVisible; return feedVisible; }
        public static bool ToggleHud() { visible = !visible; return visible; }

        public static void ApplyConfig(Config cfg)
        {
            if (cfg == null || cfg.Ini == null) return;
            IniFile ini = cfg.Ini;
            visible = ini.GetBool("HUDStream", "Mostrar", true);
            feedVisible = ini.GetBool("HUDStream", "Feed", true);
            mapVisible = ini.GetBool("HUDStream", "Minimapa", false);
            maxRows = Math.Max(1, Math.Min(12, ini.GetInt("HUDStream", "MaxFilas", 6)));
            feedRows = Math.Max(1, Math.Min(5, ini.GetInt("HUDStream", "FilasFeed", 3)));
            width = StreamHudPolicy.Clamp(ini.GetFloat("HUDStream", "Ancho", 0.26f), 0.20f, 0.45f, 0.26f);
            x = StreamHudPolicy.Clamp(ini.GetFloat("HUDStream", "X", 0.72f), 0.01f, 0.99f - width, 0.72f);
            y = StreamHudPolicy.Clamp(ini.GetFloat("HUDStream", "Y", 0.055f), 0.01f, 0.30f, 0.055f);
            range = StreamHudPolicy.Clamp(ini.GetFloat("HUDStream", "RadioMinimapa", 600), 50, 5000, 600);
            mapSize = StreamHudPolicy.Clamp(ini.GetFloat("HUDStream", "TamanoMinimapa", 0.21f), 0.12f, 0.4f, 0.21f);
            mapX = StreamHudPolicy.Clamp(ini.GetFloat("HUDStream", "MinimapaX", 0.025f), 0.01f, 0.75f, 0.025f);
            mapY = StreamHudPolicy.Clamp(ini.GetFloat("HUDStream", "MinimapaY", 0.72f), 0.035f, 0.96f - mapSize, 0.72f);
            spotlightSeconds = StreamHudPolicy.Clamp(ini.GetFloat("HUDStream", "DestacadoSegundos", 8), 0, 30, 8);
        }
        static Color KindColor(string kind) { return kind == "SUB" ? green : kind == "DUPLICADO" ? Color.FromArgb(255, 187, 115, 255) : Color.FromArgb(255, 95, 190, 255); }
        static void Text(GTA.Graphics g, string value, float tx, float ty, float w, float h, Color color, GTA.Font font)
        {
            g.DrawText(StreamHudPolicy.Clean(value, Math.Max(8, (int)(w * Math.Max(0.7f, Menu.Aspect) / 0.009f))), new RectangleF(tx, ty, w, h), Align, color, font);
        }
        public static void Draw(GTA.Graphics g, float aspect)
        {
            StreamSnapshot s = StreamRuntime.Snapshot;
            if ((!visible && !mapVisible) || !StreamHudPolicy.Fresh(s, DateTime.UtcNow)) return;
            float ratio = StreamHudPolicy.Clamp(aspect, 0.7f, 4, 16f / 9f);
            g.Scaling = GTA.FontScaling.ScreenUnits;
            if (textFont == null)
            {
                titleFont = new GTA.Font("Arial", 0.023f, GTA.FontScaling.ScreenUnits, true, false);
                textFont = new GTA.Font("Arial", 0.021f, GTA.FontScaling.ScreenUnits, false, false);
                smallFont = new GTA.Font("Arial", 0.0175f, GTA.FontScaling.ScreenUnits, false, false);
            }
            double now = G.Now;
            List<StreamFeedEntry> entries = StreamRuntime.Feed(now);
            if (visible)
            {
                StreamNpcRow newest = StreamHudPolicy.Spotlight(s, entries, now, spotlightSeconds);
                List<StreamNpcRow> rows = StreamHudPolicy.Rows(s, StreamHudPolicy.Capacity(y, maxRows, feedVisible ? feedRows : 0, newest != null), now);
                float cy = y;
                g.DrawRectangle(new RectangleF(x, cy, width, 0.035f), back);
                g.DrawRectangle(new RectangleF(x, cy, width, 0.002f), green);
                Text(g, "KICK / " + (s.Npcs == null ? 0 : s.Npcs.Count) + " VIVOS", x + 0.006f, cy + 0.003f, width - 0.012f, 0.028f, Color.White, titleFont);
                cy += 0.035f;
                foreach (StreamNpcRow row in rows)
                {
                    g.DrawRectangle(new RectangleF(x, cy, width, 0.052f), back);
                    Color color = KindColor(row.Kind);
                    g.DrawRectangle(new RectangleF(x, cy + 0.006f, 0.0025f, 0.038f), color);
                    Text(g, StreamHudPolicy.Clean(row.Name, 24) + "  [" + StreamHudPolicy.Clean(row.Kind, 9) + "]", x + 0.006f, cy, width - 0.012f, 0.025f, color, textFont);
                    Text(g, StreamHudPolicy.Stats(row), x + 0.006f, cy + 0.024f, width - 0.012f, 0.023f, gray, smallFont);
                    float health = StreamHudPolicy.Clamp(row.Health, 0, 100) / 100;
                    g.DrawRectangle(new RectangleF(x + 0.006f, cy + 0.049f, (width - 0.012f) * health, 0.0015f), health < 0.30f ? Color.OrangeRed : color);
                    cy += 0.052f;
                }
                int hidden = (s.Npcs == null ? 0 : s.Npcs.Count) - rows.Count;
                if (hidden > 0)
                {
                    g.DrawRectangle(new RectangleF(x, cy, width, 0.024f), back);
                    Text(g, "+ " + hidden + " en el panel", x + 0.006f, cy, width - 0.012f, 0.024f, gray, smallFont);
                    cy += 0.024f;
                }
                if (newest != null)
                {
                    cy += 0.008f;
                    g.DrawRectangle(new RectangleF(x, cy, width, 0.052f), back);
                    Text(g, "NUEVO SUB: " + newest.Name, x + 0.006f, cy, width - 0.012f, 0.026f, green, textFont);
                    Text(g, StreamHudPolicy.Stats(newest), x + 0.006f, cy + 0.026f, width - 0.012f, 0.026f, gray, smallFont);
                    cy += 0.052f;
                }
                if (feedVisible)
                {
                    var visibleFeed = entries.FindAll(entry => entry.Kind != "spawnsub");
                    int start = Math.Max(0, visibleFeed.Count - feedRows);
                    cy += 0.008f;
                    for (int i = start; i < visibleFeed.Count && cy < 0.91f; i++)
                    {
                        StreamFeedEntry entry = visibleFeed[i];
                        g.DrawRectangle(new RectangleF(x, cy, width, 0.025f), back);
                        Text(g, StreamHudPolicy.FeedLabel(entry), x + 0.006f, cy, width - 0.012f, 0.025f, entry.Kind == "kill" ? Color.FromArgb(255, 255, 185, 75) : gray, smallFont);
                        cy += 0.025f;
                    }
                }
            }
            if (mapVisible) DrawMap(g, s, ratio);
        }
        static void DrawMap(GTA.Graphics g, StreamSnapshot s, float aspect)
        {
            float h = mapSize, w = h / aspect, mx = Math.Min(mapX, 0.98f - w), my = mapY;
            float cx = mx + w / 2, cy = my + h / 2, rx = w * 0.46f, ry = h * 0.46f;
            g.DrawRectangle(new RectangleF(mx, my, w, h), back);
            g.DrawLine(cx - rx, cy, cx + rx, cy, 0.0006f, Color.FromArgb(100, 130, 140, 145));
            g.DrawLine(cx, cy - ry, cx, cy + ry, 0.0006f, Color.FromArgb(100, 130, 140, 145));
            double heading = StreamHudPolicy.Clamp(s.CameraHeading, -100000, 100000) * Math.PI / 180;
            double half = StreamHudPolicy.Clamp(s.CameraFov, 5, 120, 45) * Math.PI / 360;
            float ax = cx - (float)Math.Sin(heading - half) * rx, ay = cy - (float)Math.Cos(heading - half) * ry;
            float bx = cx - (float)Math.Sin(heading + half) * rx, by = cy - (float)Math.Cos(heading + half) * ry;
            Color cone = Color.FromArgb(180, 235, 235, 235);
            g.DrawLine(cx, cy, ax, ay, 0.001f, cone); g.DrawLine(cx, cy, bx, by, 0.001f, cone); g.DrawLine(ax, ay, bx, by, 0.001f, cone);
            if (s.Markers != null)
                foreach (StreamMarker marker in s.Markers)
                {
                    float px, py;
                    if (marker == null || !StreamHudPolicy.MapPoint(marker.X, marker.Y, s, range, out px, out py)) continue;
                    g.DrawText(marker.Kind == "race" || marker.Kind == "carrera" ? "R" : "E", new RectangleF(cx + px * rx - 0.003f, cy + py * ry - 0.009f, 0.01f, 0.018f), Align, Color.Orange, smallFont);
                }
            if (s.Npcs != null)
                foreach (StreamNpcRow row in s.Npcs)
                {
                    float px, py; bool nearby;
                    if (row == null || !StreamHudPolicy.MapPointClamped(row.X, row.Y, s, range, out px, out py, out nearby)) continue;
                    float dx = cx + px * rx, dy = cy + py * ry, size = 0.0055f;
                    if (nearby && StreamHudPolicy.InCone(row.X, row.Y, s)) g.DrawRectangle(new RectangleF(dx - size / aspect, dy - size, size * 2 / aspect, size * 2), Color.White);
                    else if (nearby && StreamHudPolicy.ApproachingCone(row, s)) g.DrawRectangle(new RectangleF(dx - size / aspect, dy - size, size * 2 / aspect, size * 2), Color.Cyan);
                    g.DrawRectangle(new RectangleF(dx - size * 0.65f / aspect, dy - size * 0.65f, size * 1.3f / aspect, size * 1.3f), KindColor(row.Kind));
                }
            g.DrawRectangle(new RectangleF(cx - 0.002f, cy - 0.003f, 0.004f, 0.006f), Color.White);
            Text(g, "N  " + (int)range + "m", mx, my - 0.025f, Math.Max(w, 0.15f), 0.023f, gray, smallFont);
            Text(g, "SUB", mx, my + h + 0.003f, w / 3, 0.020f, green, smallFont);
            Text(g, "FOL", mx + w / 3, my + h + 0.003f, w / 3, 0.020f, KindColor("FOLLOW"), smallFont);
            Text(g, "DUP", mx + w * 2 / 3, my + h + 0.003f, w / 3, 0.020f, KindColor("DUPLICADO"), smallFont);
        }
        public static void Dispose()
        {
            if (textFont != null) textFont.Dispose(); if (smallFont != null) smallFont.Dispose(); if (titleFont != null) titleFont.Dispose();
            textFont = smallFont = titleFont = null;
        }
    }
}
