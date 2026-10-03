using System.Collections.Generic;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// The sprites exported from the web version (Resources/Art), their materials, and the conversion between house pixels
    /// and Unity units. Positions in the game are in house pixels with y down; Unity's y goes up.
    /// </summary>
    public static class Art
    {
        /// <summary>100 house pixels = 1 Unity unit.</summary>
        public const float PxPerUnit = 100f;

        /// <summary>Where each painted floor, wall and piece of furniture goes (loaded by <see cref="Load"/>).</summary>
        public static ArtManifest Manifest;
        /// <summary>A plain white sprite (top-left pivot), scaled and tinted for flat shapes.</summary>
        public static Sprite WhiteSprite;
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        static Material spriteMaterial, fogMaterial, pixelMaterial;

        // The project enters Play mode without a domain reload, so statics from the last session must be cleared.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Manifest = null; spriteCache.Clear(); spriteMaterial = null; fogMaterial = null; pixelMaterial = null; WhiteSprite = null; }

        /// <summary>House pixels (y down) → Unity world position (y up).</summary>
        public static Vector3 ToWorld(float x, float y, float z = 0) => new Vector3(x / PxPerUnit, -y / PxPerUnit, z);

        /// <summary>Sorting order for something standing at <paramref name="baseY"/>: lower on screen draws in front. <paramref name="kind"/> breaks ties (wall 0, furniture 1, character 2).</summary>
        public static int SortingOrder(float baseY, int kind) => Mathf.RoundToInt(baseY * 4) + kind;

        public static void Load()
        {
            if (Manifest != null) return;
            Manifest = JsonUtility.FromJson<ArtManifest>(Resources.Load<TextAsset>("Art/manifest").text);
            var white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            white.SetPixels32(pixels); white.Apply();
            WhiteSprite = Sprite.Create(white, new Rect(0, 0, 4, 4), new Vector2(0, 1), 4);
        }

        /// <summary>Unlit sprite material, so the URP 2D renderer shows the art as painted without 2D lights.</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial != null) return spriteMaterial;
                spriteMaterial = Resources.Load<Material>("UniceUnlit");
                if (spriteMaterial == null)
                    spriteMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
                return spriteMaterial;
            }
        }

        /// <summary>Plain vertex-color material for the darkness mesh (Resources/UniceFog.shader).</summary>
        public static Material FogMat => fogMaterial ??= MaterialFromShader("UniceFog");

        /// <summary>Chunky/pixelated material for character sprites (Resources/UnicePixel.shader): the chibi retro look.</summary>
        public static Material PixelMat => pixelMaterial ??= MaterialFromShader("UnicePixel");

        static Material MaterialFromShader(string resourceName)
        {
            var shader = Resources.Load<Shader>(resourceName);
            return new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        }

        /// <summary>A sprite by file name (cached), with its pivot in 0..1 and its texture pixels per house pixel. Null if the file is missing.</summary>
        public static Sprite LoadSprite(string file, Vector2 pivot, float texturePixelsPerHousePixel)
        {
            string key = file + "|" + pivot.x.ToString("0.000") + "|" + pivot.y.ToString("0.000") + "|" + texturePixelsPerHousePixel;
            if (spriteCache.TryGetValue(key, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>("Art/" + file);
            if (texture == null) { spriteCache[key] = null; return null; }
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, texturePixelsPerHousePixel * PxPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = file;
            spriteCache[key] = sprite;
            return sprite;
        }

        public static Sprite CharacterSprite(string file) => LoadSprite(file, new Vector2(Manifest.kid.pivotX, Manifest.kid.pivotY), Manifest.charScale);
        public static Sprite UniceFrame(string file) => LoadSprite(file, new Vector2(Manifest.unice.pivotX, Manifest.unice.pivotY), Manifest.charScale);
        public static Sprite Centered(string file, float texturePixelsPerHousePixel) => LoadSprite(file, new Vector2(.5f, .5f), texturePixelsPerHousePixel);

        /// <summary>"#rrggbb" → Color (black if it doesn't parse).</summary>
        public static Color HexColor(string hex) { ColorUtility.TryParseHtmlString(hex, out var color); return color; }

        /// <summary>A new GameObject with a SpriteRenderer using the unlit sprite material.</summary>
        public static SpriteRenderer CreateSpriteRenderer(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var spriteObject = new GameObject(name); spriteObject.transform.SetParent(parent, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingOrder = sortingOrder; renderer.sharedMaterial = SpriteMaterial;
            return renderer;
        }
    }
}
