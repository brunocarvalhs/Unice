using System.Collections.Generic;
using UnityEngine;

namespace Unice
{
    public enum Language { Pt, En, Es }

    /// <summary>
    /// Every player-facing string, by key, in Pt/En/Es. Pt is the game's original language and the fallback.
    /// Call <see cref="Get"/> wherever text reaches the player; keep everything else (asset paths, USS classes,
    /// GameObject names, debug logs) as plain strings — those aren't localized.
    /// </summary>
    public static partial class Localization
    {
        public static Language Current { get; private set; } = Load();

        const string LanguagePrefKey = "unice.lang";

        /// <summary>Switches the language and remembers it on this device.</summary>
        public static void Set(Language language)
        {
            Current = language;
            PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
            PlayerPrefs.Save();
            Metrics.SetLanguageProperty();
        }

        static Language Load() => Initial(
            PlayerPrefs.HasKey(LanguagePrefKey) ? PlayerPrefs.GetInt(LanguagePrefKey) : (int?)null, Application.systemLanguage);

        /// <summary>The language to open in: the one saved on this device (<paramref name="savedLanguage"/>, null on the
        /// first run) wins; otherwise the device's own — Portuguese → Pt, Spanish → Es, anything else → En.</summary>
        public static Language Initial(int? savedLanguage, SystemLanguage systemLanguage)
        {
            if (savedLanguage.HasValue) return (Language)Mathf.Clamp(savedLanguage.Value, 0, 2);
            return systemLanguage switch
            {
                SystemLanguage.Portuguese => Language.Pt,
                SystemLanguage.Spanish => Language.Es,
                _ => Language.En,
            };
        }

        /// <summary>Looks up <paramref name="key"/> in the current language and formats it with <paramref name="args"/>.
        /// A missing key returns a visibly-broken placeholder instead of throwing, so one gap doesn't crash the UI.</summary>
        public static string Get(string key, params object[] args)
        {
            if (!Strings.TryGetValue(key, out var translations)) return "⟨missing:" + key + "⟩";
            string text = translations[(int)Current];
            return args != null && args.Length > 0 ? string.Format(text, args) : text;
        }

        /// <summary>A room's name, e.g. "Biblioteca" / "Library" / "Biblioteca".</summary>
        public static string RoomName(Room room) => Get("room." + room.id + ".name");
        /// <summary>A room's "in" phrase for sentences, e.g. "na Biblioteca" / "in the Library" / "en la Biblioteca".</summary>
        public static string RoomPhrase(Room room) => Get("room." + room.id + ".in");
        /// <summary>A hiding spot's flavor phrase, e.g. "debaixo da cama" / "under the bed" / "debajo de la cama".</summary>
        public static string SpotPhrase(Furniture spot) => Get("spot." + spot.type);
        /// <summary>A kit option's display name, for the slots that have names (hair, outfit, hat, accessory).</summary>
        public static string KitOptionName(int slot, int option) => Get("kit." + KitSlotKeys[slot] + "." + option);
        static readonly Dictionary<int, string> KitSlotKeys = new Dictionary<int, string>
        {
            { Kit.HairSlot, "hair" }, { Kit.OutfitSlot, "outfit" }, { Kit.HatSlot, "hat" }, { Kit.AccessorySlot, "accessory" },
        };

        /// <summary>1º / 1st / 1º — the one piece of text whose shape (not just words) differs per language.</summary>
        public static string Ordinal(int number)
        {
            if (Current != Language.En) return number + "º";
            int lastTwoDigits = number % 100;
            if (lastTwoDigits >= 11 && lastTwoDigits <= 13) return number + "th";   // 11th, 12th, 13th
            return (number % 10) switch { 1 => number + "st", 2 => number + "nd", 3 => number + "rd", _ => number + "th" };
        }
    }
}
