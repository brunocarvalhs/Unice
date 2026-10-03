using System.Collections.Generic;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// The map's static sprites: floors, thresholds, walls and furniture. The house has painted art (Resources/Art,
    /// placed by the art manifest); anything without art (other maps, new pieces) is drawn as flat tinted shapes.
    /// Walls and furniture are "occluders": they turn see-through when the player walks behind them.
    /// </summary>
    sealed class MapScenery
    {
        /// <summary>How tall each furniture type stands (house pixels), for occlusion: you're "behind" it within this height.</summary>
        static readonly Dictionary<string, float> FurnitureHeights = new Dictionary<string, float>
        {
            { "bed", 24 }, { "wardrobe", 56 }, { "dresser", 36 }, { "tub", 24 }, { "sink", 32 }, { "bunk", 50 }, { "chest", 22 }, { "dollhouse", 44 },
            { "shelves", 56 }, { "desk", 28 }, { "clock", 58 }, { "armchair", 28 }, { "closet", 52 }, { "commode", 30 }, { "sofa", 26 }, { "fireplace", 50 },
            { "piano", 32 }, { "table", 24 }, { "cabinet", 54 }, { "sinkcab", 30 }, { "stove", 32 }, { "ktable", 26 }, { "ferns", 34 }, { "planters", 20 },
            { "stairs", 42 }, { "coats", 54 }, { "barrel", 34 }, { "pshelves", 52 }, { "hamper", 28 }, { "tank", 28 }
        };
        const float DefaultFurnitureHeight = 28;
        const float SeeThroughAlpha = .4f;

        /// <summary>Something you can walk behind: its sprite and its footprint plus height.</summary>
        struct Occluder { public SpriteRenderer renderer; public float x, y, w, h, height; }

        readonly Transform root;
        readonly List<Occluder> occluders = new List<Occluder>();
        string builtMapId;

        public MapScenery(Transform parent)
        {
            root = new GameObject("Static").transform; root.SetParent(parent, false);
            Build();
        }

        /// <summary>Tears down and rebuilds everything when the active map changed.</summary>
        public void Sync()
        {
            if (builtMapId == Map.Id) return;
            var children = new List<GameObject>();
            for (int i = 0; i < root.childCount; i++) children.Add(root.GetChild(i).gameObject);
            foreach (var child in children) Object.Destroy(child);
            occluders.Clear();
            Build();
        }

        void Build()
        {
            builtMapId = Map.Id;
            var background = Art.CreateSpriteRenderer("background", root, Art.WhiteSprite, -30000); background.color = Art.HexColor("#121212");
            background.transform.localPosition = Art.ToWorld(14, 14);
            background.transform.localScale = new Vector3((Map.WorldW - 28) / Art.PxPerUnit, (Map.WorldH - 28) / Art.PxPerUnit, 1);

            // painted art is hand-authored for the house's exact room/furniture ids and indices — it only applies to the house
            bool isHouse = Map.Id == MapHouse.Id;
            var groundTint = Art.HexColor(Map.GroundTint);
            BuildFloors(isHouse, groundTint);
            BuildThresholds();
            if (isHouse) BuildHouseWalls();
            BuildFurniture(isHouse, groundTint);
        }

        void BuildFloors(bool isHouse, Color groundTint)
        {
            var paintedFloors = new HashSet<string>();
            if (isHouse)
                foreach (var floor in Art.Manifest.floors)
                {
                    paintedFloors.Add(floor.id);
                    var sprite = Art.CreateSpriteRenderer("floor_" + floor.id, root, Art.LoadSprite(floor.file, new Vector2(0, 1), Art.Manifest.worldScale), -20000);
                    sprite.transform.localPosition = Art.ToWorld(floor.x, floor.y);
                }
            // rooms without painted floor art get a flat tint
            foreach (var room in Map.Rooms)
            {
                if (paintedFloors.Contains(room.id)) continue;
                var sprite = Art.CreateSpriteRenderer("floor_" + room.id, root, Art.WhiteSprite, -20000); sprite.color = groundTint;
                sprite.transform.localPosition = Art.ToWorld(room.x, room.y);
                sprite.transform.localScale = new Vector3(room.w / Art.PxPerUnit, room.h / Art.PxPerUnit, 1);
            }
        }

        /// <summary>A darker strip on the floor across each doorway.</summary>
        void BuildThresholds()
        {
            foreach (var door in Map.Doors)
            {
                var sprite = Art.CreateSpriteRenderer("threshold", root, Art.WhiteSprite, -19999); sprite.color = Art.HexColor("#3e2a1e");
                var strip = door.vertical
                    ? new RectF(door.rect.x, door.rect.y + Map.DoorOverlap - 1, door.rect.w, Map.DoorGap + 2)
                    : new RectF(door.rect.x + Map.DoorOverlap - 1, door.rect.y, Map.DoorGap + 2, door.rect.h);
                sprite.transform.localPosition = Art.ToWorld(strip.x, strip.y);
                sprite.transform.localScale = new Vector3(strip.w / Art.PxPerUnit, strip.h / Art.PxPerUnit, 1);
            }
        }

        void BuildHouseWalls()
        {
            var doormat = Art.CreateSpriteRenderer("mat", root, Art.WhiteSprite, -19998); doormat.color = Art.HexColor("#4a2e1f");
            doormat.transform.localPosition = Art.ToWorld(784, 1128); doormat.transform.localScale = new Vector3(.8f, .1f, 1);
            foreach (var wall in Art.Manifest.walls)
            {
                var sprite = Art.CreateSpriteRenderer("wall_" + wall.i, root, Art.LoadSprite(wall.file, new Vector2(0, 0), Art.Manifest.worldScale), Art.SortingOrder(wall.y + wall.h, 0));
                sprite.transform.localPosition = Art.ToWorld(wall.x, wall.y + wall.h);
                occluders.Add(new Occluder { renderer = sprite, x = wall.x, y = wall.y, w = wall.w, h = wall.h, height = wall.hgt });
            }
        }

        void BuildFurniture(bool isHouse, Color groundTint)
        {
            var paintedFurniture = new HashSet<int>();
            if (isHouse)
                foreach (var art in Art.Manifest.furn)
                {
                    paintedFurniture.Add(art.i);
                    var piece = Map.Furniture[art.i];
                    var pivot = new Vector2(art.ax / art.pw, 1 - art.ay / art.ph);
                    var sprite = Art.CreateSpriteRenderer("furn_" + piece.type, root, Art.LoadSprite(art.file, pivot, Art.Manifest.worldScale), Art.SortingOrder(piece.y + piece.h, 1));
                    sprite.transform.localPosition = Art.ToWorld(piece.x, piece.y + piece.h);
                    AddOccluder(sprite, piece);
                }
            // furniture without painted art gets a flat tinted silhouette
            var silhouetteTint = Color.Lerp(groundTint, Color.black, .35f);
            for (int i = 0; i < Map.Furniture.Length; i++)
            {
                if (paintedFurniture.Contains(i)) continue;
                var piece = Map.Furniture[i];
                var sprite = Art.CreateSpriteRenderer("furn_" + piece.type, root, Art.WhiteSprite, Art.SortingOrder(piece.y + piece.h, 1)); sprite.color = silhouetteTint;
                sprite.transform.localPosition = Art.ToWorld(piece.x, piece.y);
                sprite.transform.localScale = new Vector3(piece.w / Art.PxPerUnit, piece.h / Art.PxPerUnit, 1);
                AddOccluder(sprite, piece);
            }
        }

        void AddOccluder(SpriteRenderer sprite, Furniture piece)
        {
            float height = FurnitureHeights.TryGetValue(piece.type, out var known) ? known : DefaultFurnitureHeight;
            occluders.Add(new Occluder { renderer = sprite, x = piece.x, y = piece.y, w = piece.w, h = piece.h, height = height });
        }

        /// <summary>Every frame: occluders in front of the player (it stands behind their footprint) become see-through.</summary>
        public void UpdateOcclusion(Character me, bool live)
        {
            bool playerOnMap = me != null && live && !me.outOfRound && me.hidingSpot == null && me.x > 0;
            foreach (var occluder in occluders)
            {
                bool behindIt = playerOnMap && me.y < occluder.y + occluder.h && me.y > occluder.y - occluder.height - 4 && me.x + 12 > occluder.x && me.x - 12 < occluder.x + occluder.w;
                float alpha = behindIt ? SeeThroughAlpha : 1;
                var color = occluder.renderer.color;
                if (!Mathf.Approximately(color.a, alpha)) { color.a = alpha; occluder.renderer.color = color; }
            }
        }
    }
}
