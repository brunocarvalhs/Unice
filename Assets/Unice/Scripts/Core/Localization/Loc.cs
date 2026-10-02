using System.Collections.Generic;
using UnityEngine;

namespace Unice
{
    public enum Lang { Pt, En, Es }

    /// <summary>
    /// Every player-facing string, by key, in Pt/En/Es. Pt is the game's original language and the fallback.
    /// Call <see cref="T"/> wherever text reaches the player; keep everything else (asset paths, USS classes,
    /// GameObject names, debug logs) as plain strings — those aren't localized.
    /// </summary>
    public static partial class Loc
    {
        public static Lang Current { get; private set; } = Load();

        public static void Set(Lang l)
        {
            Current = l;
            PlayerPrefs.SetInt("unice.lang", (int)l);
            PlayerPrefs.Save();
            Metrics.SetLang();
        }

        static Lang Load() => (Lang)Mathf.Clamp(PlayerPrefs.GetInt("unice.lang", (int)Lang.Pt), 0, 2);

        /// <summary>Looks up <paramref name="key"/> in the current language and formats it with <paramref name="args"/>.
        /// A missing key returns a visibly-broken placeholder instead of throwing, so one gap doesn't crash the UI.</summary>
        public static string T(string key, params object[] args)
        {
            if (!Table.TryGetValue(key, out var arr)) return "⟨missing:" + key + "⟩";
            string s = arr[(int)Current];
            return args != null && args.Length > 0 ? string.Format(s, args) : s;
        }

        /// <summary>A room's name, e.g. "Biblioteca" / "Library" / "Biblioteca".</summary>
        public static string RoomName(Room r) => T("room." + r.id + ".name");
        /// <summary>A room's "in" phrase for sentences, e.g. "na Biblioteca" / "in the Library" / "en la Biblioteca".</summary>
        public static string RoomIn(Room r) => T("room." + r.id + ".in");
        /// <summary>A hiding spot's flavor phrase, e.g. "debaixo da cama" / "under the bed" / "debajo de la cama".</summary>
        public static string Spot(Furn f) => T("spot." + f.type);
        /// <summary>A kit option's display name by slot (1 hair, 3 outfit, 5 hat, 6 accessory) and index.</summary>
        public static string KitOpt(int slot, int idx) => T("kit." + KitSlotKey[slot] + "." + idx);
        static readonly Dictionary<int, string> KitSlotKey = new Dictionary<int, string> { { 1, "hair" }, { 3, "outfit" }, { 5, "hat" }, { 6, "accessory" } };

        /// <summary>1º / 1st / 1º — the one piece of text whose shape (not just words) differs per language.</summary>
        public static string Ordinal(int n)
        {
            if (Current != Lang.En) return n + "º";
            int t = n % 100;
            if (t >= 11 && t <= 13) return n + "th";
            return (n % 10) switch { 1 => n + "st", 2 => n + "nd", 3 => n + "rd", _ => n + "th" };
        }
    }
}
