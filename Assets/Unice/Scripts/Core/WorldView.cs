using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unice
{
    /// <summary>
    /// Builds the house (floors, walls, furniture) and the characters out of sprites.
    /// Everything that stands up is sorted by the y of its base, so you walk in front of or behind things like in the web version.
    /// </summary>
    public sealed class WorldView
    {
        static readonly Dictionary<string, float> FH = new Dictionary<string, float>
        {
            { "bed", 24 }, { "wardrobe", 56 }, { "dresser", 36 }, { "tub", 24 }, { "sink", 32 }, { "bunk", 50 }, { "chest", 22 }, { "dollhouse", 44 },
            { "shelves", 56 }, { "desk", 28 }, { "clock", 58 }, { "armchair", 28 }, { "closet", 52 }, { "commode", 30 }, { "sofa", 26 }, { "fireplace", 50 },
            { "piano", 32 }, { "table", 24 }, { "cabinet", 54 }, { "sinkcab", 30 }, { "stove", 32 }, { "ktable", 26 }, { "ferns", 34 }, { "planters", 20 },
            { "stairs", 42 }, { "coats", 54 }, { "barrel", 34 }, { "pshelves", 52 }, { "hamper", 28 }, { "tank", 28 }
        };
        struct Block { public SpriteRenderer sr; public float x, y, w, h, hgt; }

        readonly Transform root;
        readonly List<Block> blocks = new List<Block>();
        readonly List<EntView> views = new List<EntView>();
        readonly List<(SpriteRenderer sr, float t, float life, float max, Color c)> ripples = new List<(SpriteRenderer, float, float, float, Color)>();
        readonly Stack<SpriteRenderer> ripplePool = new Stack<SpriteRenderer>();
        public readonly FogView fog;

        public WorldView(Transform parent)
        {
            Art.Load();
            root = new GameObject("House").transform; root.SetParent(parent, false);
            var bg = Art.MakeSR("background", root, Art.White, -30000); bg.color = Art.Hex("#121212");
            bg.transform.localPosition = Art.W(14, 14); bg.transform.localScale = new Vector3((Map.WorldW - 28) / Art.PxPerUnit, (Map.WorldH - 28) / Art.PxPerUnit, 1);
            foreach (var f in Art.M.floors)
            {
                var sr = Art.MakeSR("floor_" + f.id, root, Art.Get(f.file, new Vector2(0, 1), Art.M.worldScale), -20000);
                sr.transform.localPosition = Art.W(f.x, f.y);
            }
            foreach (var d in Map.Doors)
            {
                var sr = Art.MakeSR("threshold", root, Art.White, -19999); sr.color = Art.Hex("#3e2a1e");
                var r = d.vertical ? new RectF(d.r.x, d.r.y + Map.OV - 1, d.r.w, Map.GAP + 2) : new RectF(d.r.x + Map.OV - 1, d.r.y, Map.GAP + 2, d.r.h);
                sr.transform.localPosition = Art.W(r.x, r.y); sr.transform.localScale = new Vector3(r.w / Art.PxPerUnit, r.h / Art.PxPerUnit, 1);
            }
            var mat = Art.MakeSR("mat", root, Art.White, -19998); mat.color = Art.Hex("#4a2e1f");
            mat.transform.localPosition = Art.W(784, 1128); mat.transform.localScale = new Vector3(.8f, .1f, 1);
            foreach (var w in Art.M.walls)
            {
                var sr = Art.MakeSR("wall_" + w.i, root, Art.Get(w.file, new Vector2(0, 0), Art.M.worldScale), Art.Order(w.y + w.h, 0));
                sr.transform.localPosition = Art.W(w.x, w.y + w.h);
                blocks.Add(new Block { sr = sr, x = w.x, y = w.y, w = w.w, h = w.h, hgt = w.hgt });
            }
            foreach (var fi in Art.M.furn)
            {
                var f = Map.Furniture[fi.i];
                var sr = Art.MakeSR("furn_" + f.type, root, Art.Get(fi.file, new Vector2(fi.ax / fi.pw, 1 - fi.ay / fi.ph), Art.M.worldScale), Art.Order(f.y + f.h, 1));
                sr.transform.localPosition = Art.W(f.x, f.y + f.h);
                blocks.Add(new Block { sr = sr, x = f.x, y = f.y, w = f.w, h = f.h, hgt = FH.TryGetValue(f.type, out var hg) ? hg : 28 });
            }
            fog = new FogView(parent);
        }

        /// <summary>Walls and furniture you walk behind turn see-through, so you never lose your own character.</summary>
        public void UpdateOcclusion(Ent me, bool live)
        {
            foreach (var b in blocks)
            {
                float a = 1;
                if (me != null && live && !me.outOfRound && me.hidden == null && me.x > 0 && me.y < b.y + b.h && me.y > b.y - b.hgt - 4 && me.x + 12 > b.x && me.x - 12 < b.x + b.w) a = .4f;
                var c = b.sr.color; if (!Mathf.Approximately(c.a, a)) { c.a = a; b.sr.color = c; }
            }
        }

        /// <summary>Alpha each character was drawn with this frame (0 = not shown), for name tags.</summary>
        public readonly List<float> Shown = new List<float>();

        public void SyncEntities(Game g, Ent me, System.Func<float, float, bool> seen, bool teamView, float time)
        {
            while (views.Count < g.ents.Count) views.Add(new EntView(root, views.Count));
            Shown.Clear();
            for (int i = 0; i < views.Count; i++)
            {
                if (i >= g.ents.Count || !g.Live && g.ph != Phase.Result) { views[i].Hide(); if (i < g.ents.Count) Shown.Add(0); continue; }
                var e = g.ents[i];
                float alpha = 1;
                bool show = !e.outOfRound && e.x > 0 && (e == me || seen(e.x, e.y));
                if (show && e.hidden != null) { if (e == me) alpha = .45f; else if (teamView && !e.IsUnice) alpha = .3f; else show = false; }
                if (!show) { views[i].Hide(); Shown.Add(0); continue; }
                views[i].Show(e, e == me, alpha, teamView && e == g.doll.holder && !e.IsUnice, time);
                Shown.Add(alpha);
            }
        }

        public void Ripple(float x, float y, string kind)
        {
            var sr = ripplePool.Count > 0 ? ripplePool.Pop() : Art.MakeSR("ripple", root, Art.Centered("ring", 1), 31000);
            sr.gameObject.SetActive(true);
            sr.transform.localPosition = Art.W(x, y + (kind == "giggle" ? 0 : 6));
            Color c = kind == "giggle" ? Art.Hex("#ef7394") : Art.Hex("#f0b057");
            float max = kind == "giggle" ? 240 : kind == "step" ? 46 : 34, life = kind == "giggle" ? 1.8f : .8f;
            if (kind == "own") c.a = .45f;
            ripples.Add((sr, 0, life, max, c));
        }

        public void Tick(float dt)
        {
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var (sr, t, life, max, c) = ripples[i]; t += dt;
                if (t >= life) { sr.gameObject.SetActive(false); ripplePool.Push(sr); ripples.RemoveAt(i); continue; }
                float k = t / life, radius = 8 + k * max;
                sr.transform.localScale = Vector3.one * (radius * 2 / 256f);   // ring sprite is 256 px wide at 1 px per house px
                sr.color = new Color(c.r, c.g, c.b, c.a * (1 - k));
                ripples[i] = (sr, t, life, max, c);
            }
        }
    }

    /// <summary>One character on screen: a paper-doll child (tinted layers) or the Unice, plus shadow, ring and doll icon.</summary>
    public sealed class EntView
    {
        static readonly string[] Order = { "cape", "packside", "legs", "body", "arm", "teddy", "packback", "head", "hair0", "hairX", "face", "glasses", "hat", "hatX" };
        readonly GameObject go;
        readonly SortingGroup group;
        readonly SpriteRenderer shadow, ring, doll, unice;
        readonly Dictionary<string, SpriteRenderer> layers = new Dictionary<string, SpriteRenderer>();
        readonly int idx;

        public EntView(Transform parent, int idx)
        {
            this.idx = idx;
            go = new GameObject("ent" + idx); go.transform.SetParent(parent, false);
            group = go.AddComponent<SortingGroup>();
            shadow = Art.MakeSR("shadow", go.transform, Art.Centered("shadow", 5.6f), 0); shadow.color = new Color(0, 0, 0, .45f);
            ring = Art.MakeSR("ring", go.transform, Art.Centered("ring", 8), 1); ring.color = new Color(.94f, .69f, .34f, .7f);
            ring.transform.localScale = new Vector3(1, .34f, 1);
            for (int i = 0; i < Order.Length; i++) { var sr = Art.MakeSR(Order[i], go.transform, null, 10 + i); sr.sharedMaterial = Art.PixelMat; layers[Order[i]] = sr; }
            unice = Art.MakeSR("unice", go.transform, null, 40); unice.sharedMaterial = Art.PixelMat;
            doll = Art.MakeSR("doll", go.transform, Art.Centered("doll", Art.M.charScale), 50);
            doll.transform.localPosition = new Vector3(.12f, .46f, 0);
            go.SetActive(false);
        }

        public void Hide() { if (go.activeSelf) go.SetActive(false); }

        public void Show(Ent e, bool isMe, float alpha, bool showDoll, float time)
        {
            if (!go.activeSelf) go.SetActive(true);
            if (e.fx > .3f) e.face = 1; else if (e.fx < -.3f) e.face = -1;
            bool back = e.fy < -.6f && Mathf.Abs(e.fx) < .55f;
            string v = back ? "b" : "s";
            float bob = e.IsUnice ? 0 : (e.moving ? Mathf.Abs(Mathf.Sin(time * 13 + e.idx)) * 1.2f : 0);
            go.transform.localPosition = Art.W(e.x, e.y - bob);
            group.sortingOrder = Art.Order(e.y, 2);
            shadow.transform.localPosition = new Vector3(0, bob / Art.PxPerUnit, 0);
            ring.gameObject.SetActive(isMe && alpha >= 1);
            doll.gameObject.SetActive(showDoll);
            if (showDoll) doll.color = new Color(1, 1, 1, e.passCd > 0 ? .5f + .5f * Mathf.Abs(Mathf.Sin(time * 8)) : 1);

            if (e.IsUnice)
            {
                foreach (var l in layers.Values) l.enabled = false;
                unice.enabled = true;
                int f = Mathf.FloorToInt(time * (e.moving ? 8f : 3f)) % Art.M.unice.frames;   // 8 frames = one stride (two steps)
                unice.sprite = Art.UniceFrame($"unice_{(e.moving ? "m" : "i")}_{v}_{f}");
                unice.flipX = e.face < 0;
                unice.transform.localPosition = Vector3.zero;
                unice.color = new Color(1, 1, 1, alpha);
                shadow.transform.localScale = new Vector3(1.1f, 1.1f, 1);
                return;
            }
            unice.enabled = false; shadow.transform.localScale = Vector3.one;
            int frame = e.moving ? 1 + Mathf.FloorToInt(time * 13 / (Mathf.PI * 2) * 6) % 6 : 0;
            KidLayers(e.k, back, frame, slots);
            for (int i = 0; i < Order.Length; i++) Set(Order[i], slots[i].file, slots[i].tint, alpha, e.face);
        }

        readonly (string file, Color tint)[] slots = new (string, Color)[14];

        /// <summary>The paper-doll layers of a child, back to front (null file = layer not used). Shared with the menu avatars.</summary>
        public static void KidLayers(int[] k, bool back, int frame, (string file, Color tint)[] o)
        {
            string v = back ? "b" : "s";
            Color skin = Art.Hex(Kit.Skins[k[0]]), hair = Art.Hex(Kit.HairColors[k[2]]), outfit = Art.Hex(Kit.OutfitColors[k[4]]), w = Color.white;
            o[0] = (k[3] == 4 ? $"kid_cape_{v}_{frame}" : null, outfit);
            o[1] = (k[6] == 1 && !back ? "kid_packside_s" : null, w);
            o[2] = ($"kid_legs_{v}_{frame}", w);
            o[3] = ($"kid_body{k[3]}_{v}", outfit);
            o[4] = ($"kid_arm_{v}_{frame}", skin);
            o[5] = (k[6] == 3 && !back ? "kid_teddy_s" : null, w);
            o[6] = (k[6] == 1 && back ? "kid_packback_b" : null, w);
            o[7] = ($"kid_head_{v}", skin);
            o[8] = ($"kid_hair0_{v}", hair);
            o[9] = (k[1] > 0 ? $"kid_hair{k[1]}_{v}" : null, hair);
            o[10] = (back ? null : "kid_face_s", w);
            o[11] = (k[6] == 2 && !back ? "kid_glasses_s" : null, w);
            o[12] = (k[5] > 0 ? $"kid_hat{k[5]}_{v}" : null, k[5] == 1 || k[5] == 2 ? outfit : k[5] == 5 ? hair : w);
            o[13] = (k[5] == 2 ? $"kid_hat2pom_{v}" : k[5] == 5 ? $"kid_hat5in_{v}" : null, w);
        }

        void Set(string layer, string file, Color tint, float alpha, int face)
        {
            var sr = layers[layer];
            if (file == null) { sr.enabled = false; return; }
            sr.enabled = true; sr.sprite = Art.Char(file); sr.flipX = face < 0;
            tint.a = alpha; sr.color = tint;
        }
    }
}
