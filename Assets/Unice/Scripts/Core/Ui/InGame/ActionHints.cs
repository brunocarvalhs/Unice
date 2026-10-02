using System.Collections.Generic;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// What this player can do right now (E use a spot, Q pass the doll, R laugh), as the key-hints line and as the state
    /// of the touch buttons. Computed every step from the game state.
    /// </summary>
    struct ActionHints
    {
        /// <summary>The player is in the round (not watching, not caught): the action buttons show.</summary>
        public bool active;
        public bool isUnice;
        /// <summary>E does something here (hide, leave the spot, or open it as the Unice).</summary>
        public bool canUseSpot;
        /// <summary>Q shows (holding the doll) and works now (someone in reach, no cooldown).</summary>
        public bool showPass, canPass;
        public bool showLaugh, canLaugh;
        /// <summary>The hints line ("E hide under the bed    Q pass to Bia").</summary>
        public string text;

        /// <param name="showKeys">Prefix each hint with its keyboard key (not on touch screens).</param>
        public static ActionHints For(Game game, Character me, bool showKeys)
        {
            var hints = new ActionHints();
            var parts = new List<string>();
            string Key(string key) => showKeys ? $"<b>{key}</b> " : "";
            hints.active = me != null && game.IsLive && !me.outOfRound;
            hints.isUnice = me != null && me.IsUnice;
            if (hints.active)
            {
                if (me.IsUnice)
                {
                    if (game.phase == Phase.Hunt && me.stunTime <= 0)
                    {
                        var spot = Game.NearestSpot(me);
                        if (spot != null) { parts.Add(Key("E") + Localization.Get("prompt.look", Localization.SpotPhrase(spot))); hints.canUseSpot = true; }
                    }
                }
                else
                {
                    if (me.hidingSpot != null) { parts.Add(Key("E") + Localization.Get("prompt.exit_hide")); hints.canUseSpot = true; }
                    else
                    {
                        var spot = Game.NearestSpot(me);
                        if (spot != null)
                        {
                            if (spot.occupant != null) parts.Add(Localization.Get("prompt.occupied", spot.occupant.name, Localization.SpotPhrase(spot)));
                            else { parts.Add(Key("E") + Localization.Get("prompt.hide", Localization.SpotPhrase(spot))); hints.canUseSpot = true; }
                        }
                    }

                    if (game.doll.holder == me)
                    {
                        hints.showPass = true;
                        var receiver = game.PassTarget(me);
                        if (receiver != null)
                        {
                            hints.canPass = me.passCooldown <= 0;
                            parts.Add(me.passCooldown > 0
                                ? Localization.Get("prompt.pass_in", Mathf.CeilToInt(me.passCooldown))
                                : Key("Q") + Localization.Get("prompt.pass_to", receiver.name));
                        }
                    }

                    hints.showLaugh = true; hints.canLaugh = me.laughCooldown <= 0;
                    parts.Add(me.laughCooldown > 0
                        ? Localization.Get("prompt.laugh_in", Mathf.CeilToInt(me.laughCooldown))
                        : Key("R") + Localization.Get("prompt.laugh"));
                }
            }
            hints.text = string.Join("    ", parts);
            return hints;
        }
    }
}
