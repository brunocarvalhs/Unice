using System.Collections.Generic;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// The sound / mic / whisper / language buttons. The same setting appears on several screens (menu, waiting room,
    /// pause), so every copy is registered here and they all update together.
    /// </summary>
    sealed class SettingToggles : UiPart
    {
        readonly List<Button> soundButtons = new List<Button>(), micButtons = new List<Button>(), whisperButtons = new List<Button>(), languageButtons = new List<Button>();
        readonly System.Action onLanguageChanged;

        public SettingToggles(Ui ui, System.Action onLanguageChanged) : base(ui) { this.onLanguageChanged = onLanguageChanged; }

        public Button AddSoundButton(VisualElement parent, params string[] classes) => Register(soundButtons, AddButton(parent, "", ToggleSound, classes));
        /// <summary>Personal choice: when on, the room hears you through proximity voice chat as long as you're not hidden.</summary>
        public Button AddMicButton(VisualElement parent, params string[] classes) => Register(micButtons, AddButton(parent, "", ToggleMic, classes));
        /// <summary>Personal choice: "whisper" transmits quieter and never counts as noise for the Unice. Needs the mic on.</summary>
        public Button AddWhisperButton(VisualElement parent, params string[] classes) => Register(whisperButtons, AddButton(parent, "", ToggleWhisper, classes));
        public Button AddLanguageButton(VisualElement parent, params string[] classes) => Register(languageButtons, AddButton(parent, "", CycleLanguage, classes));

        static Button Register(List<Button> list, Button button) { list.Add(button); return button; }

        void ToggleSound() { app.sfx.Enabled = !app.sfx.Enabled; Refresh(); }
        void ToggleMic() { app.MicOn = !app.MicOn; Refresh(); }
        void ToggleWhisper() { app.Whisper = !app.Whisper; Refresh(); }
        void CycleLanguage()
        {
            var next = (Language)(((int)Localization.Current + 1) % 3);
            Localization.Set(next);
            onLanguageChanged();
        }

        /// <summary>Re-applies every button's text (and the whisper buttons' enabled state) from the current settings.</summary>
        public void Refresh()
        {
            string sound = Localization.Get(app.sfx.Enabled ? "menu.sound_on" : "menu.sound_off");
            string mic = Localization.Get(app.MicOn ? "menu.mic_on" : "menu.mic_off");
            string whisper = Localization.Get(app.Whisper ? "menu.voice_whisper" : "menu.voice_normal");
            string language = $"{Localization.Get("menu.lang_prefix")}: {Localization.Current.ToString().ToUpperInvariant()}";
            foreach (var button in soundButtons) button.text = sound;
            foreach (var button in micButtons) button.text = mic;
            foreach (var button in whisperButtons) { button.text = whisper; button.SetEnabled(app.MicOn); }
            foreach (var button in languageButtons) button.text = language;
        }
    }
}
