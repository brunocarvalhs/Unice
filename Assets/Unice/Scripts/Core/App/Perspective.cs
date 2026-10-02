using System.Linq;

namespace Unice
{
    /// <summary>Where this device looks from and how far it sees.</summary>
    public struct ViewInfo
    {
        public float x, y, radius;
        /// <summary>Walls block the view (false for spectators, who see a plain circle).</summary>
        public bool usesLineOfSight;
        public bool spectating;
    }

    /// <summary>
    /// This device's point of view: where it looks from and how far it sees (its own character, or whoever it spectates),
    /// what is visible from there, and how to call each character to this player ("você" vs. their name).
    /// </summary>
    sealed class Perspective
    {
        /// <summary>Where a spectator looks when there is no one to follow.</summary>
        const float DefaultViewX = 860, DefaultViewY = 590;

        readonly App app;
        Game game => app.game;
        Character me => app.myCharacter;

        public ViewInfo view;
        /// <summary>False until there is something to look at (stays false at home before the first match).</summary>
        public bool hasView;
        /// <summary>While spectating, who to follow (their playerId) — null means auto (the doll holder). A stale id just falls back.</summary>
        public string spectateTargetId;

        public Perspective(App app) { this.app = app; }

        /// <summary>Per step: follows the player (or the spectated one) once there's something to look at.</summary>
        public void Update()
        {
            if (me == null && game.phase == Phase.Home) return;
            view = ComputeView(); hasView = true;
        }

        /// <summary>False only for the Unice while a round is live: then the hiders' information (who has the doll, who is hidden) stays secret.</summary>
        public bool ShowsTeamInfo => me == null || me.outOfRound || !me.IsUnice || !game.IsLive;

        ViewInfo ComputeView()
        {
            if (me == null || me.outOfRound)
            {
                // spectating: follow the chosen player, else the doll holder
                var followed = spectateTargetId != null ? game.characters.FirstOrDefault(character => character.playerId == spectateTargetId && !character.outOfRound) : null;
                followed ??= game.doll.holder;
                bool onMap = followed != null && followed.x > 0;
                return new ViewInfo
                {
                    x = onMap ? followed.x : DefaultViewX, y = onMap ? followed.y : DefaultViewY,
                    radius = Rules.SpectatorViewRange, usesLineOfSight = false, spectating = true,
                };
            }
            float radius = me.hidingSpot != null ? Rules.HiddenViewRange : me.IsUnice ? Rules.UniceViewRange : Rules.HiderViewRange;
            return new ViewInfo { x = me.x, y = me.y, radius = radius, usesLineOfSight = true };
        }

        /// <summary>Whether (x, y) is within view: close enough and, unless spectating, with no wall in between.</summary>
        public bool CanSee(float x, float y)
        {
            if (!hasView || Geometry.Distance(x, y, view.x, view.y) > view.radius) return false;
            return !view.usesLineOfSight || Map.HasLineOfSight(view.x, view.y, x, y);
        }

        /// <summary>"você" for this player, the name for others, "alguém" for nobody (mid-sentence).</summary>
        public string NameFor(Character character) =>
            character == null ? Localization.Get("app.someone") : character == me ? Localization.Get("app.you") : character.name;

        /// <summary>Same as <see cref="NameFor"/>, capitalized for the start of a sentence.</summary>
        public string NameForCapitalized(Character character) =>
            character == null ? Localization.Get("app.someone.up") : character == me ? Localization.Get("app.you.up") : character.name;
    }
}
