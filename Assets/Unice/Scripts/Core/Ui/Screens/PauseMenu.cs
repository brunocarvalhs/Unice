using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>The pause sheet: continue, sound/voice settings, leave. Solo it freezes the match; online it only covers the screen.</summary>
    sealed class PauseMenu : UiPart
    {
        /// <summary>The full-screen dim; <see cref="Ui"/> pads it so the sheet stays inside the safe area.</summary>
        public readonly VisualElement Root;
        readonly Label titleLabel, noteLabel;
        readonly Button leaveButton;

        public PauseMenu(Ui ui) : base(ui)
        {
            var sheet = AddSheet(ui.root, out Root);
            titleLabel = AddLabel(sheet, "", "title", "title-md");
            noteLabel = AddLabel(sheet, "", "muted");
            LocalizedButton(sheet, "pause.continue", () => app.TogglePause(), "btn", "gap");
            var settingsRow = AddElement(sheet, "row", "gap", "wrap");
            ui.settings.AddSoundButton(settingsRow, "btn", "btn-ghost");
            ui.settings.AddMicButton(settingsRow, "btn", "btn-ghost");
            ui.settings.AddWhisperButton(settingsRow, "btn", "btn-ghost");
            leaveButton = AddButton(sheet, "", () => app.LeaveRoom(), "btn", "btn-link", "gap");
            UiKit.Show(Root, false);
        }

        public void Show(bool visible, bool online)
        {
            UiKit.Show(Root, visible);
            titleLabel.text = Localization.Get(online ? "pause.online_title" : "pause.title");
            noteLabel.text = Localization.Get(online ? "pause.note_online" : "pause.note_local");
            leaveButton.text = Localization.Get(online ? "res.leave_room" : "pause.leave_match");
        }
    }
}
