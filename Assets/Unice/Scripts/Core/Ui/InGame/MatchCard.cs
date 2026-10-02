using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>HUD top-left card: round, clock, phase, which room the player is in, and the room code (or "solo").</summary>
    sealed class MatchCard : UiPart
    {
        /// <summary>The clock turns red in the last seconds of the hunt.</summary>
        const float LowTimeSeconds = 15;

        readonly Label roundLabel, clockLabel, phaseLabel, whereLabel, roomLabel;

        public MatchCard(Ui ui, VisualElement parent) : base(ui)
        {
            var card = IgnorePointer(AddElement(parent, "hud-card", "hud-tl"));
            var roundRow = AddElement(card, "row");
            LocalizedLabel(roundRow, "hud.round", "hud-lbl"); roundLabel = AddLabel(roundRow, "", "hud-lbl");
            clockLabel = AddLabel(card, "0:30", "clock");
            phaseLabel = AddLabel(card, "", "phase");
            whereLabel = AddLabel(card, "", "where");
            roomLabel = AddLabel(card, "", "roomline");
            foreach (var label in card.Query<Label>().ToList()) IgnorePointer(label);
        }

        public void Refresh()
        {
            var me = app.myCharacter; bool live = game.IsLive;
            SetText(roundLabel, $"{game.round}/{game.totalRounds}");
            SetText(clockLabel, TextFormat.FormatMinutesSeconds(live ? game.timeLeft : 0));
            clockLabel.EnableInClassList("clock-low", game.phase == Phase.Hunt && game.timeLeft <= LowTimeSeconds);

            string phaseKey = game.phase switch
            {
                Phase.Hide => "hud.phase_hide",
                Phase.Hunt => game.unice == me ? "hud.phase_hunt_you" : "hud.phase_hunt_other",
                Phase.Result => "hud.phase_result",
                _ => null,
            };
            SetText(phaseLabel, phaseKey != null ? Localization.Get(phaseKey) : "");

            bool onTheMap = me != null && !me.outOfRound && me.x > 0 && live;
            SetText(whereLabel, onTheMap ? Localization.Get("hud.where", Localization.RoomPhrase(Map.RoomAt(me.x, me.y))) : "");
            SetText(roomLabel, app.IsOnline
                ? Localization.Get("hud.room_online", app.roomCode) + (game.isHost ? Localization.Get("hud.room_host_suffix") : "")
                : Localization.Get("hud.room_solo"));
        }
    }
}
