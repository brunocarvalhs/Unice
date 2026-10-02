using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>The pause sheet. Solo it freezes the match; online it only covers the screen.</summary>
    sealed class PauseMenu : UiPart
    {
        readonly VisualElement pauseOv; readonly Label pauseTitle, pauseNote; readonly Button pauseLeave;

        public PauseMenu(Ui ui) : base(ui)
        {
            var sh = Sheet(ui.root, out pauseOv);
            pauseTitle = L(sh, "", "title", "title-md");
            pauseNote = L(sh, "", "muted");
            BB(sh, "pause.continue", () => app.TogglePause(), "btn", "gap");
            var r = E(sh, "row", "gap", "wrap");
            ui.SoundBtn(r, "btn", "btn-ghost");
            ui.MicBtn(r, "btn", "btn-ghost");
            ui.WhisperBtn(r, "btn", "btn-ghost");
            pauseLeave = B(sh, "", () => app.LeaveRoom(), "btn", "btn-link", "gap");
            UiKit.Show(pauseOv, false);
        }

        public void Show(bool on, bool online)
        {
            UiKit.Show(pauseOv, on);
            pauseTitle.text = Loc.T(online ? "pause.online_title" : "pause.title");
            pauseNote.text = Loc.T(online ? "pause.note_online" : "pause.note_local");
            pauseLeave.text = Loc.T(online ? "res.leave_room" : "pause.leave_match");
        }
    }
}
