using System;

namespace KickChaos
{
    /// <summary>Acomodar textos del menu (sin dependencias del juego).</summary>
    public static class MenuText
    {
        /// <summary>Ancho / alto de la pantalla.</summary>
        public static float Aspect = 16f / 9f;

        /// <summary>
        /// Que el nombre y el valor de una fila no se pisen: si no entran, se acorta el valor
        /// (y si hace falta, el nombre) con "..". El ancho de cada letra se estima.
        /// </summary>
        public static void FitRow(ref string label, ref string value, float width)
        {
            float charW = 0.026f * 0.5f / Math.Max(0.5f, Aspect);
            int max = Math.Max(10, (int)(width / charW));
            int need = label.Length + (value.Length > 0 ? value.Length + 3 : 0);
            if (need <= max) return;
            int room = max - label.Length - 3;
            if (room < 10)
            {
                int lblRoom = Math.Max(8, max - Math.Min(value.Length, 16) - 3);
                label = Cut(label, lblRoom);
                room = max - label.Length - 3;
            }
            value = Cut(value, Math.Max(6, room));
        }

        static string Cut(string s, int n)
        {
            if (s.Length <= n) return s;
            bool arrows = s.StartsWith("<  ") && s.EndsWith("  >");
            if (arrows)
            {
                string inner = s.Substring(3, s.Length - 6);
                int k = Math.Max(2, n - 8);
                return "<  " + (inner.Length > k ? inner.Substring(0, k).TrimEnd() + ".." : inner) + "  >";
            }
            return s.Substring(0, Math.Max(2, n - 2)).TrimEnd() + "..";
        }

    }
}
