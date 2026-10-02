using System;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>Tiny UI Toolkit builders shared by every screen (`using static Unice.UiKit`).</summary>
    static class UiKit
    {
        public static T Add<T>(VisualElement p, T el, string[] cls) where T : VisualElement { foreach (var c in cls) el.AddToClassList(c); p.Add(el); return el; }
        public static VisualElement E(VisualElement p, params string[] cls) => Add(p, new VisualElement(), cls);
        public static Label L(VisualElement p, string text, params string[] cls) => Add(p, new Label(text), cls);
        public static Button B(VisualElement p, string text, Action click, params string[] cls) => Add(p, new Button(click) { text = text }, cls);
        public static VisualElement Ig(VisualElement e) { e.pickingMode = PickingMode.Ignore; return e; }
        public static void Show(VisualElement e, bool on) { var d = on ? DisplayStyle.Flex : DisplayStyle.None; if (e.style.display != d) e.style.display = d; }
        public static void Txt(Label l, string t) { if (l.text != t) l.text = t; }
        public static TextField Field(VisualElement p, string value, int max, params string[] cls)
        {
            var f = new TextField { value = value, maxLength = max }; f.AddToClassList("field"); foreach (var c in cls) f.AddToClassList(c); p.Add(f); return f;
        }
        public static VisualElement Sheet(VisualElement p, out VisualElement overlay)
        {
            overlay = E(p, "overlay");
            var sc = new ScrollView(ScrollViewMode.Vertical); sc.AddToClassList("sheet-scroll"); overlay.Add(sc);
            return E(sc, "sheet");
        }
    }

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
