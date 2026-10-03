using System;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>One piece of the UI (a screen, the HUD, the touch controls…): reaches the app and the game through <see cref="Ui"/>.</summary>
    abstract class UiPart
    {
        protected readonly Ui ui;
        protected App app => ui.app;
        protected Game game => ui.app.game;

        protected UiPart(Ui ui) { this.ui = ui; }

        /// <summary>A label whose text follows language changes (see <see cref="Ui.LocalizedLabel"/>).</summary>
        protected Label LocalizedLabel(VisualElement parent, string key, params string[] classes) => ui.LocalizedLabel(parent, key, classes);
        protected Button LocalizedButton(VisualElement parent, string key, Action onClick, params string[] classes) => ui.LocalizedButton(parent, key, onClick, classes);
    }
}
