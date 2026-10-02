using UnityEngine;

namespace Unice
{
    /// <summary>
    /// This device's own name and look, persisted locally via PlayerPrefs. Extracted out of App so App stays
    /// a thin Unity-lifecycle adapter; App keeps exposing myName/kit/SaveProfile/SetKit as one-line passthroughs
    /// so nothing else (Ui, etc.) needs to change.
    /// </summary>
    public sealed class ProfileStore
    {
        public string Name;
        public int[] Kit;

        public ProfileStore()
        {
            Name = U.CleanName(PlayerPrefs.GetString("unice.name", "Nova"));
            Kit = PlayerPrefs.HasKey("unice.kit") ? Unice.Kit.Unpack(PlayerPrefs.GetInt("unice.kit")) : Unice.Kit.Random();
        }

        public void Save()
        {
            PlayerPrefs.SetString("unice.name", Name);
            PlayerPrefs.SetInt("unice.kit", Unice.Kit.Pack(Kit));
            PlayerPrefs.Save();
        }

        public void SetKit(int[] k) { Kit = Unice.Kit.Clean(k); Save(); }
    }
}
