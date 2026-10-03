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
        const string DefaultName = "Nova";

        public string Name;
        public int[] Kit;

        /// <summary>Loads the saved profile; a first run gets the default name and a random look.</summary>
        public ProfileStore()
        {
            Name = TextFormat.CleanName(PlayerPrefs.GetString(NameKey, DefaultName));
            Kit = PlayerPrefs.HasKey(KitKey) ? Unice.Kit.Unpack(PlayerPrefs.GetInt(KitKey)) : Unice.Kit.Random();
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
