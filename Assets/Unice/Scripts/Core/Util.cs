using System;
using System.Collections.Generic;
using System.Text;

namespace Unice
{
    /// <summary>A point on the house floor plan, in "house pixels" (x right, y down), same units as the web version.</summary>
    public struct P
    {
        public float x, y;
        public P(float x, float y) { this.x = x; this.y = y; }
    }

    public static class U
    {
        static readonly Random rng = new Random();
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        public static int RandInt(int n) => rng.Next(n);
        public static T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];
        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        }
        public static float Dist(float ax, float ay, float bx, float by) { float dx = ax - bx, dy = ay - by; return MathF.Sqrt(dx * dx + dy * dy); }
        public static float Dist(Ent a, Ent b) => Dist(a.x, a.y, b.x, b.y);

        /// <summary>Names come from other players: drop control/invisible characters, trim, cap at 12.</summary>
        public static string CleanName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Alguém";
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (ch < 0x20 || (ch >= 0x7f && ch <= 0x9f) || (ch >= 0x200b && ch <= 0x200f) || (ch >= 0x2028 && ch <= 0x202e) || (ch >= 0x2060 && ch <= 0x206f) || ch == 0xfeff) continue;
                sb.Append(ch);
            }
            var r = sb.ToString().Trim();
            if (r.Length > 12) r = r.Substring(0, 12).Trim();
            return r.Length == 0 ? "Alguém" : r;
        }

        public static string Mmss(float t)
        {
            int s = Math.Max(0, (int)MathF.Ceiling(t));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        const string Alph = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        /// <summary>Normalizes a typed room code: uppercase letters and digits only.</summary>
        public static string CleanCode(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char ch in s.ToUpperInvariant()) if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
            return sb.ToString();
        }
        public static string OfflineCode()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 5; i++) sb.Append(Alph[rng.Next(Alph.Length)]);
            return sb.ToString();
        }
    }
}
