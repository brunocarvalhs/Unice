using System;
using System.Collections.Generic;

namespace Unice
{
    /// <summary>The game's dice: one shared random generator for draws, bot decisions and kits.</summary>
    public static class GameRandom
    {
        static readonly Random random = new Random();

        /// <summary>A random float in [min, max).</summary>
        public static float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        /// <summary>A random index in [0, count).</summary>
        public static int Index(int count) => random.Next(count);

        public static T Pick<T>(IList<T> items) => items[random.Next(items.Count)];

        /// <summary>Shuffles in place (Fisher–Yates).</summary>
        public static void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
