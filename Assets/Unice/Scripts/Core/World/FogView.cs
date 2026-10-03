using UnityEngine;

namespace Unice
{
    /// <summary>
    /// The darkness: everything outside the line of sight is black, and what you can see fades with distance.
    /// Rays from the feet stop at walls (furniture doesn't block sight), like the web version.
    /// The mesh is a lit fan (center → ray ends) plus a dark band from each ray end out to far away.
    /// </summary>
    public sealed class FogView
    {
        const int RayCount = 128;
        /// <summary>How tall walls are drawn: a ray that hits a wall above the viewer also reveals the wall's face.</summary>
        const float WallHeight = 58;
        /// <summary>Rays march this far per sample, then refine the hit with a few halvings.</summary>
        const float RayStep = 8;
        /// <summary>The view is centered a bit above the feet.</summary>
        const float EyeAboveFeet = 18;
        /// <summary>Visible area starts fading at this fraction of the view radius.</summary>
        const float FadeStart = .45f;
        const float FarAway = 4000;

        readonly Mesh mesh;
        // vertex 0 = center; then RayCount ray ends (lit), RayCount ray ends (dark), RayCount far points (dark)
        readonly Vector3[] vertices = new Vector3[1 + RayCount * 3];
        readonly Color32[] colors = new Color32[1 + RayCount * 3];
        readonly GameObject fogObject;

        public FogView(Transform parent)
        {
            fogObject = new GameObject("Fog"); fogObject.transform.SetParent(parent, false);
            mesh = new Mesh { name = "fog" }; mesh.MarkDynamic();
            var triangles = new int[RayCount * 9];
            for (int i = 0; i < RayCount; i++)
            {
                int next = (i + 1) % RayCount, offset = i * 9;
                // lit fan: center, this ray's end, the next ray's end
                triangles[offset] = 0; triangles[offset + 1] = 1 + next; triangles[offset + 2] = 1 + i;
                // dark band: from the ray ends out to the far ring
                int edge = 1 + RayCount + i, edgeNext = 1 + RayCount + next, far = 1 + 2 * RayCount + i, farNext = 1 + 2 * RayCount + next;
                triangles[offset + 3] = edge; triangles[offset + 4] = edgeNext; triangles[offset + 5] = farNext;
                triangles[offset + 6] = edge; triangles[offset + 7] = farNext; triangles[offset + 8] = far;
            }
            mesh.vertices = vertices; mesh.colors32 = colors; mesh.triangles = triangles;
            fogObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = fogObject.AddComponent<MeshRenderer>(); meshRenderer.sharedMaterial = Art.FogMat; meshRenderer.sortingOrder = 30000;
        }

        public void Hide() => fogObject.SetActive(false);

        /// <param name="viewX">Where the viewer stands (feet), in house pixels.</param>
        /// <param name="radius">How far it sees.</param>
        /// <param name="usesLineOfSight">False for spectators: a plain circle, no walls.</param>
        /// <param name="darkness">The color (and opacity) of what can't be seen.</param>
        public void Draw(float viewX, float viewY, float radius, bool usesLineOfSight, Color darkness)
        {
            if (!fogObject.activeSelf) fogObject.SetActive(true);
            var dark = (Color32)darkness; var clear = dark; clear.a = 0;
            float eyeY = viewY - EyeAboveFeet;
            vertices[0] = Art.ToWorld(viewX, eyeY); colors[0] = clear;
            for (int i = 0; i < RayCount; i++)
            {
                float angle = i / (float)RayCount * Mathf.PI * 2, dirX = Mathf.Cos(angle), dirY = Mathf.Sin(angle);
                float reach = radius; bool hitWall = false;
                if (usesLineOfSight) reach = CastRay(viewX, viewY, dirX, dirY, radius, out hitWall);
                float endX = viewX + dirX * reach, endY = (usesLineOfSight ? viewY : eyeY) + dirY * reach;
                // reveal the face of a wall standing right above the hit point
                if (hitWall && dirY < -.1f && Map.IsWalkable(endX, endY + 3, 0) && !Map.IsWalkable(endX, endY - 3, 0)) endY -= WallHeight + 2;

                float distanceFromEye = Mathf.Sqrt((endX - viewX) * (endX - viewX) + (endY - eyeY) * (endY - eyeY));
                float fade = Mathf.Clamp01((distanceFromEye / radius - FadeStart) / (1 - FadeStart));
                var fadedDark = dark; fadedDark.a = (byte)(dark.a * fade);
                vertices[1 + i] = Art.ToWorld(endX, endY); colors[1 + i] = fadedDark;
                vertices[1 + RayCount + i] = Art.ToWorld(endX, endY); colors[1 + RayCount + i] = dark;
                vertices[1 + 2 * RayCount + i] = Art.ToWorld(viewX + dirX * FarAway, eyeY + dirY * FarAway); colors[1 + 2 * RayCount + i] = dark;
            }
            mesh.vertices = vertices; mesh.colors32 = colors; mesh.RecalculateBounds();
        }

        /// <summary>How far a ray from (x, y) goes before leaving the walkable area (a wall), capped at <paramref name="maxReach"/>.</summary>
        static float CastRay(float x, float y, float dirX, float dirY, float maxReach, out bool hitWall)
        {
            float reach = 4; hitWall = false;
            while (reach < maxReach)
            {
                if (!Map.IsWalkable(x + dirX * reach, y + dirY * reach, 0)) { hitWall = true; break; }
                reach += RayStep;
            }
            if (hitWall)
            {
                // binary search between the last free sample and the wall, for a smooth edge
                float free = reach - RayStep, blocked = reach;
                for (int i = 0; i < 4; i++)
                {
                    float middle = (free + blocked) / 2;
                    if (Map.IsWalkable(x + dirX * middle, y + dirY * middle, 0)) free = middle; else blocked = middle;
                }
                reach = blocked;
            }
            return Mathf.Min(reach, maxReach);
        }
    }
}
