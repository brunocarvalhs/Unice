using System;
using System.Collections.Generic;

namespace Unice
{
    /// <summary>Walking a bot along its path (<see cref="BotBrain.path"/>), with shortcuts and getting unstuck.</summary>
    static class BotPath
    {
        /// <summary>Moving less than this fraction of the step counts as blocked.</summary>
        const float BlockedFraction = .3f;
        const float StuckSeconds = .5f;
        const int GiveUpAfterStucks = 4;

        /// <summary>Walks towards the next waypoint at <paramref name="speed"/>. Returns true once there is no path left (arrived or gave up).</summary>
        public static bool Follow(Character bot, float dt, float speed)
        {
            var brain = bot.brain; var path = brain.path;
            if (path.Count == 0) { bot.moving = false; return true; }

            // skip a waypoint when the one after it is in a straight line
            if (path.Count > 1 && Map.CanWalkStraight(bot.x, bot.y, path[1].x, path[1].y)) path.RemoveAt(0);

            var waypoint = path[0];
            float dx = waypoint.x - bot.x, dy = waypoint.y - bot.y, distance = MathF.Sqrt(dx * dx + dy * dy), step = speed * dt;
            bot.moving = true;
            if (distance <= step)
            {
                if (Map.CanStand(waypoint.x, waypoint.y)) { bot.x = waypoint.x; bot.y = waypoint.y; }
                path.RemoveAt(0);
                return path.Count == 0;
            }

            float dirX = dx / distance, dirY = dy / distance, oldX = bot.x, oldY = bot.y;
            Game.MoveCharacter(bot, dirX * step, dirY * step);
            bot.faceX = dirX; bot.faceY = dirY;

            if (Geometry.Distance(bot.x, bot.y, oldX, oldY) < step * BlockedFraction)
            {
                brain.stuckTime += dt;
                if (brain.stuckTime > StuckSeconds)
                {
                    // try a fresh path to the same destination; after too many tries, give up on it
                    brain.stuckTime = 0; brain.stuckCount++;
                    var destination = path[path.Count - 1];
                    brain.path = brain.stuckCount > GiveUpAfterStucks ? new List<Point>() : Navigation.FindPath(bot.x, bot.y, destination.x, destination.y);
                    if (brain.path.Count == 0) { brain.stuckCount = 0; return true; }
                }
            }
            else brain.stuckTime = 0;
            return false;
        }

        /// <summary>Whether two characters can see each other (no wall in between).</summary>
        public static bool CanSee(Character a, Character b) => Map.HasLineOfSight(a.x, a.y, b.x, b.y);
    }
}
