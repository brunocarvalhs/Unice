using UnityEngine;

namespace Unice
{
    /// <summary>
    /// The darkness: everything outside the line of sight is black, and what you can see fades with distance.
    /// Rays from the feet stop at walls (furniture doesn't block sight), like the web version.
    /// </summary>
    public sealed class FogView
    {
        const int N = 128;
        const float WallH = 58;
        readonly Mesh mesh;
        readonly Vector3[] verts = new Vector3[1 + N * 3];
        readonly Color32[] cols = new Color32[1 + N * 3];
        readonly GameObject go;

        public FogView(Transform parent)
        {
            go = new GameObject("Fog"); go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "fog" }; mesh.MarkDynamic();
            var tris = new int[N * 9];
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N, o = i * 9;
                tris[o] = 0; tris[o + 1] = 1 + j; tris[o + 2] = 1 + i;                                   // lit fan
                int a0 = 1 + N + i, a1 = 1 + N + j, b0 = 1 + 2 * N + i, b1 = 1 + 2 * N + j;            // wall edge → far ring
                tris[o + 3] = a0; tris[o + 4] = a1; tris[o + 5] = b1;
                tris[o + 6] = a0; tris[o + 7] = b1; tris[o + 8] = b0;
            }
            mesh.vertices = verts; mesh.colors32 = cols; mesh.triangles = tris;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Art.FogMat; mr.sortingOrder = 30000;
        }

        public void Hide() => go.SetActive(false);

        /// <param name="los">false for spectators: a plain circle, no walls.</param>
        public void Draw(float vx, float vy, float r, bool los, Color dark)
        {
            if (!go.activeSelf) go.SetActive(true);
            var dk = (Color32)dark; var clear = dk; clear.a = 0;
            float cy = vy - 18;
            verts[0] = Art.W(vx, cy); cols[0] = clear;
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2, dx = Mathf.Cos(a), dy = Mathf.Sin(a);
                float d = r; bool hit = false;
                if (los)
                {
                    d = 4;
                    while (d < r) { if (!Map.InWalk(vx + dx * d, vy + dy * d, 0)) { hit = true; break; } d += 8; }
                    if (hit) { float lo = d - 8, hi = d; for (int k = 0; k < 4; k++) { float m = (lo + hi) / 2; if (Map.InWalk(vx + dx * m, vy + dy * m, 0)) lo = m; else hi = m; } d = hi; }
                    d = Mathf.Min(d, r);
                }
                float x = vx + dx * d, y = (los ? vy : cy) + dy * d;
                // reveal the face of a wall standing right above the hit point
                if (hit && dy < -.1f && Map.InWalk(x, y + 3, 0) && !Map.InWalk(x, y - 3, 0)) y -= WallH + 2;
                float dist = Mathf.Sqrt((x - vx) * (x - vx) + (y - cy) * (y - cy));
                float k01 = Mathf.Clamp01((dist / r - .45f) / .55f);
                var c = dk; c.a = (byte)(dk.a * k01);
                verts[1 + i] = Art.W(x, y); cols[1 + i] = c;
                verts[1 + N + i] = Art.W(x, y); cols[1 + N + i] = dk;
                verts[1 + 2 * N + i] = Art.W(vx + dx * 4000, cy + dy * 4000); cols[1 + 2 * N + i] = dk;
            }
            mesh.vertices = verts; mesh.colors32 = cols; mesh.RecalculateBounds();
        }
    }
}
