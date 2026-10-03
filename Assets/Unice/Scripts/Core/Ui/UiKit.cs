using System;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>
    /// Tiny UI Toolkit builders shared by every screen (import with <c>using static Unice.UiKit;</c>).
    /// Each "Add" creates an element, gives it the USS classes, appends it to <c>parent</c> and returns it.
    /// </summary>
    static class UiKit
    {
        public static T Add<T>(VisualElement parent, T element, string[] classes) where T : VisualElement
        {
            foreach (var cssClass in classes) element.AddToClassList(cssClass);
            parent.Add(element);
            return element;
        }

        public static VisualElement AddElement(VisualElement parent, params string[] classes) => Add(parent, new VisualElement(), classes);
        public static Label AddLabel(VisualElement parent, string text, params string[] classes) => Add(parent, new Label(text), classes);
        public static Button AddButton(VisualElement parent, string text, Action onClick, params string[] classes) => Add(parent, new Button(onClick) { text = text }, classes);

        public static TextField AddTextField(VisualElement parent, string value, int maxLength, params string[] classes)
        {
            var field = new TextField { value = value, maxLength = maxLength };
            field.AddToClassList("field");
            return Add(parent, field, classes);
        }

        /// <summary>A dimmed full-screen overlay with a scrollable sheet in the middle; returns the sheet.</summary>
        public static VisualElement AddSheet(VisualElement parent, out VisualElement overlay)
        {
            overlay = AddElement(parent, "overlay");
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("sheet-scroll"); overlay.Add(scroll);
            return AddElement(scroll, "sheet");
        }

        /// <summary>Lets touches and clicks pass through the element (for overlays and labels drawn over the game).</summary>
        public static T IgnorePointer<T>(T element) where T : VisualElement { element.pickingMode = PickingMode.Ignore; return element; }

        /// <summary>Shows or hides (display: none). Only writes the style when it changes, since this runs every step.</summary>
        public static void Show(VisualElement element, bool visible)
        {
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != display) element.style.display = display;
        }

        /// <summary>Sets the text only when it changes (avoids re-layout every step).</summary>
        public static void SetText(Label label, string text) { if (label.text != text) label.text = text; }
    }
}
