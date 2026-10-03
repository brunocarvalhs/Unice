using System;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// The "Mute" / "Unmute" button for another player's voice, on this device only. Shown in the waiting room cards and the
    /// in-game player list — the store's voice-chat policy asks for a way to stop hearing someone.
    /// </summary>
    static class MuteButton
    {
        /// <summary>Adds the button if that player can be muted (online, with voice, not this device); returns null otherwise.</summary>
        /// <param name="onChanged">Called after a toggle, so the screen re-renders the button's text.</param>
        public static Button Add(VisualElement parent, App app, string playerId, Action onChanged)
        {
            if (!app.CanMutePlayer(playerId)) return null;
            bool muted = app.IsPlayerMuted(playerId);
            var button = AddButton(parent, Localization.Get(muted ? "voice.unmute_player" : "voice.mute_player"),
                () => { app.TogglePlayerMuted(playerId); onChanged(); }, "mpill", "mute-btn");
            button.EnableInClassList("mute-btn-on", muted);
            button.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());   // in the player list, don't also pick the row to spectate
            return button;
        }
    }
}
