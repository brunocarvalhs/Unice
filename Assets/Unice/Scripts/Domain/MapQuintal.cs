namespace Unice
{
    /// <summary>
    /// Concept data for the backyard map: overgrown garden at dusk. Same world scale as the house
    /// (1720×1180) for comparable round length. The Unice enters from the back door (top of the
    /// patio) instead of a front door — small thematic twist, costs nothing.
    /// </summary>
    public static class MapQuintal
    {
        public const string Id = "quintal";

        public static MapDefinition Build() => new MapDefinition
        {
            id = Id,
            worldW = 1720,
            worldH = 1180,
            groundTint = "#4a7c3f", // grass
            entryX = 860, entryY = 45, // back door, top-center of the patio
            spawnX = 860, spawnY = 750,
            zones = new[]
            {
                new Room("front", 40, 40, 540, 380),
                new Room("patio", 620, 40, 480, 380),
                new Room("side", 1140, 40, 540, 380),
                new Room("path", 40, 420, 1640, 100),
                new Room("garden", 40, 560, 540, 580),
                new Room("yard", 620, 560, 480, 580),
                new Room("shed", 1140, 560, 540, 580),
            },
            doorsV = new[]
            {
                new MapDoor(620, 230), new MapDoor(1140, 230),   // front|patio, patio|side (top row)
                new MapDoor(620, 850), new MapDoor(1140, 850),   // garden|yard, yard|shed (bottom row)
            },
            doorsH = new[]
            {
                new MapDoor(300, 420), new MapDoor(860, 420), new MapDoor(1400, 420),   // top row -> path
                new MapDoor(300, 560), new MapDoor(860, 560), new MapDoor(1400, 560),   // path -> bottom row
            },
            props = new[]
            {
                // --- hiding spots (17) ---
                new MapProp("hedge",      80,   140, 140, 90,  'n'),
                new MapProp("bush",       1180, 140, 100, 90,  'e'),
                new MapProp("gatebush",   760,  440, 100, 70,  'n'),
                new MapProp("basket",     660,  80,  90,  70,  'n'),
                new MapProp("deck",       640,  300, 260, 60,  's'),
                new MapProp("swing",      1500, 300, 110, 90,  's'),
                new MapProp("compost",    80,   600, 100, 80,  'n'),
                new MapProp("pots",       460,  600, 90,  90,  'e'),
                new MapProp("wheelbarrow",80,   980, 120, 70,  'w'),
                new MapProp("treehouse",  820,  600, 140, 110, 'n'),
                new MapProp("doghouse",   660,  980, 100, 80,  'w'),
                new MapProp("bigbush",    980,  940, 120, 110, 'w'),
                new MapProp("shed",       1560, 600, 110, 160, 's'),
                new MapProp("woodpile",   1160, 980, 110, 80,  'e'),
                new MapProp("tarpcar",    1340, 980, 180, 120, 's'),
                new MapProp("hammock",    1160, 620, 140, 60,  'n'),
                new MapProp("rosebush",   380,  140, 90,  90,  'n'),
                // --- decorative only (8) ---
                new MapProp("fencepost",  40,   40,  20,  340),
                new MapProp("birdbath",   420,  200, 50,  50),
                new MapProp("gnome",      980,  200, 30,  40),
                new MapProp("mailbox",    40,   420, 40,  40),
                new MapProp("picnictable",1250, 300, 160, 70),
                new MapProp("well",       260,  660, 90,  90),
                new MapProp("lanternpost",900,  440, 20,  60),
                new MapProp("clothesline",1480, 80,  160, 20),
            },
        };
    }
}
