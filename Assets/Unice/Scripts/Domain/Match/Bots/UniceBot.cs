using System;

namespace Unice
{
    /// <summary>
    /// The Unice as a bot: chases any hider it sees, goes where it last saw them, reacts to giggles and noise,
    /// and otherwise patrols, opening the spots it hasn't checked for a while.
    /// </summary>
    sealed class UniceBot
    {
        readonly Game game;
        public UniceBot(Game game) { this.game = game; }

        /// <summary>Close enough to a spot to start opening it.</summary>
        const float OpenReach = 34;
        /// <summary>Chasing longer than this makes it tired for <see cref="TiredSeconds"/>.</summary>
        const float MaxChaseSeconds = 5, TiredSeconds = 3;
        /// <summary>A spot opened less than this many seconds ago doesn't need another look.</summary>
        const float RecentlyChecked = 6;

        public void Update(Character bot, float dt)
        {
            var brain = bot.brain; float roundClock = game.roundClock;
            bot.moving = false;
            if (game.phase != Phase.Hunt || bot.stunTime > 0) return;
            if (bot.tiredTime > 0) bot.tiredTime -= dt;

            // opening a spot takes a moment
            if (brain.checkTimer > 0)
            {
                brain.checkTimer -= dt;
                if (brain.checkTimer <= 0) FinishSpotCheck(bot, brain.checkingSpot);
                return;
            }

            var target = VisibleTarget(bot);
            if (target != null) { Chase(bot, target, dt); return; }
            brain.chaseTarget = null; brain.chaseTime = MathF.Max(0, brain.chaseTime - dt * 2);

            // lost them: go to where they were last seen
            if (brain.remembersTarget)
            {
                SetGoal(bot, new BotGoal { kind = BotGoal.Kind.Position, x = brain.rememberedX, y = brain.rememberedY, priority = 2, hopsLeft = 2 });
                brain.remembersTarget = false;
            }

            // a new giggle: react to it after a short, human-like delay
            var doll = game.doll;
            if (doll.hasLastGiggle && doll.lastGiggleTime > brain.lastGiggleHeardAt)
            {
                brain.lastGiggleHeardAt = doll.lastGiggleTime;
                brain.pendingGoal = new BotGoal
                {
                    kind = doll.lastGiggleSpot != null ? BotGoal.Kind.Spot : BotGoal.Kind.Position, spot = doll.lastGiggleSpot,
                    x = doll.lastGiggleX, y = doll.lastGiggleY, priority = 3, hopsLeft = 2,
                    hasSearchCenter = true, searchCenterX = doll.lastGiggleX, searchCenterY = doll.lastGiggleY,
                };
                brain.reactAt = roundClock + GameRandom.Range(.3f, .7f);
            }
            if (brain.pendingGoal != null && roundClock >= brain.reactAt)
            {
                var goal = brain.pendingGoal; brain.pendingGoal = null; brain.goal = null;
                SetGoal(bot, goal);
            }

            if (brain.goal == null) Patrol(bot);
            bool urgent = brain.goal != null && brain.goal.priority >= 2;
            if (BotPath.Follow(bot, dt, urgent ? Rules.UniceChaseSpeed : Rules.UnicePatrolSpeed)) OnArrived(bot);
        }

        /// <summary>The closest hider it can see (sticking to the one it is already chasing), or null.</summary>
        Character VisibleTarget(Character bot)
        {
            Character closest = null; float closestDistance = float.MaxValue; bool currentStillVisible = false;
            foreach (var hider in game.characters)
            {
                if (hider.IsUnice || hider.outOfRound || hider.hidingSpot != null) continue;
                float distance = Geometry.Distance(bot, hider);
                if (distance >= Rules.UniceViewRange || !BotPath.CanSee(bot, hider)) continue;
                if (hider == bot.brain.chaseTarget) currentStillVisible = true;
                if (distance < closestDistance) { closestDistance = distance; closest = hider; }
            }
            return currentStillVisible ? bot.brain.chaseTarget : closest;
        }

        void Chase(Character bot, Character target, float dt)
        {
            var brain = bot.brain;
            brain.chaseTarget = target; brain.remembersTarget = true; brain.rememberedX = target.x; brain.rememberedY = target.y; brain.goal = null;
            brain.chaseTime += dt;
            if (brain.chaseTime > MaxChaseSeconds) { brain.chaseTime = 0; bot.tiredTime = TiredSeconds; }
            brain.repathTimer -= dt;
            if (brain.repathTimer <= 0 || brain.path.Count == 0) { brain.repathTimer = .3f; brain.path = Navigation.FindPath(bot.x, bot.y, target.x, target.y); }
            BotPath.Follow(bot, dt, bot.tiredTime > 0 ? Rules.UniceTiredSpeed : Rules.UniceChaseSpeed);
        }

        /// <summary>Host: a hider made noise nearby. The bot goes to check unless it is busy with something more important.</summary>
        public void HearNoise(Character noisy)
        {
            var bot = game.unice; var brain = bot.brain;
            if (!bot.isBot || game.phase != Phase.Hunt || Geometry.Distance(noisy, bot) >= Rules.NoiseRange || brain.chaseTarget != null || brain.checkTimer > 0) return;
            var goal = brain.goal;
            bool alreadyGoingThere = goal != null && goal.priority >= 1 && Geometry.Distance(goal.x, goal.y, noisy.x, noisy.y) <= 90;
            if (!alreadyGoingThere) SetGoal(bot, new BotGoal { kind = BotGoal.Kind.Position, x = noisy.x, y = noisy.y, priority = 1, hopsLeft = 1 });
        }

        /// <summary>Takes <paramref name="goal"/> unless the current one is more important, and plans the path there.</summary>
        void SetGoal(Character bot, BotGoal goal)
        {
            var brain = bot.brain;
            if (brain.goal != null && brain.goal.priority > goal.priority) return;
            if (goal.spot != null) { goal.x = goal.spot.standX; goal.y = goal.spot.standY; }
            brain.goal = goal; brain.path = Navigation.FindPath(bot.x, bot.y, goal.x, goal.y);
        }

        /// <summary>The closest spot to (x, y) within <paramref name="maxDistance"/> that wasn't opened in the last <paramref name="minSecondsSinceCheck"/> seconds.</summary>
        Furniture UncheckedSpotNear(float x, float y, float maxDistance, float minSecondsSinceCheck)
        {
            Furniture nearest = null; float nearestDistance = maxDistance;
            foreach (var spot in Map.Spots)
            {
                if (game.roundClock - spot.checkedAt < minSecondsSinceCheck) continue;
                float distance = Geometry.Distance(spot.standX, spot.standY, x, y);
                if (distance < nearestDistance) { nearestDistance = distance; nearest = spot; }
            }
            return nearest;
        }

        /// <summary>No lead: wander to a random node, or open a spot that hasn't been checked for a while (favoring close ones).</summary>
        void Patrol(Character bot)
        {
            if (GameRandom.Range(0, 1) < .45f)
            {
                var node = GameRandom.Pick(Navigation.Nodes);
                SetGoal(bot, new BotGoal { kind = BotGoal.Kind.Position, x = node.x, y = node.y, priority = 0, hopsLeft = 0 });
                return;
            }
            Furniture best = null; float bestScore = float.MinValue;
            foreach (var spot in Map.Spots)
            {
                float secondsSinceCheck = MathF.Min(40, game.roundClock - spot.checkedAt);
                float score = secondsSinceCheck * 8 - Geometry.Distance(spot.standX, spot.standY, bot.x, bot.y) * .45f + GameRandom.Range(0, 70);
                if (score > bestScore) { bestScore = score; best = spot; }
            }
            SetGoal(bot, new BotGoal { kind = BotGoal.Kind.Spot, spot = best, priority = 0, hopsLeft = 0 });
        }

        /// <summary>Reached the end of its path: start opening the spot, or search the spots around the place it came to check.</summary>
        void OnArrived(Character bot)
        {
            var brain = bot.brain; var goal = brain.goal;
            if (goal == null) return;
            if (goal.kind == BotGoal.Kind.Spot && Geometry.Distance(bot.x, bot.y, goal.spot.standX, goal.spot.standY) < OpenReach)
            {
                brain.checkTimer = Rules.BotSpotCheckTime; brain.checkingSpot = goal.spot;
                return;
            }
            brain.goal = null;
            var nextSpot = goal.hopsLeft > 0 ? UncheckedSpotNear(goal.x, goal.y, 260, RecentlyChecked) : null;
            if (nextSpot != null)
                SetGoal(bot, new BotGoal { kind = BotGoal.Kind.Spot, spot = nextSpot, priority = 1, hopsLeft = goal.hopsLeft - 1, hasSearchCenter = true, searchCenterX = goal.x, searchCenterY = goal.y });
        }

        /// <summary>The spot is open: catch whoever is inside, or keep searching nearby if the goal allows more hops.</summary>
        void FinishSpotCheck(Character bot, Furniture spot)
        {
            var brain = bot.brain; var goal = brain.goal;
            brain.goal = null; brain.checkingSpot = null;
            bool someoneInside = spot.occupant != null;
            game.OpenSpot(bot, spot);
            if (someoneInside || goal == null || goal.hopsLeft <= 0) return;
            float centerX = goal.hasSearchCenter ? goal.searchCenterX : spot.standX, centerY = goal.hasSearchCenter ? goal.searchCenterY : spot.standY;
            var nextSpot = UncheckedSpotNear(centerX, centerY, 280, RecentlyChecked);
            if (nextSpot != null)
                SetGoal(bot, new BotGoal { kind = BotGoal.Kind.Spot, spot = nextSpot, priority = 1, hopsLeft = goal.hopsLeft - 1, hasSearchCenter = true, searchCenterX = centerX, searchCenterY = centerY });
        }
    }
}
