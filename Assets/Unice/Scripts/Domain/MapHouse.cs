namespace Unice
{
    /// <summary>
    /// The house: the original 12-room layout plus a 4th row (porão, oficina, depósito) added below.
    /// The original 30 furniture entries keep their exact array order/coordinates — Art/manifest.json's
    /// furn[].i indexes into Map.Furniture positionally, so reordering or inserting among them would
    /// point existing art at the wrong piece. New furniture is appended after index 29 instead.
    /// Some originally-decorative pieces (dresser, dollhouse, shelves, armchair, commode, fireplace,
    /// cabinet, ktable, planters, pshelves, tank) gained a hiding side; sink/stove didn't (too small).
    /// </summary>
    public static class MapHouse
    {
        public const string Id = "house";

        public static MapDefinition Build() => new MapDefinition
        {
            id = Id,
            worldW = 1720,
            worldH = 1454,   // 1180 + a 4th row (250 tall) + its gap
            groundTint = "#2e2319",
            entryX = 824, entryY = 1085,
            spawnX = 620, spawnY = 474,
            zones = new[]
            {
                new Room("master", 40, 40, 440, 350),
                new Room("bath", 504, 40, 220, 350),
                new Room("kids", 748, 40, 420, 350),
                new Room("library", 1192, 40, 488, 350),
                new Room("hall", 40, 414, 1640, 120),
                new Room("living", 40, 558, 520, 342),
                new Room("dining", 584, 558, 480, 342),
                new Room("kitchen", 1088, 558, 592, 342),
                new Room("estufa", 40, 924, 520, 216),
                new Room("entrance", 584, 924, 480, 216),
                new Room("pantry", 1088, 924, 300, 216),
                new Room("laundry", 1412, 924, 268, 216),
                // 4th row, new this pass
                new Room("cellar", 40, 1164, 540, 250),
                new Room("workshop", 604, 1164, 520, 250),
                new Room("storage", 1148, 1164, 532, 250),
            },
            doorsV = new[]
            {
                new MapDoor(260, 390), new MapDoor(614, 390), new MapDoor(958, 390), new MapDoor(1436, 390),
                new MapDoor(300, 534), new MapDoor(824, 534), new MapDoor(1384, 534),
                new MapDoor(300, 900), new MapDoor(824, 900), new MapDoor(1238, 900), new MapDoor(1546, 900),
                // row3 -> row4
                new MapDoor(300, 1140), new MapDoor(824, 1140), new MapDoor(1300, 1140),
            },
            doorsH = new[]
            {
                new MapDoor(480, 215), new MapDoor(1168, 215),
                new MapDoor(560, 729), new MapDoor(1064, 729),
                new MapDoor(560, 1032), new MapDoor(1064, 1032), new MapDoor(1388, 1032),
                // within row4
                new MapDoor(580, 1289), new MapDoor(1124, 1289),
            },
            props = new[]
            {
                // --- original 30, same order/coordinates as before (manifest furn[].i depends on this) ---
                new MapProp("bed", 170, 40, 150, 175, 's'),
                new MapProp("wardrobe", 40, 240, 58, 120, 'e'),
                new MapProp("dresser", 390, 40, 80, 42, 's'),
                new MapProp("tub", 600, 40, 124, 66, 's'),
                new MapProp("sink", 504, 56, 50, 56),
                new MapProp("bunk", 748, 40, 92, 175, 'e'),
                new MapProp("chest", 990, 40, 100, 54, 's'),
                new MapProp("dollhouse", 1060, 296, 92, 70, 'w'),
                new MapProp("shelves", 1250, 40, 380, 40, 's'),
                new MapProp("desk", 1350, 180, 160, 70, 's'),
                new MapProp("clock", 1624, 150, 56, 64, 'w'),
                new MapProp("armchair", 1230, 300, 60, 60, 'e'),
                new MapProp("closet", 1090, 414, 90, 34, 's'),
                new MapProp("commode", 480, 414, 80, 30, 's'),
                new MapProp("sofa", 150, 650, 200, 56, 'n'),
                new MapProp("fireplace", 40, 760, 40, 110, 'e'),
                new MapProp("piano", 400, 790, 140, 90, 'n'),
                new MapProp("table", 700, 680, 250, 100, 's'),
                new MapProp("cabinet", 584, 558, 46, 100, 'e'),
                new MapProp("sinkcab", 1150, 558, 150, 44, 's'),
                new MapProp("stove", 1570, 558, 110, 60),
                new MapProp("ktable", 1340, 700, 130, 80, 's'),
                new MapProp("ferns", 40, 1030, 110, 110, 'e'),
                new MapProp("planters", 380, 1090, 170, 44, 'n'),
                new MapProp("stairs", 900, 924, 110, 76, 's'),
                new MapProp("coats", 620, 1090, 110, 50, 'n'),
                new MapProp("barrel", 1310, 950, 60, 60, 'w'),
                new MapProp("pshelves", 1100, 1098, 280, 42, 'n'),
                new MapProp("hamper", 1606, 1060, 66, 66, 'w'),
                new MapProp("tank", 1600, 930, 74, 56, 'w'),
                // --- new, 4th row (appended, indices 30+) ---
                new MapProp("winerack", 70, 1190, 160, 80, 's'),
                new MapProp("coalbin", 380, 1190, 140, 80, 's'),
                new MapProp("crates", 200, 1320, 120, 70),
                new MapProp("toolchest", 640, 1190, 160, 80, 's'),
                new MapProp("sawhorse", 860, 1320, 140, 60),
                new MapProp("oldtrunk", 1180, 1190, 160, 80, 's'),
                new MapProp("boxes", 1420, 1190, 140, 90, 's'),
                new MapProp("mirror", 1300, 1320, 100, 60),
            },
        };
    }
}
