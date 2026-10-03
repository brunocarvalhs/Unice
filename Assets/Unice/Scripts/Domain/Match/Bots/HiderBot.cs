using System;
using System.Linq;

namespace Unice
{
    /// <summary>
    /// A bot hider: picks a spot away from the Unice and walks (or sneaks) there; runs away when it sees the Unice;
    /// passes the doll when cornered; and, holding the doll, leaves its spot after a giggle gives it away.
    /// </summary>
    sealed class HiderBot
    {
        readonly Game game;
        public HiderBot(Game game) { this.game = game; }

        /// <summary>Seconds without seeing the Unice before going back to hiding.</summary>
        const float FleeMemory = 1.4f;
        /// <summary>Hidden and the Unice gets this close: may bolt (30%); calms down once it is past <see cref="CalmDistance"/>.</summary>
        const float BoltDistance = 100, CalmDistance = 220, BoltChance = .3f;
        /// <summary>Holding the doll and the Unice is this close and seen: pass it.</summary>
        const float PassWhenUniceWithin = 260;
        /// <summary>Sneaks (quiet but slow) while the Unice is within this distance.</summary>
        const float SneakWhenUniceWithin = 380;
        /// <summary>Close enough to the spot's standing point to get in.</summary>
        const float HideReach = 26;

        public void Update(Character bot, float dt)
        {
            if (bot.outOfRound) return;
            var brain = bot.brain; var uniceCharacter = game.unice; float roundClock = game.roundClock;
            bool hunting = game.phase == Phase.Hunt;
            float uniceDistance = Geometry.Distance(bot, uniceCharacter);
            bool seesUnice = hunting && (uniceDistance < Rules.BotHearingRange || (uniceDistance < Rules.BotSightRange && BotPath.CanSee(bot, uniceCharacter)));
            if (seesUnice) brain.lastSawUniceAt = roundClock;

            bool hasDoll = game.doll.holder == bot;
            if (hasDoll && seesUnice && uniceDistance < PassWhenUniceWithin && bot.hidingSpot == null && bot.passCooldown <= 0)
            {
                var receiver = game.PassTarget(bot);
                if (receiver != null) game.PassDoll(bot, receiver);
            }

            if (bot.hidingSpot != null) { UpdateHidden(bot, hasDoll, hunting, uniceDistance); return; }

            if (seesUnice) brain.mode = HiderMode.Flee;
            if (brain.mode == HiderMode.Flee)
            {
                if (roundClock - brain.lastSawUniceAt > FleeMemory) { brain.mode = HiderMode.Seek; brain.targetSpot = null; }
                else { Flee(bot, uniceCharacter, uniceDistance, dt); return; }
            }

            // seek: walk to a free spot and hide there
            var target = brain.targetSpot;
            if (target == null || target.occupant != null || (target.reservedBy != null && target.reservedBy != bot)) ChooseHidingSpot(bot);
            var spot = brain.targetSpot;
            if (spot == null) { bot.moving = false; return; }
            bot.sneaking = hunting && uniceDistance < SneakWhenUniceWithin && roundClock > brain.dashUntil;
            if (BotPath.Follow(bot, dt, bot.sneaking ? Rules.SneakSpeed : Rules.RunSpeed))
            {
                if (spot.occupant == null && Geometry.Distance(bot.x, bot.y, spot.standX, spot.standY) < HideReach) game.Hide(bot, spot);
                else { if (spot.reservedBy == bot) spot.reservedBy = null; brain.targetSpot = null; }   // didn't make it: pick another next step
            }
        }

        void UpdateHidden(Character bot, bool hasDoll, bool hunting, float uniceDistance)
        {
            var brain = bot.brain; float roundClock = game.roundClock;
            bot.moving = false;

            // the doll just gave this spot away: run to another one
            if (hasDoll && brain.leaveSpotAt > 0 && roundClock >= brain.leaveSpotAt)
            {
                brain.leaveSpotAt = 0; brain.dashUntil = roundClock + 2.5f;
                Game.LeaveSpot(bot); brain.mode = HiderMode.Seek; ChooseHidingSpot(bot);
                return;
            }

            if (hunting && uniceDistance < BoltDistance && !brain.startled)
            {
                brain.startled = true;
                if (GameRandom.Range(0, 1) < BoltChance) { Game.LeaveSpot(bot); brain.mode = HiderMode.Flee; brain.lastSawUniceAt = roundClock; brain.path.Clear(); }
            }
            else if (uniceDistance > CalmDistance) brain.startled = false;
        }

        void Flee(Character bot, Character uniceCharacter, float uniceDistance, float dt)
        {
            var brain = bot.brain;
            // out of sight and far enough: duck into a spot close by
            if (uniceDistance > 200 && !BotPath.CanSee(bot, uniceCharacter))
            {
                var spot = game.NearestFreeSpot(bot, 70);
                if (spot != null) { game.Hide(bot, spot); return; }
            }
            brain.repathTimer -= dt;
            if (brain.repathTimer <= 0 || brain.path.Count == 0)
            {
                brain.repathTimer = 1.2f;
                var away = FleePoint(bot, uniceCharacter);
                brain.path = Navigation.FindPath(bot.x, bot.y, away.x, away.y);
            }
            bot.sneaking = false;
            BotPath.Follow(bot, dt, Rules.RunSpeed);
        }

        /// <summary>
        /// Picks (and reserves) the best free spot: far from the Unice (or the front door while it waits outside), close to the bot,
        /// avoiding where the doll was last heard and where this bot already hid.
        /// </summary>
        void ChooseHidingSpot(Character bot)
        {
            var brain = bot.brain; var doll = game.doll;
            if (brain.targetSpot != null && brain.targetSpot.reservedBy == bot) brain.targetSpot.reservedBy = null;
            bool uniceOutside = game.phase == Phase.Hide;
            float threatX = uniceOutside ? Map.Entrance.x : game.unice.x, threatY = uniceOutside ? Map.Entrance.y : game.unice.y;

            Furniture best = null; float bestScore = float.MinValue;
            foreach (var spot in Map.Spots)
            {
                if (spot.occupant != null || (spot.reservedBy != null && spot.reservedBy != bot)) continue;
                float score = Geometry.Distance(spot.standX, spot.standY, threatX, threatY) * .8f
                            - Geometry.Distance(spot.standX, spot.standY, bot.x, bot.y) * .55f
                            + GameRandom.Range(0, 140);
                if (doll.hasLastGiggle && Geometry.Distance(spot.standX, spot.standY, doll.lastGiggleX, doll.lastGiggleY) < 350) score -= 450;
                if (spot == doll.lastGiggleSpot) score -= 500;
                if (spot == brain.previousSpot) score -= 700;
                if (score > bestScore) { bestScore = score; best = spot; }
            }
            brain.targetSpot = best;
            if (best != null) { best.reservedBy = bot; brain.path = Navigation.FindPath(bot.x, bot.y, best.standX, best.standY); }
        }

        /// <summary>A nav node a short run away that leads away from the Unice (not past it).</summary>
        static Point FleePoint(Character bot, Character uniceCharacter)
        {
            var nearby = Navigation.Nodes.Where(node =>
            {
                float distance = Geometry.Distance(node.x, node.y, bot.x, bot.y);
                return distance > 80 && distance < 460;
            }).ToList();

            Point best = default; bool found = false; float bestScore = float.MinValue;
            for (int i = 0; i < 20 && nearby.Count > 0; i++)
            {
                var node = GameRandom.Pick(nearby);
                float distanceFromBot = Geometry.Distance(node.x, node.y, bot.x, bot.y);
                float score = Geometry.Distance(node.x, node.y, uniceCharacter.x, uniceCharacter.y) - distanceFromBot * .35f;
                bool towardsUnice = (node.x - bot.x) * (uniceCharacter.x - bot.x) + (node.y - bot.y) * (uniceCharacter.y - bot.y) > 0;
                if (towardsUnice) score -= 300;
                if (score > bestScore) { bestScore = score; best = node; found = true; }
            }
            return found ? best : Navigation.Nodes[Navigation.NearestNode(bot.x, bot.y)];
        }
    }
}
