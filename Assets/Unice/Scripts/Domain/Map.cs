using System;
using System.Collections.Generic;

namespace Unice
{
    /// <summary>A room's display name/phrase lives in Core's Loc table, keyed by <see cref="id"/> — Domain stays engine/text-free.</summary>
    public sealed class Room
    {
        public string id;
        public float x, y, w, h;
        public Room(string id, float x, float y, float w, float h) { this.id = id; this.x = x; this.y = y; this.w = w; this.h = h; }
    }

    public struct RectF
    {
        public float x, y, w, h;
        public RectF(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
    }

    public sealed class Door
    {
        public RectF r; public float cx, cy; public bool vertical;
    }

    /// <summary>A piece of furniture. When <see cref="spot"/> is set, a child can hide in it.</summary>
    /// <summary>When hideable, its flavor phrase ("under the bed") lives in Core's Loc table as "spot." + type.</summary>
    public sealed class Furn
    {
        public int id, si = -1;
        public string type;
        public char side;
        public float x, y, w, h, ax, ay;
        // round state (host)
        public Ent occ, res;
        public float checkedAt = -99;
        public float Cx => x + w / 2;
        public float Cy => y + h / 2;
    }

    /// <summary>The house: rooms, doors, furniture, collision, line of sight and path finding (same layout as the web version).</summary>
    public static class Map
    {
        public const float R = 14, GAP = 24, DW = 64, OV = 30, WorldW = 1720, WorldH = 1180;

        public static readonly Room[] Rooms =
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
        };

        public static readonly float[,] DV = { { 260, 390 }, { 614, 390 }, { 958, 390 }, { 1436, 390 }, { 300, 534 }, { 824, 534 }, { 1384, 534 }, { 300, 900 }, { 824, 900 }, { 1238, 900 }, { 1546, 900 } };
        public static readonly float[,] DH = { { 480, 215 }, { 1168, 215 }, { 560, 729 }, { 1064, 729 }, { 560, 1032 }, { 1064, 1032 }, { 1388, 1032 } };

        public static readonly Door[] Doors;
        static readonly RectF[] walk;
        public static readonly Furn[] Furniture;
        public static readonly Furn[] Spots;
        public static readonly P DoorIn = new P(824, 1085);

        // navigation graph
        public static readonly List<P> Nodes = new List<P>();
        static List<(int to, float d)>[] adj;

        static Map()
        {
            var doors = new List<Door>();
            for (int i = 0; i < DV.GetLength(0); i++)
            {
                float x = DV[i, 0], y = DV[i, 1];
                doors.Add(new Door { r = new RectF(x - DW / 2, y - OV, DW, GAP + 2 * OV), cx = x, cy = y + GAP / 2, vertical = true });
            }
            for (int i = 0; i < DH.GetLength(0); i++)
            {
                float x = DH[i, 0], y = DH[i, 1];
                doors.Add(new Door { r = new RectF(x - OV, y - DW / 2, GAP + 2 * OV, DW), cx = x + GAP / 2, cy = y, vertical = false });
            }
            Doors = doors.ToArray();
            var wl = new List<RectF>();
            foreach (var r in Rooms) wl.Add(new RectF(r.x, r.y, r.w, r.h));
            foreach (var d in Doors) wl.Add(d.r);
            walk = wl.ToArray();

            object[][] F =
            {
                new object[] { "bed", 170, 40, 150, 175, 's' },
                new object[] { "wardrobe", 40, 240, 58, 120, 'e' },
                new object[] { "dresser", 390, 40, 80, 42 },
                new object[] { "tub", 600, 40, 124, 66, 's' },
                new object[] { "sink", 504, 56, 50, 56 },
                new object[] { "bunk", 748, 40, 92, 175, 'e' },
                new object[] { "chest", 990, 40, 100, 54, 's' },
                new object[] { "dollhouse", 1060, 296, 92, 70 },
                new object[] { "shelves", 1250, 40, 380, 40 },
                new object[] { "desk", 1350, 180, 160, 70, 's' },
                new object[] { "clock", 1624, 150, 56, 64, 'w' },
                new object[] { "armchair", 1230, 300, 60, 60 },
                new object[] { "closet", 1090, 414, 90, 34, 's' },
                new object[] { "commode", 480, 414, 80, 30 },
                new object[] { "sofa", 150, 650, 200, 56, 'n' },
                new object[] { "fireplace", 40, 760, 40, 110 },
                new object[] { "piano", 400, 790, 140, 90, 'n' },
                new object[] { "table", 700, 680, 250, 100, 's' },
                new object[] { "cabinet", 584, 558, 46, 100 },
                new object[] { "sinkcab", 1150, 558, 150, 44, 's' },
                new object[] { "stove", 1570, 558, 110, 60 },
                new object[] { "ktable", 1340, 700, 130, 80 },
                new object[] { "ferns", 40, 1030, 110, 110, 'e' },
                new object[] { "planters", 380, 1090, 170, 44 },
                new object[] { "stairs", 900, 924, 110, 76, 's' },
                new object[] { "coats", 620, 1090, 110, 50, 'n' },
                new object[] { "barrel", 1310, 950, 60, 60, 'w' },
                new object[] { "pshelves", 1100, 1098, 280, 42 },
                new object[] { "hamper", 1606, 1060, 66, 66, 'w' },
                new object[] { "tank", 1600, 930, 74, 56 },
            };
            var furn = new List<Furn>(); var spots = new List<Furn>();
            for (int i = 0; i < F.Length; i++)
            {
                var a = F[i];
                var f = new Furn { id = i, type = (string)a[0], x = Convert.ToSingle(a[1]), y = Convert.ToSingle(a[2]), w = Convert.ToSingle(a[3]), h = Convert.ToSingle(a[4]) };
                if (a.Length > 5)
                {
                    f.side = (char)a[5];
                    const float o = 22;
                    switch (f.side)
                    {
                        case 'n': f.ax = f.Cx; f.ay = f.y - o; break;
                        case 's': f.ax = f.Cx; f.ay = f.y + f.h + o; break;
                        case 'w': f.ax = f.x - o; f.ay = f.Cy; break;
                        default: f.ax = f.x + f.w + o; f.ay = f.Cy; break;
                    }
                    f.si = spots.Count; spots.Add(f);
                }
                furn.Add(f);
            }
            Furniture = furn.ToArray(); Spots = spots.ToArray();
            BuildGraph();
        }

        public static bool InWalk(float x, float y, float m)
        {
            foreach (var r in walk) if (x >= r.x + m && x <= r.x + r.w - m && y >= r.y + m && y <= r.y + r.h - m) return true;
            return false;
        }
        public static bool InFurn(float x, float y, float m)
        {
            foreach (var f in Furniture) if (x > f.x - m && x < f.x + f.w + m && y > f.y - m && y < f.y + f.h + m) return true;
            return false;
        }
        public static bool CanStand(float x, float y, float m = R) => InWalk(x, y, m) && !InFurn(x, y, m);

        static bool ClearLine(float ax, float ay, float bx, float by, Func<float, float, bool> test)
        {
            float d = U.Dist(ax, ay, bx, by); int n = Math.Max(1, (int)MathF.Ceiling(d / 8));
            for (int i = 0; i <= n; i++) { float t = (float)i / n; if (!test(ax + (bx - ax) * t, ay + (by - ay) * t)) return false; }
            return true;
        }
        static readonly Func<float, float, bool> standTest = (x, y) => CanStand(x, y);
        static readonly Func<float, float, bool> sightTest = (x, y) => InWalk(x, y, 0);
        public static bool WalkLine(float ax, float ay, float bx, float by) => ClearLine(ax, ay, bx, by, standTest);
        public static bool SightLine(float ax, float ay, float bx, float by) => ClearLine(ax, ay, bx, by, sightTest);

        public static Room RoomOf(float x, float y)
        {
            foreach (var r in Rooms) if (x >= r.x && x <= r.x + r.w && y >= r.y && y <= r.y + r.h) return r;
            Room best = Rooms[0]; float bd = float.MaxValue;
            foreach (var r in Rooms)
            {
                float d = U.Dist(x, y, U.Clamp(x, r.x, r.x + r.w), U.Clamp(y, r.y, r.y + r.h));
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        public static float RectDist(Furn f, float x, float y)
        {
            float dx = MathF.Max(MathF.Max(f.x - x, 0), x - f.x - f.w), dy = MathF.Max(MathF.Max(f.y - y, 0), y - f.y - f.h);
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        static void BuildGraph()
        {
            for (float y = 64; y < WorldH; y += 46) for (float x = 64; x < WorldW; x += 46) if (CanStand(x, y, R + 3)) Nodes.Add(new P(x, y));
            foreach (var d in Doors)
            {
                Nodes.Add(new P(d.cx, d.cy));
                foreach (float s in new float[] { -34, 34 })
                {
                    float x = d.vertical ? d.cx : d.cx + s, y = d.vertical ? d.cy + s : d.cy;
                    if (CanStand(x, y)) Nodes.Add(new P(x, y));
                }
            }
            foreach (var s in Spots) Nodes.Add(new P(s.ax, s.ay));
            adj = new List<(int, float)>[Nodes.Count];
            for (int i = 0; i < Nodes.Count; i++) adj[i] = new List<(int, float)>();
            for (int i = 0; i < Nodes.Count; i++)
                for (int j = i + 1; j < Nodes.Count; j++)
                {
                    var a = Nodes[i]; var b = Nodes[j]; float d = U.Dist(a.x, a.y, b.x, b.y);
                    if (d <= 80 && WalkLine(a.x, a.y, b.x, b.y)) { adj[i].Add((j, d)); adj[j].Add((i, d)); }
                }
        }

        static List<(int i, float d)> NearNodes(float x, float y)
        {
            var o = new List<(int, float)>();
            for (int i = 0; i < Nodes.Count; i++)
            {
                var n = Nodes[i]; float d = U.Dist(n.x, n.y, x, y);
                if (d < 110 && WalkLine(x, y, n.x, n.y)) o.Add((i, d));
            }
            if (o.Count == 0)
            {
                int b = 0; float bd = float.MaxValue;
                for (int i = 0; i < Nodes.Count; i++) { var n = Nodes[i]; float d = U.Dist(n.x, n.y, x, y); if (d < bd) { bd = d; b = i; } }
                o.Add((b, bd));
            }
            return o;
        }
        public static int NearestNode(float x, float y) => NearNodes(x, y)[0].i;

        static float[] D; static int[] Prev; static bool[] Done;
        /// <summary>Shortest walkable route. Empty when unreachable. ponytail: O(N²) Dijkstra over ~550 nodes; use a heap if the house grows.</summary>
        public static List<P> FindPath(float sx, float sy, float tx, float ty)
        {
            var path = new List<P>();
            if (WalkLine(sx, sy, tx, ty)) { path.Add(new P(tx, ty)); return path; }
            int N = Nodes.Count;
            if (D == null || D.Length != N) { D = new float[N]; Prev = new int[N]; Done = new bool[N]; }
            for (int i = 0; i < N; i++) { D[i] = float.PositiveInfinity; Prev[i] = -1; Done[i] = false; }
            foreach (var (i, d) in NearNodes(sx, sy)) D[i] = d;
            var T = new Dictionary<int, float>();
            foreach (var (i, d) in NearNodes(tx, ty)) T[i] = d;
            float best = float.PositiveInfinity; int end = -1;
            for (;;)
            {
                int u = -1; float ud = float.PositiveInfinity;
                for (int i = 0; i < N; i++) if (!Done[i] && D[i] < ud) { ud = D[i]; u = i; }
                if (u < 0 || ud >= best) break;
                Done[u] = true;
                if (T.TryGetValue(u, out float td) && ud + td < best) { best = ud + td; end = u; }
                foreach (var (v, w) in adj[u]) if (ud + w < D[v]) { D[v] = ud + w; Prev[v] = u; }
            }
            if (end < 0) return path;
            for (int u = end; u >= 0; u = Prev[u]) path.Insert(0, Nodes[u]);
            path.Add(new P(tx, ty));
            return path;
        }
    }
}
