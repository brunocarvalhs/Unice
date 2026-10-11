using System.Linq;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// The panel opened by the HUD's map button: the house plan and the list of players with their status and score.
    /// While spectating, tapping a player follows them.
    /// </summary>
    sealed class PlayerListPanel : UiPart
    {
        readonly VisualElement panel, rows;
        readonly MiniMap miniMap;
        /// <summary>Everything the list shows, as text: the list is only rebuilt when this changes.</summary>
        string renderedSignature;

        public PlayerListPanel(Ui ui, VisualElement parent) : base(ui)
        {
            panel = IgnorePointer(AddElement(parent, "hud-card", "plist"));
            LocalizedLabel(panel, "hud.house_plan", "hud-lbl");
            miniMap = new MiniMap(ui, panel);
            LocalizedLabel(panel, "hud.players", "hud-lbl").style.marginTop = 8;
            rows = AddScroll(panel, "plist-rows", null);
            Show(panel, false);
        }

        bool IsOpen => panel.style.display != DisplayStyle.None;

        public void Toggle() { Show(panel, !IsOpen); renderedSignature = null; }
        public void Invalidate() => renderedSignature = null;

        public void Refresh()
        {
            if (!IsOpen) return;
            miniMap.Repaint();

            var me = app.myCharacter; var holder = game.doll.holder; var perspective = app.perspective;
            bool teamInfo = perspective.ShowsTeamInfo, spectating = me == null || me.outOfRound;
            string signature = string.Join("|", game.characters.Select(character => $"{character.playerId},{character.role},{character.outOfRound},{character.hidingSpot != null},{character.moving},{character.sneaking},{character == holder},{character.score},{Kit.Pack(character.kit)},{app.IsPlayerMuted(character.playerId)},{app.CanMutePlayer(character.playerId)}"))
                             + me?.playerId + me?.role + game.phase + teamInfo + Localization.Current + spectating + perspective.spectateTargetId;
            if (signature == renderedSignature) return;
            renderedSignature = signature;

            var host = app.IsOnline ? app.FindHost() : null;
            string hostId = host != null ? host.id : app.IsOnline ? null : app.myPlayerId;
            rows.Clear();
            foreach (var character in game.characters) AddRow(character, me, holder, hostId, teamInfo, spectating);
        }

        void AddRow(Character character, Character me, Character holder, string hostId, bool teamInfo, bool spectating)
        {
            var perspective = app.perspective;
            var row = AddElement(rows, "plrow"); if (character.outOfRound) row.AddToClassList("plout");

            bool canFollow = spectating && !character.outOfRound;
            bool following = spectating && (perspective.spectateTargetId != null ? perspective.spectateTargetId == character.playerId : character == holder);
            if (canFollow)
            {
                row.AddToClassList("plrow-pick");
                row.RegisterCallback<PointerDownEvent>(_ => { perspective.spectateTargetId = character.playerId; renderedSignature = null; });
            }
            else row.pickingMode = PickingMode.Ignore;
            row.EnableInClassList("plrow-on", following);

            var portrait = new CharacterPortrait(24);
            if (character.IsUnice && game.IsLive) portrait.SetUnice(); else portrait.ShowKit(character.kit);
            row.Add(portrait);

            var column = IgnorePointer(AddElement(row, "grow")); column.style.marginLeft = 8;
            var nameLine = IgnorePointer(AddElement(column, "row", "wrap"));   // long name + tags wrap instead of overflowing
            IgnorePointer(AddLabel(nameLine, character.name + (character == me ? Localization.Get("hud.you_suffix") : ""), "plname"));
            if (character.isBot) IgnorePointer(AddLabel(nameLine, Localization.Get("hud.bot_tag"), "tag"));
            if (app.IsOnline && character.playerId == hostId) IgnorePointer(AddLabel(nameLine, Localization.Get("hud.host_tag"), "tag"));
            IgnorePointer(AddLabel(column, Localization.Get(StatusKey(character, teamInfo)), "plst"));

            if (teamInfo && character == holder)
            {
                var dollIcon = IgnorePointer(AddElement(row));
                dollIcon.style.width = 18; dollIcon.style.height = 24; dollIcon.style.backgroundImage = new StyleBackground(Art.Centered("doll", 1));
            }
            if (!character.isBot && character != me) MuteButton.Add(row, app, character.playerId, Invalidate);
            IgnorePointer(AddLabel(row, character.score.ToString(), "pts")).style.marginLeft = 8;
        }

        /// <summary>What each one is doing; the Unice's player only gets "somewhere" for the hiders.</summary>
        static string StatusKey(Character character, bool teamInfo)
        {
            if (character.IsUnice) return "hud.st_unice";
            if (character.outOfRound) return "hud.st_out";
            if (!teamInfo) return "hud.st_somewhere";
            if (character.hidingSpot != null) return "hud.st_hidden";
            if (!character.moving) return "hud.st_still";
            return character.sneaking ? "hud.st_sneaking" : "hud.st_running";
        }
    }
}
