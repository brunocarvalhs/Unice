using System;
using System.Text;

namespace Unice
{
    /// <summary>Cleaning text that comes from players (names, room codes) and formatting the clock.</summary>
    public static class TextFormat
    {
        public const int MaxNameLength = 12;
        const string FallbackName = "Alguém";
        /// <summary>A new profile's name when there's no Play Games name to use.</summary>
        const string DefaultName = "Nova";
        /// <summary>Room code letters: no 0/O, 1/I/L, so codes are easy to read aloud and type.</summary>
        const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        const int RoomCodeLength = 5;

        /// <summary>Names come from other players: drop control and invisible characters, trim, cap the length.</summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return FallbackName;
            var clean = new StringBuilder();
            foreach (char c in name)
            {
                bool invisible = c < 0x20 || (c >= 0x7f && c <= 0x9f) || (c >= 0x200b && c <= 0x200f) || (c >= 0x2028 && c <= 0x202e) || (c >= 0x2060 && c <= 0x206f) || c == 0xfeff;
                if (!invisible) clean.Append(c);
            }
            var result = clean.ToString().Trim();
            if (result.Length > MaxNameLength) result = result.Substring(0, MaxNameLength).Trim();
            return result.Length == 0 ? FallbackName : result;
        }

        /// <summary>
        /// The profile name: the one saved on this device wins (the player chose it, or it was settled on the first run);
        /// with nothing saved yet, the Google Play Games name; without one, "Nova". Null means "none".
        /// </summary>
        public static string FirstRunName(string savedName, string playGamesName)
        {
            if (savedName != null) return CleanName(savedName);
            return string.IsNullOrWhiteSpace(playGamesName) ? DefaultName : CleanName(playGamesName);
        }

        /// <summary>Normalizes a typed room code: uppercase letters and digits only.</summary>
        public static string CleanCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            var clean = new StringBuilder();
            foreach (char c in code.ToUpperInvariant()) if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) clean.Append(c);
            return clean.ToString();
        }

        public static string RandomRoomCode()
        {
            var code = new StringBuilder();
            for (int i = 0; i < RoomCodeLength; i++) code.Append(CodeAlphabet[GameRandom.Index(CodeAlphabet.Length)]);
            return code.ToString();
        }

        /// <summary>Seconds left as "m:ss", rounding up (so it shows 0:00 only at the end).</summary>
        public static string FormatMinutesSeconds(float seconds)
        {
            int whole = Math.Max(0, (int)MathF.Ceiling(seconds));
            return (whole / 60) + ":" + (whole % 60).ToString("00");
        }
    }
}
