using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// In-game HUD: the match card (top left), the role card (top right), the map/menu buttons and the panel with the
    /// house plan and the player list. Refreshed a few times a second, not every frame.
    /// </summary>
    sealed class Hud : UiPart
    {
        public readonly VisualElement Root;
        readonly MatchCard matchCard;
        readonly RoleCard roleCard;
        readonly PlayerListPanel playerList;

        public Hud(Ui ui) : base(ui)
        {
            Root = IgnorePointer(AddElement(ui.safeArea, "layer"));
            matchCard = new MatchCard(ui, Root);
            roleCard = new RoleCard(ui, Root);
            var buttons = IgnorePointer(AddElement(Root, "hud-btns"));
            LocalizedButton(buttons, "hud.map_btn", () => playerList.Toggle(), "hud-btn");
            LocalizedButton(buttons, "hud.menu_btn", () => app.TogglePause(), "hud-btn");
            playerList = new PlayerListPanel(ui, Root);
        }

        /// <summary>Every 0.2 s while playing.</summary>
        public void Tick() { matchCard.Refresh(); roleCard.Refresh(); playerList.Refresh(); }

        /// <summary>Rebuild the player list on the next tick even if nothing seems to have changed (e.g. the language did).</summary>
        public void Invalidate() => playerList.Invalidate();
    }
}
