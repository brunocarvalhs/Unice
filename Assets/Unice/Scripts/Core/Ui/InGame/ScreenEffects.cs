using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// Full-screen effects: the vignette (dark behind the menus, red heartbeat when the Unice is near), the slats seen
    /// from inside a hiding spot, and the quick color flash of a catch.
    /// </summary>
    sealed class ScreenEffects : UiPart
    {
        const int SlatCount = 60;
        const float SlatSpacing = 26;

        readonly VisualElement vignette, slats, flash;
        float flashTimeLeft, flashDuration; Color flashColor;

        public ScreenEffects(Ui ui) : base(ui)
        {
            var root = ui.root;
            vignette = IgnorePointer(AddElement(root, "layer"));
            vignette.style.backgroundImage = new StyleBackground(Art.Centered("vignette", 1));
            slats = IgnorePointer(AddElement(root, "layer"));
            for (int i = 0; i < SlatCount; i++) IgnorePointer(AddElement(slats, "slat")).style.top = i * SlatSpacing;
            flash = IgnorePointer(AddElement(root, "layer")); Show(flash, false);
        }

        /// <summary>A quick full-screen color flash (the catch moment) — no new art, just an alpha-decaying overlay.</summary>
        public void Flash(Color color, float seconds) { flashColor = color; flashDuration = Mathf.Max(.01f, seconds); flashTimeLeft = flashDuration; }

        public void Frame(float dt)
        {
            var me = app.myCharacter; var heartbeat = app.heartbeat;
            bool live = game.IsLive, inGame = live || game.phase == Phase.Result;

            heartbeat.Fade(dt);
            if (!inGame)
            {
                vignette.style.unityBackgroundImageTintColor = new Color(0, 0, 0, .88f); vignette.style.backgroundColor = new Color(0, 0, 0, .3f);
                Show(vignette, true);
            }
            else if (heartbeat.Intensity > 0 && live)
            {
                float alpha = .18f + .4f * heartbeat.Intensity * heartbeat.Pulse;
                vignette.style.unityBackgroundImageTintColor = new Color(.886f, .29f, .32f, alpha); vignette.style.backgroundColor = Color.clear;
                Show(vignette, true);
            }
            else Show(vignette, false);

            Show(slats, live && me != null && me.hidingSpot != null);

            if (flashTimeLeft > 0)
            {
                flashTimeLeft -= dt;
                var color = flashColor; color.a *= Mathf.Max(0, flashTimeLeft) / flashDuration;
                flash.style.backgroundColor = color; Show(flash, true);
            }
            else Show(flash, false);
        }
    }
}
