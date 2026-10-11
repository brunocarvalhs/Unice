using UnityEngine;

namespace Unice
{
    /// <summary>
    /// This device's player name and look, saved on the device (PlayerPrefs). The screens reach it through
    /// <see cref="App.myName"/>, <see cref="App.kit"/>, <see cref="App.SaveProfile"/> and <see cref="App.SetKit"/>.
    /// </summary>
    public sealed class ProfileStore
    {
        const string NameKey = "unice.name", KitKey = "unice.kit";

        public string Name;
        public int[] Kit;

        /// <summary>Loads the saved profile; a first run gets the default name (until <see cref="UsePlayGamesName"/>) and a random look.</summary>
        public ProfileStore()
        {
            Name = TextFormat.FirstRunName(PlayerPrefs.HasKey(NameKey) ? PlayerPrefs.GetString(NameKey) : null, null);
            Kit = PlayerPrefs.HasKey(KitKey) ? Unice.Kit.Unpack(PlayerPrefs.GetInt(KitKey)) : Unice.Kit.Random();
        }

        /// <summary>
        /// The Google Play Games name, which arrives a moment after the app opens: becomes the name (and is saved) only if
        /// none was saved yet, so a name the player chose is never replaced.
        /// </summary>
        public void UsePlayGamesName(string playGamesName)
        {
            if (PlayerPrefs.HasKey(NameKey) || string.IsNullOrWhiteSpace(playGamesName)) return;   // no name: try again next run
            Name = TextFormat.FirstRunName(null, playGamesName);
            Save();
        }

        public void Save()
        {
            PlayerPrefs.SetString(NameKey, Name);
            PlayerPrefs.SetInt(KitKey, Unice.Kit.Pack(Kit));
            PlayerPrefs.Save();
        }

        /// <summary>Replaces the look (invalid options become 0) and saves.</summary>
        public void SetKit(int[] newKit) { Kit = Unice.Kit.Sanitize(newKit); Save(); }
    }
}
