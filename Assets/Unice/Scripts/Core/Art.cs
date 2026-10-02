using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unice
{
    [Serializable] public class FloorInfo { public string id, file; public float x, y, w, h; }
    [Serializable] public class WallInfo { public int i; public string file; public float x, y, w, h, hgt; }
    [Serializable] public class FurnInfo { public int i; public string type, file; public float ax, ay, pw, ph; }
    [Serializable] public class FrameInfo { public float w, h, pivotX, pivotY; public int frames; }
    [Serializable] public class LayerInfo { public string name, views; public int tint, frames; }
    [Serializable]
    public class ArtManifest
    {
        public float worldScale, charScale;
        public FloorInfo[] floors; public WallInfo[] walls; public FurnInfo[] furn;
        public FrameInfo kid, unice; public LayerInfo[] layers;
    }

    /// <summary>Sprites exported from the web version (Resources/Art) and the conversions between house pixels and Unity units.</summary>
    public static class Art
    {
        public const float PxPerUnit = 100f;   // 100 house pixels = 1 Unity unit
        public static ArtManifest M;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Material mat;
        public static Sprite White;
        static Material fogMat;
        static Material pixelMat;

        /// <summary>Plain vertex-color material for the darkness mesh (Resources/UniceFog.shader).</summary>
        public static Material FogMat
        {
            get
            {
                if (fogMat != null) return fogMat;
                var sh = Resources.Load<Shader>("UniceFog");
                fogMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                return fogMat;
            }
        }

        public static Vector3 W(float x, float y, float z = 0) => new Vector3(x / PxPerUnit, -y / PxPerUnit, z);
        public static int Order(float baseY, int kind) => Mathf.RoundToInt(baseY * 4) + kind;

        // The project enters Play mode without a domain reload, so statics from the last session must be cleared.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { M = null; cache.Clear(); mat = null; fogMat = null; pixelMat = null; White = null; }

        public static void Load()
        {
            if (M != null) return;
            var ta = Resources.Load<TextAsset>("Art/manifest");
            M = JsonUtility.FromJson<ArtManifest>(ta.text);
            var wt = new Texture2D(4, 4, TextureFormat.RGBA32, false); var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            wt.SetPixels32(px); wt.Apply();
            White = Sprite.Create(wt, new Rect(0, 0, 4, 4), new Vector2(0, 1), 4);
        }

        /// <summary>Unlit sprite material, so the URP 2D renderer shows the art as painted without 2D lights.</summary>
        public static Material Mat
        {
            get
            {
                if (mat != null) return mat;
                mat = Resources.Load<Material>("UniceUnlit");
                if (mat == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                    mat = new Material(sh);
                }
                return mat;
            }
        }

        /// <summary>Chunky/pixelated material for character sprites (Resources/UnicePixel.shader): the chibi retro look.</summary>
        public static Material PixelMat
        {
            get
            {
                if (pixelMat != null) return pixelMat;
                var sh = Resources.Load<Shader>("UnicePixel");
                pixelMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                return pixelMat;
            }
        }

        /// <summary>A sprite by file name, with its pivot given in 0..1 and its texture pixels per Unity unit.</summary>
        public static Sprite Get(string file, Vector2 pivot, float texPxPerHousePx)
        {
            string key = file + "|" + pivot.x.ToString("0.000") + "|" + pivot.y.ToString("0.000") + "|" + texPxPerHousePx;
            if (cache.TryGetValue(key, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Art/" + file);
            if (tex == null) { cache[key] = null; return null; }
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, texPxPerHousePx * PxPerUnit, 0, SpriteMeshType.FullRect);
            s.name = file; cache[key] = s; return s;
        }
        public static Sprite Char(string file) => Get(file, new Vector2(M.kid.pivotX, M.kid.pivotY), M.charScale);
        public static Sprite UniceFrame(string file) => Get(file, new Vector2(M.unice.pivotX, M.unice.pivotY), M.charScale);
        public static Sprite Centered(string file, float texPxPerHousePx) => Get(file, new Vector2(.5f, .5f), texPxPerHousePx);

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static SpriteRenderer MakeSR(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = order; sr.sharedMaterial = Mat;
            return sr;
        }
    }
}
