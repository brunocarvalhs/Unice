using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>Colors of a banner (USS classes "banner-info", "banner-rose", …).</summary>
    public enum BannerStyle { Info, Rose, Blood, Moss, Candle }

    /// <summary>
    /// Text messages over the game: the big banner, the toast stack, the caption for sounds heard while hidden,
    /// the key-hints prompt, and the hiding-phase countdown (black screen for the Unice, a pill for everyone else).
    /// </summary>
    sealed class Messages : UiPart
    {
        const int MaxToasts = 3;
        const float ToastSeconds = 3.4f, CaptionSeconds = 2.4f;

        readonly Label banner, caption, countdownPillText, countdownNumber, prompt;
        readonly VisualElement toasts, countdownPill, captionBox, blackout, promptBox;
        float bannerElapsed, bannerDuration, captionTimeLeft;
        readonly List<(Label label, float removeAt)> activeToasts = new List<(Label, float)>();

        public Messages(Ui ui) : base(ui)
        {
            var safeArea = ui.safeArea;
            blackout = IgnorePointer(AddElement(safeArea, "layer", "blackout"));
            countdownNumber = (Label)IgnorePointer(AddLabel(blackout, "", "count"));
            IgnorePointer(LocalizedLabel(blackout, "game.countdown_sub", "count-sub"));
            countdownPill = IgnorePointer(AddElement(safeArea, "pill"));
            countdownPillText = (Label)IgnorePointer(AddLabel(countdownPill, "", "pill-in"));
            captionBox = IgnorePointer(AddElement(safeArea, "caption"));
            caption = (Label)IgnorePointer(AddLabel(captionBox, "", "caption-in"));
            promptBox = IgnorePointer(AddElement(safeArea, "prompt"));
            prompt = (Label)IgnorePointer(AddLabel(promptBox, "", "prompt-in"));
            if (ui.isMobile) promptBox.style.bottom = 150;   // above the touch buttons
            toasts = IgnorePointer(AddElement(safeArea, "toasts"));
            banner = (Label)IgnorePointer(AddLabel(safeArea, "", "banner"));
            Show(banner, false); Show(blackout, false); Show(countdownPill, false); Show(captionBox, false); Show(promptBox, false);
        }

        public void Banner(string text, BannerStyle style, float seconds)
        {
            banner.text = text;
            foreach (BannerStyle each in System.Enum.GetValues(typeof(BannerStyle)))
                banner.EnableInClassList("banner-" + each.ToString().ToLowerInvariant(), each == style);
            bannerElapsed = 0; bannerDuration = seconds; Show(banner, true);
        }

        /// <summary>A short line at the top; the newest goes first and only the last few stay.</summary>
        public void Toast(string text)
        {
            var toast = (Label)IgnorePointer(AddLabel(toasts, text, "toast"));
            toast.RemoveFromHierarchy(); toasts.Insert(0, toast);
            activeToasts.Add((toast, Time.unscaledTime + ToastSeconds));
            while (toasts.childCount > MaxToasts) toasts[toasts.childCount - 1].RemoveFromHierarchy();
        }

        /// <summary>Describes a sound heard while hidden (shown only while the player is hidden).</summary>
        public void Caption(string text) { caption.text = text; captionTimeLeft = CaptionSeconds; }

        /// <summary>The key hints line ("E hide under the bed    Q pass to Bia").</summary>
        public void SetPrompt(string text)
        {
            if (prompt.text == text) return;
            prompt.text = text; Show(promptBox, text.Length > 0);
        }

        public void Frame(float dt)
        {
            var me = app.myCharacter;

            // the banner pops in, holds, then fades out
            if (bannerDuration > 0)
            {
                bannerElapsed += dt; float progress = bannerElapsed / bannerDuration;
                if (progress >= 1) { bannerDuration = 0; Show(banner, false); }
                else
                {
                    banner.style.opacity = progress < .12f ? progress / .12f : progress > .75f ? (1 - progress) / .25f : 1;
                    float scale = progress < .12f ? .85f + progress / .12f * .15f : 1;
                    banner.style.scale = new Scale(new Vector3(scale, scale, 1));
                }
            }

            for (int i = activeToasts.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > activeToasts[i].removeAt) { activeToasts[i].label.RemoveFromHierarchy(); activeToasts.RemoveAt(i); }

            if (captionTimeLeft > 0) captionTimeLeft -= dt;
            Show(captionBox, captionTimeLeft > 0 && me != null && me.hidingSpot != null);

            // hiding phase: the Unice waits with eyes closed, the others see a countdown
            bool hiding = game.phase == Phase.Hide; int secondsLeft = Mathf.CeilToInt(game.timeLeft);
            Show(blackout, hiding && me != null && me.IsUnice);
            Show(countdownPill, hiding && (me == null || !me.IsUnice));
            if (hiding) { SetText(countdownNumber, secondsLeft.ToString()); SetText(countdownPillText, Localization.Get("game.unice_enters_in", secondsLeft)); }
        }
    }
}
