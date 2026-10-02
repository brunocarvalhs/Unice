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

    /// <summary>A piece of furniture. When <see cref="side"/> is non-zero, a child can hide in it.</summary>
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
}
