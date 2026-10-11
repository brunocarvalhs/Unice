using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// The player's name, an animated preview of their child, and the look options. There is one instance, moved between
    /// "Minha criança" (home menu) and the waiting room's appearance tab.
    /// </summary>
    sealed class KidCustomizer : UiPart
    {
        public readonly VisualElement Root;
        readonly CharacterPortrait preview;
        readonly KitOptionPicker optionPicker;
        readonly TextField nameField;

        public KidCustomizer(Ui ui) : base(ui)
        {
            Root = new VisualElement();
            LocalizedLabel(Root, "lbl.name", "lbl");
            nameField = AddTextField(Root, app.myName, TextFormat.MaxNameLength);
            nameField.RegisterValueChangedCallback(e => { app.myName = TextFormat.CleanName(e.newValue); app.SaveProfile(); });

            LocalizedLabel(Root, "lbl.kid", "lbl").style.marginTop = 10;
            var previewRow = AddElement(Root, "row");
            var previewBox = AddElement(previewRow, "preview"); previewBox.style.alignItems = Align.Center; previewBox.style.justifyContent = Justify.Center;
            preview = new CharacterPortrait(100); previewBox.Add(preview);
            LocalizedButton(previewRow, "menu.randomize", () => SetKit(Kit.Random()), "btn", "btn-ghost");

            var options = AddElement(Root); options.style.marginTop = 10;
            optionPicker = new KitOptionPicker(ui, options, SetKit);
        }

        void SetKit(int[] kit)
        {
            app.SetKit(kit);
            optionPicker.Render();
            ui.OnKitChanged();
        }

        /// <summary>Moves the customizer under <paramref name="parent"/> (if it isn't there already) and refreshes it.</summary>
        public void ShowIn(VisualElement parent)
        {
            if (Root.parent != parent) parent.Add(Root);
            nameField.SetValueWithoutNotify(app.myName);   // the first run's Play Games name may have arrived since
            optionPicker.Render();
        }

        /// <summary>The preview faces down, right, up, left in turn (1.4 s each), walking.</summary>
        static readonly Vector2[] PreviewDirections = { new Vector2(.01f, 1), new Vector2(1, .2f), new Vector2(.01f, -1), new Vector2(-1, .2f) };

        /// <summary>Per frame while visible.</summary>
        public void AnimatePreview()
        {
            float time = Time.unscaledTime;
            var direction = PreviewDirections[(int)(time / 1.4f) % 4];
            int walkFrame = 1 + Mathf.FloorToInt(time * 13 / (Mathf.PI * 2) * 6) % 6;
            preview.ShowKit(app.kit, showBack: direction.y < -.6f, frame: walkFrame, flip: direction.x < -.3f);
        }
    }
}
