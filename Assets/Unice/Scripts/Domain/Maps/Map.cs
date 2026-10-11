using System;
using System.Collections.Generic;

namespace Unice
{
    /// <summary>
    /// The current map: rooms, doors, furniture, collision and line of sight (distances in house pixels).
    /// Built from a <see cref="MapDefinition"/> by <see cref="Load"/> — the house loads by default, and <see cref="Load"/>
    /// can swap in another map (quintal, praça) before a match starts. Path finding is in <see cref="Navigation"/>.
    /// </summary>
    public static class Map
    {
        /// <summary>Half the width of a character: how far it keeps from walls and furniture.</summary>
        public const float BodyRadius = 14;
        /// <summary>Doors: the wall gap they cross, how wide they are, and how far they reach into each room.</summary>
        public const float DoorGap = 24, DoorWidth = 64, DoorOverlap = 30;
        /// <summary>How far in front of a hiding spot a character stands (and is drawn) when hidden in it.</summary>
        const float SpotStandOffset = 22;
        /// <summary>Line checks sample a point every this many pixels.</summary>
        const float LineCheckStep = 8;

        public static float WorldW { get; private set; }
        public static float WorldH { get; private set; }
        public static string Id { get; private set; }
        /// <summary>Color of the ground outside the rooms (hex).</summary>
        public static string GroundTint { get; private set; }
        /// <summary>Where the hiders line up when a round starts.</summary>
        public static float SpawnX { get; private set; }
        public static float SpawnY { get; private set; }
        /// <summary>The front door, where the Unice comes in.</summary>
        public static Point Entrance { get; private set; }

        public static Room[] Rooms { get; private set; }
        public static Door[] Doors { get; private set; }
        /// <summary>Every piece of furniture: obstacles, and the hiding spots among them.</summary>
        public static Furniture[] Furniture { get; private set; }
        /// <summary>Only the furniture someone can hide in, in <see cref="Furniture.spotIndex"/> order.</summary>
        public static Furniture[] Spots { get; private set; }
        /// <summary>The ticking clock (its tick plays from there), or null on a map without one.</summary>
        public static Furniture Clock { get; private set; }

        /// <summary>Rooms plus door openings: where a character can be.</summary>
        static RectF[] walkable;

        static Map() { Load(MapHouse.Build()); }

        /// <summary>Rebuilds the whole map from <paramref name="definition"/>. Only call between matches — it wipes hiding-spot state.</summary>
        public static void Load(MapDefinition definition)
        {
            Id = definition.id; WorldW = definition.worldW; WorldH = definition.worldH; GroundTint = definition.groundTint;
            Entrance = new Point(definition.entryX, definition.entryY); SpawnX = definition.spawnX; SpawnY = definition.spawnY;
            Rooms = definition.rooms;

            var doors = new List<Door>();
            foreach (var door in definition.verticalDoors)
                doors.Add(new Door
                {
                    rect = new RectF(door.x - DoorWidth / 2, door.y - DoorOverlap, DoorWidth, DoorGap + 2 * DoorOverlap),
                    centerX = door.x, centerY = door.y + DoorGap / 2, vertical = true,
                });
            foreach (var door in definition.horizontalDoors)
                doors.Add(new Door
                {
                    rect = new RectF(door.x - DoorOverlap, door.y - DoorWidth / 2, DoorGap + 2 * DoorOverlap, DoorWidth),
                    centerX = door.x + DoorGap / 2, centerY = door.y, vertical = false,
                });
            Doors = doors.ToArray();

            var walkableAreas = new List<RectF>();
            foreach (var room in Rooms) walkableAreas.Add(new RectF(room.x, room.y, room.w, room.h));
            foreach (var door in Doors) walkableAreas.Add(door.rect);
            walkable = walkableAreas.ToArray();

            var furniture = new List<Furniture>(); var spots = new List<Furniture>();
            for (int i = 0; i < definition.furniture.Length; i++)
            {
                var prop = definition.furniture[i];
                var piece = new Furniture { id = i, type = prop.type, x = prop.x, y = prop.y, w = prop.w, h = prop.h };
                if (prop.hideSide != '\0')
                {
                    // a hiding spot: the character stands just outside the side it is approached from
                    piece.hideSide = prop.hideSide;
                    switch (piece.hideSide)
                    {
                        case 'n': piece.standX = piece.CenterX; piece.standY = piece.y - SpotStandOffset; break;
                        case 's': piece.standX = piece.CenterX; piece.standY = piece.y + piece.h + SpotStandOffset; break;
                        case 'w': piece.standX = piece.x - SpotStandOffset; piece.standY = piece.CenterY; break;
                        default: piece.standX = piece.x + piece.w + SpotStandOffset; piece.standY = piece.CenterY; break;
                    }
                    piece.spotIndex = spots.Count; spots.Add(piece);
                }
                furniture.Add(piece);
            }
            Furniture = furniture.ToArray(); Spots = spots.ToArray();
            Clock = Array.Find(Furniture, piece => piece.type == "clock");
            Navigation.Build();
        }

        /// <summary>Whether (x, y) is inside a room or door, at least <paramref name="margin"/> away from its edges.</summary>
        public static bool IsWalkable(float x, float y, float margin)
        {
            foreach (var area in walkable)
                if (x >= area.x + margin && x <= area.x + area.w - margin && y >= area.y + margin && y <= area.y + area.h - margin) return true;
            return false;
        }

        /// <summary>Whether (x, y) is inside (or within <paramref name="margin"/> of) any piece of furniture.</summary>
        public static bool IsInsideFurniture(float x, float y, float margin)
        {
            foreach (var piece in Furniture)
                if (x > piece.x - margin && x < piece.x + piece.w + margin && y > piece.y - margin && y < piece.y + piece.h + margin) return true;
            return false;
        }

        /// <summary>Whether a character of radius <paramref name="margin"/> fits at (x, y).</summary>
        public static bool CanStand(float x, float y, float margin = BodyRadius) => IsWalkable(x, y, margin) && !IsInsideFurniture(x, y, margin);

        /// <summary>A character can walk in a straight line from A (ax, ay) to B (bx, by) without bumping into anything.</summary>
        public static bool CanWalkStraight(float ax, float ay, float bx, float by) => IsLineClear(ax, ay, bx, by, canStandAt);

        /// <summary>Nothing but furniture between A and B: walls block sight, furniture doesn't.</summary>
        public static bool HasLineOfSight(float ax, float ay, float bx, float by) => IsLineClear(ax, ay, bx, by, canSeeThrough);

        static readonly Func<float, float, bool> canStandAt = (x, y) => CanStand(x, y);
        static readonly Func<float, float, bool> canSeeThrough = (x, y) => IsWalkable(x, y, 0);

        /// <summary>Samples the segment A→B and checks <paramref name="isClear"/> at every sample.</summary>
        static bool IsLineClear(float ax, float ay, float bx, float by, Func<float, float, bool> isClear)
        {
            float length = Geometry.Distance(ax, ay, bx, by);
            int samples = Math.Max(1, (int)MathF.Ceiling(length / LineCheckStep));
            for (int i = 0; i <= samples; i++)
            {
                float along = (float)i / samples;   // 0 at A, 1 at B
                if (!isClear(ax + (bx - ax) * along, ay + (by - ay) * along)) return false;
            }
            return true;
        }

        /// <summary>The room (x, y) is in, or the closest one when it is in a doorway or outside.</summary>
        public static Room RoomAt(float x, float y)
        {
            foreach (var room in Rooms) if (x >= room.x && x <= room.x + room.w && y >= room.y && y <= room.y + room.h) return room;
            Room closest = Rooms[0]; float closestDistance = float.MaxValue;
            foreach (var room in Rooms)
            {
                float distance = Geometry.Distance(x, y, Geometry.Clamp(x, room.x, room.x + room.w), Geometry.Clamp(y, room.y, room.y + room.h));
                if (distance < closestDistance) { closestDistance = distance; closest = room; }
            }
            return closest;
        }

        /// <summary>Distance from (x, y) to the closest edge of <paramref name="piece"/> (0 when inside).</summary>
        public static float DistanceToFurniture(Furniture piece, float x, float y)
        {
            float dx = MathF.Max(MathF.Max(piece.x - x, 0), x - piece.x - piece.w);
            float dy = MathF.Max(MathF.Max(piece.y - y, 0), y - piece.y - piece.h);
            return MathF.Sqrt(dx * dx + dy * dy);
        }
    }
}
