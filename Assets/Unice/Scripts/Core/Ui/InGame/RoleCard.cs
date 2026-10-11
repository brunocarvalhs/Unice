using UnityEngine;
using UnityEngine.UIElements;
using static Unice.UiKit;

namespace Unice
{
    /// <summary>
    /// HUD top-right card: your role and goal, who has the doll (if your team can know), when it giggles next,
    /// and how much noise you are making.
    /// </summary>
    sealed class RoleCard : UiPart
    {
        static readonly string[] RoleClasses = { "role-unice", "role-out", "role-holder" };

        readonly VisualElement card, giggleMeter, giggleMeterFill, noiseBox, noiseMeterFill;
        readonly Label roleLabel, goalLabel, dollHolderLabel, dollNoteLabel, noiseLabel;

        public RoleCard(Ui ui, VisualElement parent) : base(ui)
        {
            card = IgnorePointer(AddElement(parent, "hud-card", "hud-tr"));
            roleLabel = AddLabel(card, "", "role-t"); goalLabel = AddLabel(card, "", "role-g");
            var dollRow = AddElement(card, "row", "wrap"); dollRow.style.marginTop = 8;
            LocalizedLabel(dollRow, "hud.doll", "hud-lbl"); dollHolderLabel = AddLabel(dollRow, "", "hud-lbl");
            giggleMeter = AddElement(card, "meter"); giggleMeterFill = AddElement(giggleMeter, "meter-fill");
            dollNoteLabel = AddLabel(card, "", "note");
            noiseBox = AddElement(card); noiseBox.style.marginTop = 8;
            var noiseRow = AddElement(noiseBox, "row", "wrap");
            LocalizedLabel(noiseRow, "hud.noise", "hud-lbl"); noiseLabel = AddLabel(noiseRow, "", "hud-lbl");
            var noiseMeter = AddElement(noiseBox, "meter", "meter-noise"); noiseMeterFill = AddElement(noiseMeter, "meter-fill");
            foreach (var label in card.Query<Label>().ToList()) IgnorePointer(label);
        }

        public void Refresh()
        {
            var me = app.myCharacter;
            RefreshRole(me);
            RefreshDoll(me);
            RefreshNoise(me);
        }

        void RefreshRole(Character me)
        {
            string role, goal, roleClass = "";
            if (me == null) { role = Localization.Get("hud.role.watch"); goal = Localization.Get("hud.goal.watch"); }
            else if (me.IsUnice) { role = Localization.Get("hud.role.unice"); roleClass = "role-unice"; goal = Localization.Get(game.phase == Phase.Hide ? "hud.goal.unice_hide" : "hud.goal.unice_hunt"); }
            else if (me.outOfRound) { role = Localization.Get("hud.role.caught"); roleClass = "role-out"; goal = Localization.Get("hud.goal.caught"); }
            else if (game.doll.holder == me) { role = Localization.Get("hud.role.holder"); roleClass = "role-holder"; goal = Localization.Get("hud.goal.holder", game.giggleInterval.ToString("0")); }
            else { role = Localization.Get("hud.role.hiding"); goal = Localization.Get(me.hidingSpot != null ? "hud.goal.hiding_spot" : "hud.goal.hiding_out"); }
            SetText(roleLabel, role); SetText(goalLabel, goal);
            foreach (var cssClass in RoleClasses) card.EnableInClassList(cssClass, cssClass == roleClass);
        }

        /// <summary>The team sees who has the doll and a meter until the next giggle; the Unice only sees where it last giggled.</summary>
        void RefreshDoll(Character me)
        {
            var doll = game.doll; var holder = doll.holder;
            float giggleProgress = 0; string note;
            if (holder != null && app.perspective.ShowsTeamInfo)
            {
                SetText(dollHolderLabel, holder == me ? Localization.Get("hud.doll_with_you") : Localization.Get("hud.doll_with", holder.name.ToUpperInvariant()));
                giggleProgress = game.phase == Phase.Hunt ? 1 - doll.giggleTimer / game.giggleInterval : 0;
                if (game.phase == Phase.Hide) note = Localization.Get("hud.giggle_wait_enter");
                else if (holder.hidingSpot != null) note = Localization.Get("hud.giggle_in", Localization.SpotPhrase(holder.hidingSpot), Mathf.CeilToInt(doll.giggleTimer));
                else note = Localization.Get("hud.giggle_quiet");
                if (holder == me && me.passCooldown > 0) note += Localization.Get("hud.repass_in", Mathf.CeilToInt(me.passCooldown));
                Show(giggleMeter, true);
            }
            else
            {
                SetText(dollHolderLabel, holder != null ? Localization.Get("hud.doll_hidden") : "");
                if (doll.hasLastGiggle)
                {
                    var room = Map.RoomAt(doll.lastGiggleX, doll.lastGiggleY);
                    int secondsAgo = Mathf.Max(0, Mathf.FloorToInt(game.roundClock - doll.lastGiggleTime));
                    note = Localization.Get("hud.giggle_last", Localization.RoomPhrase(room), secondsAgo);
                }
                else note = Localization.Get("hud.giggle_every", game.giggleInterval.ToString("0"));
                Show(giggleMeter, false);
            }
            giggleMeterFill.style.width = Length.Percent(Mathf.Clamp01(giggleProgress) * 100);
            SetText(dollNoteLabel, note);
        }

        void RefreshNoise(Character me)
        {
            Show(noiseBox, me != null && !me.outOfRound && !me.IsUnice);
            if (me == null) return;
            noiseMeterFill.style.width = Length.Percent(Mathf.Clamp01(me.noiseLevel) * 100);
            string noiseKey = me.hidingSpot != null ? "hud.noise_hidden" : me.noiseLevel > .5f ? "hud.noise_loud" : me.moving ? "hud.noise_quiet" : "hud.noise_stopped";
            SetText(noiseLabel, Localization.Get(noiseKey));
        }
    }
}
