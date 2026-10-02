using System.Collections.Generic;

namespace Unice
{
    /// <summary>
    /// Data shape for a swappable map (house, quintal, praça, …). Mirrors what Map.cs's static
    /// constructor bakes in today (Rooms, DV/DH door tables, furniture tuples, WorldW/H, DoorIn) —
    /// not a new concept, just the same primitives pulled out of a static class into instance data
    /// so Game.cs could one day hold "which map" the way it already holds `rounds`/`bots`.
    /// Not wired into Map.cs/Game.cs yet; see the integration note in the hand-off report.
    /// </summary>
    public sealed class MapDefinition
    {
        public string id, displayName;
        public float worldW, worldH;
        public MapZone[] zones;
        public MapDoor[] doorsV, doorsH;   // same split as today's Map.DV/DH: vertical vs horizontal openings
        public MapProp[] props;            // decorative obstacles AND hiding spots in one list; spot == null means "not hideable"
        public float entryX, entryY;       // where the Unice walks in from (today's Map.DoorIn)
        public string groundTint;          // fallback hex color for the floor when there's no painted floor art (quintal/praça today)
    }

    /// <summary>One room/zone. Same shape as today's Room (Map.cs), kept separate so this file never has to touch it.</summary>
    public sealed class MapZone
    {
        public string id, name, inPhrase;
        public float x, y, w, h;
        public MapZone(string id, string name, string inPhrase, float x, float y, float w, float h)
        { this.id = id; this.name = name; this.inPhrase = inPhrase; this.x = x; this.y = y; this.w = w; this.h = h; }
    }

    /// <summary>One opening between zones — same idea as today's DV/DH float pairs, just named.</summary>
    public readonly struct MapDoor
    {
        public readonly float x, y;
        public MapDoor(float x, float y) { this.x = x; this.y = y; }
    }

    /// <summary>
    /// One prop: a decorative obstacle, or a hiding spot when <see cref="spot"/> is set.
    /// Mirrors today's inline `object[] F` furniture tuples in Map.cs, as a real type instead of a loosely-typed array.
    /// </summary>
    public sealed class MapProp
    {
        public string type;    // display/art key (e.g. "hedge", "shed") — a render *technique* to pick, not necessarily a PNG filename
        public float x, y, w, h;
        public string spot;    // null = decorative only; else the flavor phrase shown while hiding there ("atrás da cerca viva")
        public char side;      // approach side for the hiding pose: n/s/e/w (ignored when spot == null)
        public MapProp(string type, float x, float y, float w, float h, string spot = null, char side = 'n')
        { this.type = type; this.x = x; this.y = y; this.w = w; this.h = h; this.spot = spot; this.side = side; }
    }

    /// <summary>Registry of playable maps. Today only used by MapQuintal/MapPraca to expose themselves; Map.cs (the house) is not in here yet — see hand-off report.</summary>
    public static class Maps
    {
        public static readonly Dictionary<string, MapDefinition> All = new Dictionary<string, MapDefinition>
        {
            { MapQuintal.Id, MapQuintal.Build() },
            { MapPraca.Id, MapPraca.Build() },
        };
    }
}
