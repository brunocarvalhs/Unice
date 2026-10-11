using UnityEngine;
using UnityEngine.UIElements;

namespace Unice
{
    /// <summary>
    /// Phones: the on-screen keyboard covers the lower half of the screen, so while a text field has focus the given
    /// element slides up until the field sits just above the keyboard.
    /// </summary>
    sealed class KeyboardLift
    {
        /// <summary>Gap kept between the field and the keyboard (panel pixels).</summary>
        const float Margin = 16;

        readonly Ui ui;
        readonly VisualElement target;

        public KeyboardLift(Ui ui, VisualElement target) { this.ui = ui; this.target = target; }

        /// <summary>Per frame.</summary>
        public void Update()
        {
            float lift = 0;
            var root = ui.root;
            if (ui.Typing && TouchScreenKeyboard.visible && root.panel.focusController.focusedElement is VisualElement field
                && target.Contains(field) && Screen.height > 0)
            {
                float pixelsToPanel = root.layout.height / Screen.height;
                float keyboardHeight = TouchScreenKeyboard.area.height;
                if (keyboardHeight < 1) keyboardHeight = Screen.height * .5f;   // some Android keyboards don't report their size
                float keyboardTop = root.layout.height - keyboardHeight * pixelsToPanel - Margin;
                float fieldBottom = field.worldBound.yMax - target.resolvedStyle.translate.y;   // where it would be without the lift
                lift = Mathf.Max(0, fieldBottom - keyboardTop);
            }
            if (target.resolvedStyle.translate.y != -lift) target.style.translate = new Translate(0, -lift);
        }
    }
}
