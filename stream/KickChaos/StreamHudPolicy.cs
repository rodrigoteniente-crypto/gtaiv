using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KickChaos
{
    // Layout, clipping and projection stay independent of GTA so they can be checked offline.
    public static class StreamHudPolicy
    {
        public static float Finite(float value, float fallback) { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value; }
        public static float Clamp(float value, float min, float max, float fallback = 0) { return Math.Max(min, Math.Min(max, Finite(value, fallback))); }
        public static string Clean(string text, int max)
        {
            if (max < 1) return "";
            var b = new StringBuilder();
            foreach (char c in text ?? "")
            {
                if (char.IsControl(c) || char.IsSurrogate(c) || c == '~') continue;
                b.Append(c);
            }
            string value = b.ToString().Trim();
            if (value.Length <= max) return value;
            return max < 3 ? value.Substring(0, max) : value.Substring(0, max - 2).TrimEnd() + "..";
        }
        public static string SearchKey(string text)
        {
            var b = new StringBuilder();
            foreach (char c in Clean(text, 500).ToLowerInvariant().Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && !char.IsControl(c)) b.Append(c);
            return b.ToString().Normalize(NormalizationForm.FormC);
        }
        public static bool Matches(string label, string query)
        {
            string value = SearchKey(label);
            foreach (string term in SearchKey(query).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (value.IndexOf(term, StringComparison.Ordinal) < 0) return false;
            return true;
        }
        public static List<StreamNpcRow> Rows(StreamSnapshot s, int max, double now)
        {
            var rows = new List<StreamNpcRow>();
            if (s == null || s.Npcs == null) return rows;
            foreach (StreamNpcRow r in s.Npcs)
                if (r != null && Finite(r.Health, 0) > 0) rows.Add(r);
            rows.Sort((a, b) =>
            {
                int result = (b.Ped == s.FollowedPed && s.Following).CompareTo(a.Ped == s.FollowedPed && s.Following);
                if (result != 0) return result;
                result = (b.RecentUntil > now).CompareTo(a.RecentUntil > now);
                if (result != 0) return result;
                result = KindOrder(a.Kind).CompareTo(KindOrder(b.Kind));
                if (result != 0) return result;
                return a.Id.CompareTo(b.Id);
            });
            int limit = Math.Max(1, Math.Min(12, max));
            if (rows.Count > limit) rows.RemoveRange(limit, rows.Count - limit);
            return rows;
        }
        static int KindOrder(string kind) { return kind == "SUB" ? 0 : kind == "DUPLICADO" ? 1 : 2; }
        public static string CameraStatus(StreamNpcRow row)
        {
            if (!row.CameraAvailable) return "CAM -";
            double left = row.CameraReadyIn;
            if (double.IsNaN(left) || double.IsInfinity(left) || left < 0) left = 0;
            return left > 0 ? "!npc " + Math.Ceiling(Math.Min(9999, left)).ToString(CultureInfo.InvariantCulture) + "s" : "!npc LISTO";
        }
        public static string Stats(StreamNpcRow r)
        {
            return "HP " + Math.Round(Clamp(r.Health, 0, 100)).ToString(CultureInfo.InvariantCulture) + "  K " + Math.Max(0, r.Kills)
                + "  " + (r.Build == "AGRESIVO" ? "AGR" : r.Build == "CAZADOR" ? "CAZ" : r.Build == "SUPERVIVIENTE" ? "SUP" : Clean(r.Build, 3)) + "  " + CameraStatus(r);
        }
        public static int Capacity(float y, int desired, int feedRows, bool spotlight)
        {
            float reserve = 0.035f + 0.024f + 0.016f + Math.Max(0, Math.Min(5, feedRows)) * 0.025f + (spotlight ? 0.060f : 0);
            return Math.Max(1, Math.Min(Math.Min(12, desired), (int)((0.94f - Clamp(y, 0.01f, 0.30f, 0.055f) - reserve) / 0.052f)));
        }
        public static StreamNpcRow Spotlight(StreamSnapshot snapshot, List<StreamFeedEntry> feed, double now, double seconds)
        {
            if (snapshot == null || snapshot.Npcs == null || feed == null || seconds <= 0) return null;
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                StreamFeedEntry entry = feed[i];
                if (entry == null || (entry.Kind != "sub" && entry.Kind != "spawnsub") || now < entry.At || now - entry.At > seconds) continue;
                foreach (StreamNpcRow row in snapshot.Npcs)
                    if (row != null && row.Kind == "SUB" && row.Health > 0 && (string.Equals(row.Owner, entry.User, StringComparison.OrdinalIgnoreCase) || string.Equals(row.Name, entry.User, StringComparison.OrdinalIgnoreCase))) return row;
            }
            return null;
        }
        public static bool Fresh(StreamSnapshot s, DateTime nowUtc)
        {
            if (s == null || !s.Active) return false;
            DateTime updated;
            return DateTime.TryParse(s.UpdatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out updated)
                && nowUtc >= updated.AddSeconds(-5) && nowUtc - updated <= TimeSpan.FromSeconds(10);
        }
        public static bool MapPoint(float worldX, float worldY, StreamSnapshot s, float range, out float x, out float y)
        {
            x = y = 0;
            if (s == null || float.IsNaN(worldX) || float.IsNaN(worldY) || float.IsInfinity(worldX) || float.IsInfinity(worldY)) return false;
            if (float.IsNaN(s.CameraX) || float.IsNaN(s.CameraY) || float.IsInfinity(s.CameraX) || float.IsInfinity(s.CameraY)) return false;
            range = Clamp(range, 50, 5000, 600);
            x = (worldX - s.CameraX) / range;
            y = -(worldY - s.CameraY) / range;
            return Math.Abs(x) <= 1 && Math.Abs(y) <= 1;
        }
        public static bool InCone(float worldX, float worldY, StreamSnapshot s)
        {
            float x, y;
            if (!MapPoint(worldX, worldY, s, 5000, out x, out y)) return false;
            double distance = Math.Sqrt(x * x + y * y);
            if (distance < 0.00001) return true;
            double angle = Clamp(s.CameraHeading, -100000, 100000) * Math.PI / 180;
            double dot = (x * -Math.Sin(angle) + y * -Math.Cos(angle)) / distance;
            return dot >= Math.Cos(Clamp(s.CameraFov, 5, 120, 45) * Math.PI / 360);
        }
        public static bool ApproachingCone(StreamNpcRow row, StreamSnapshot s)
        {
            if (row == null || !row.Driving || InCone(row.X, row.Y, s)) return false;
            double heading = Clamp(row.Heading, -100000, 100000) * Math.PI / 180;
            return InCone(row.X - (float)Math.Sin(heading) * 45, row.Y + (float)Math.Cos(heading) * 45, s);
        }
        public static bool MapPointClamped(float worldX, float worldY, StreamSnapshot s, float range, out float x, out float y, out bool nearby)
        {
            nearby = MapPoint(worldX, worldY, s, range, out x, out y);
            if (s == null || float.IsNaN(worldX) || float.IsNaN(worldY) || float.IsInfinity(worldX) || float.IsInfinity(worldY)
                || float.IsNaN(s.CameraX) || float.IsNaN(s.CameraY) || float.IsInfinity(s.CameraX) || float.IsInfinity(s.CameraY)) return false;
            float scale = Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)));
            x /= scale; y /= scale;
            return !float.IsNaN(x) && !float.IsNaN(y);
        }
        public static string FeedLabel(StreamFeedEntry e)
        {
            string tag = Clean(e.Kind, 10).ToUpperInvariant();
            return Clean("[" + tag + "] " + e.User + (string.IsNullOrWhiteSpace(e.Text) ? "" : " " + e.Text), 60);
        }
    }
}
