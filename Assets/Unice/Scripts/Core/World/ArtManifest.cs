using System;

namespace Unice
{
    // The shape of Resources/Art/manifest.json (exported with the art from the web version), read with JsonUtility.
    // JsonUtility matches JSON keys to field NAMES: do not rename these fields without changing the JSON.

    /// <summary>The art placement for the house (pixel sizes are in house pixels unless noted).</summary>
    [Serializable]
    public class ArtManifest
    {
        /// <summary>Texture pixels per house pixel, for the house art and for the characters.</summary>
        public float worldScale, charScale;
        public FloorInfo[] floors;
        public WallInfo[] walls;
        public FurnInfo[] furn;
        /// <summary>Character sprite sizes and pivots: the children and the Unice.</summary>
        public FrameInfo kid, unice;
        public LayerInfo[] layers;
    }

    /// <summary>A painted floor: the room id it covers and where its top-left corner goes.</summary>
    [Serializable] public class FloorInfo { public string id, file; public float x, y, w, h; }

    /// <summary>A painted wall piece; <c>hgt</c> is how tall it stands (for walking behind it).</summary>
    [Serializable] public class WallInfo { public int i; public string file; public float x, y, w, h, hgt; }

    /// <summary>
    /// Painted furniture: <c>i</c> is its index in Map.Furniture; <c>ax, ay</c> its anchor inside the image,
    /// whose size is <c>pw × ph</c> (in image pixels).
    /// </summary>
    [Serializable] public class FurnInfo { public int i; public string type, file; public float ax, ay, pw, ph; }

    /// <summary>Character frame size and pivot (0..1); <c>frames</c> = animation frames per cycle.</summary>
    [Serializable] public class FrameInfo { public float w, h, pivotX, pivotY; public int frames; }

    [Serializable] public class LayerInfo { public string name, views; public int tint, frames; }
}
