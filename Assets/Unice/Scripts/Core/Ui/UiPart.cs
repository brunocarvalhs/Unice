using System;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>One piece of the UI (a screen, the HUD, the touch controls…): reaches the app and game through <see cref="Ui"/>.</summary>
    abstract class UiPart
    {
        protected readonly Ui ui;
        protected App app => ui.app;
        protected Game G => ui.app.G;
        protected UiPart(Ui ui) { this.ui = ui; }
        protected Label LL(VisualElement p, string key, params string[] cls) => ui.LL(p, key, cls);
        protected Button BB(VisualElement p, string key, Action click, params string[] cls) => ui.BB(p, key, click, cls);
    }
}
