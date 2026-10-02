using System.Collections.Generic;

namespace Unice
{
    /// <summary>
    /// Data shape for a swappable map (house, quintal, praça, …): the same primitives Map.cs's
    /// Load(...) turns into Rooms/Doors/Furniture/Spots/the nav graph. Display text (room names,
    /// hiding-spot phrases, the map's own name) is never stored here — it lives in Core's Loc
    /// table, keyed by id/type, same as Map.cs's Room/Furn.
    /// </summary>
    public sealed class MapDefinition
    {
        public string id;
        public float worldW, worldH;
        public Room[] zones;               // reuses Domain's Room type directly: id + rect, nothing else
        public MapDoor[] doorsV, doorsH;    // vertical/horizontal openings, same split as Map.cs's old DV/DH
        public MapProp[] props;            // decorative obstacles AND hiding spots in one list
        public float entryX, entryY;       // where the Unice walks in from (Map.DoorIn)
        public float spawnX, spawnY;       // where hiders line up at the start of a round
        public string groundTint;          // flat floor color used where there's no painted floor art
    }

    /// <summary>One opening between zones — x/y of its center, same idea as the old DV/DH float pairs.</summary>
    public readonly struct MapDoor
    {
        public readonly float x, y;
        public MapDoor(float x, float y) { this.x = x; this.y = y; }
    }

    /// <summary>
    /// One prop: a decorative obstacle, or a hiding spot when <see cref="side"/> is set (non-'\0').
    /// Its flavor phrase when hideable lives in Loc as "spot." + type.
    /// </summary>
    public sealed class MapProp
    {
        public string type;    // display/art key (e.g. "hedge", "shed") — also the Loc "spot." key when hideable
        public float x, y, w, h;
        public char side;      // '\0' = decorative only; else n/s/e/w approach side for the hiding pose
        public MapProp(string type, float x, float y, float w, float h, char side = '\0')
        { this.type = type; this.x = x; this.y = y; this.w = w; this.h = h; this.side = side; }
    }

    /// <summary>Registry of every playable map, by id.</summary>
    public static class Maps
    {
        public static readonly Dictionary<string, MapDefinition> All = new Dictionary<string, MapDefinition>
        {
            { MapHouse.Id, MapHouse.Build() },
            { MapQuintal.Id, MapQuintal.Build() },
            { MapPraca.Id, MapPraca.Build() },
        };
        public static readonly string[] AllIds = { MapHouse.Id, MapQuintal.Id, MapPraca.Id };
        public static MapDefinition ById(string id) => All.TryGetValue(id, out var m) ? m : All[MapHouse.Id];
    }
}
