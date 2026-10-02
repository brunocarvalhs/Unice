namespace Unice
{
    /// <summary>
    /// Concept data for the town-square map: a plaza after the fair's closed for the night. Same
    /// world scale as the house (1720×1180). The Unice enters from the promenade (the street),
    /// not a front door — fits a public-space setting better than a house's DoorIn.
    /// Not wired into Game.cs yet — see MapDefinition.cs for the integration note.
    /// </summary>
    public static class MapPraca
    {
        public const string Id = "praca";

        public static MapDefinition Build() => new MapDefinition
        {
            id = Id,
            displayName = "A Praça",
            worldW = 1720,
            worldH = 1180,
            groundTint = "#6b6a63", // pavement
            entryX = 860, entryY = 475, // off the promenade, into the square
            zones = new[]
            {
                new MapZone("market", "Feira Coberta",        "na feira coberta",        40,   40,  540, 380),
                new MapZone("church", "Adro da Igreja",       "no adro da igreja",       620,  40,  480, 380),
                new MapZone("cafes",  "Varanda dos Cafés",    "na varanda dos cafés",    1140, 40,  540, 380),
                new MapZone("walk",   "Calçadão",             "no calçadão",             40,   420, 1640,100),
                new MapZone("square", "Jardim da Praça",      "no jardim da praça",      40,   560, 540, 580),
                new MapZone("fountain","Praça da Fonte",      "perto da fonte",          620,  560, 480, 580),
                new MapZone("park",   "Parquinho",            "no parquinho",            1140, 560, 540, 580),
            },
            doorsV = new[]
            {
                new MapDoor(620, 230), new MapDoor(1140, 230),
                new MapDoor(620, 850), new MapDoor(1140, 850),
            },
            doorsH = new[]
            {
                new MapDoor(300, 420), new MapDoor(860, 420), new MapDoor(1400, 420),
                new MapDoor(300, 560), new MapDoor(860, 560), new MapDoor(1400, 560),
            },
            props = new[]
            {
                // --- hiding spots (17) ---
                new MapProp("kiosk",      80,   140, 140, 110, "dentro da banca de jornal fechada",   's'),
                new MapProp("fruitstall", 340,  140, 150, 90,  "atrás da barraca de frutas",          'e'),
                new MapProp("hotdogcart", 760,  440, 110, 70,  "atrás do carrinho de cachorro-quente",'n'),
                new MapProp("phonebooth", 1360, 440, 70,  70,  "dentro da cabine telefônica",         's'),
                new MapProp("churchbush", 680,  140, 100, 90,  "atrás do arbusto da igreja",          'n'),
                new MapProp("belldoor",   980,  300, 80,  110, "atrás da porta do sino",              'e'),
                new MapProp("cratestack", 1180, 140, 100, 90,  "atrás das caixas do café",            's'),
                new MapProp("umbrellas",  1500, 140, 120, 90,  "embaixo da pilha de guarda-sóis",     'w'),
                new MapProp("fountainrim",700,  700, 340, 340, "atrás da borda da fonte",             'n'),
                new MapProp("statue",     800,  640, 140, 70,  "atrás do pedestal da estátua",        'w'),
                new MapProp("hedge",      80,   600, 140, 90,  "atrás da cerca-viva do jardim",       'n'),
                new MapProp("gazebo",     340,  940, 160, 130, "dentro do coreto",                    's'),
                new MapProp("benchbush",  80,   1000,120, 90,  "atrás do banco com arbustos",         'w'),
                new MapProp("slide",      1160, 620, 140, 70,  "debaixo do escorregador",             's'),
                new MapProp("sandbox",    1400, 620, 150, 110, "atrás da caixa de areia",             'e'),
                new MapProp("playbush",   1180, 940, 110, 90,  "atrás do arbusto do parquinho",       'n'),
                new MapProp("clocktower", 620,  640, 70,  220, "atrás da base da torre do relógio",   'n'),
                // --- decorative only (8) ---
                new MapProp("lamppost1",  300,  420, 20,  60),
                new MapProp("lamppost2",  860,  420, 20,  60),
                new MapProp("lamppost3",  1400, 420, 20,  60),
                new MapProp("bench1",     200,  480, 100, 30),
                new MapProp("bench2",     1250, 480, 100, 30),
                new MapProp("trashbin",   500,  480, 30,  30),
                new MapProp("flagpole",   1560, 940, 20,  140),
                new MapProp("pigeonstatue",980,  980, 40,  40),
            },
        };
    }
}
