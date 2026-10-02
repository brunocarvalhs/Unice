using System;
using System.IO;

namespace Unice
{
    /// <summary>The host's view of the match, packed small enough to send ~15 times a second.</summary>
    public sealed partial class Game
    {
        const byte SnapVersion = 4;

        public byte[] WriteSnapshot()
        {
            using var ms = new MemoryStream(1024);
            using var w = new BinaryWriter(ms);
            w.Write(SnapVersion);
            w.Write(ep); w.Write(hb); w.Write((byte)ph); w.Write((byte)r); w.Write((byte)rounds); w.Write((byte)bots); w.Write(tl); w.Write(t);
            w.Write(hideTime); w.Write(roundTime); w.Write(giggleEvery); w.Write(mapId);
            w.Write((sbyte)(unice != null ? unice.idx : -1)); w.Write((sbyte)(nextU != null ? nextU.idx : -1));
            w.Write(res != null);
            if (res != null) for (int i = 0; i < 4; i++) w.Write((sbyte)res[i]);
            w.Write((sbyte)(doll.holder != null ? doll.holder.idx : -1)); w.Write(doll.giggleT); w.Write(doll.hasLast);
            if (doll.hasLast) { w.Write(doll.lastX); w.Write(doll.lastY); w.Write(t - doll.lastT); }
            w.Write((byte)ents.Count);
            foreach (var e in ents)
            {
                w.Write(e.pid); w.Write(e.name); w.Write(Kit.Pack(e.k));
                byte f = 0;
                if (e.bot) f |= 1; if (e.outOfRound) f |= 2; if (e.moving) f |= 4; if (e.loud) f |= 8; if (e.IsUnice) f |= 16; if (e.tired > 0) f |= 32; if (e.sneak) f |= 64;
                w.Write(f);
                w.Write((short)MathF.Round(e.x)); w.Write((short)MathF.Round(e.y)); w.Write((short)MathF.Round(MathF.Atan2(e.fy, e.fx) * 1000));
                w.Write((sbyte)(e.hidden != null ? e.hidden.si : -1)); w.Write(MathF.Max(0, e.passCd)); w.Write(MathF.Max(0, e.laughCd)); w.Write((short)e.score); w.Write((byte)e.turns); w.Write(MathF.Max(0, e.stun)); w.Write(e.tp);
            }
            w.Write(evSeq); w.Write((byte)evs.Count);
            foreach (var v in evs)
            {
                w.Write(v.seq); w.Write((byte)v.type); w.Write(v.a); w.Write(v.b); w.Write(v.c); w.Write(v.d); w.Write(v.e);
                w.Write(v.s != null); if (v.s != null) w.Write(v.s);
            }
            w.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Mirror the host's snapshot. Returns this device's character (or null when watching).
        /// The local character keeps its own position unless the host moved it (tp changed), hid it or took it out.
        /// </summary>
        public Ent ApplySnapshot(byte[] data, string myId, Ent me)
        {
            using var ms = new MemoryStream(data);
            using var rd = new BinaryReader(ms);
            if (rd.ReadByte() != SnapVersion) return me;
            ep = rd.ReadInt32(); hb = rd.ReadSingle(); ph = (Phase)Math.Min((int)rd.ReadByte(), (int)Phase.Final); if (ph == Phase.Home) ph = Phase.Lobby;
            r = rd.ReadByte(); rounds = rd.ReadByte(); bots = rd.ReadByte(); tl = rd.ReadSingle(); t = rd.ReadSingle();
            hideTime = rd.ReadSingle(); roundTime = rd.ReadSingle(); giggleEvery = rd.ReadSingle();
            mapId = rd.ReadString(); if (Map.Id != mapId) Map.Load(Maps.ById(mapId));
            int ui = rd.ReadSByte(), nx = rd.ReadSByte();
            if (rd.ReadBoolean()) { res = new int[4]; for (int i = 0; i < 4; i++) res[i] = rd.ReadSByte(); } else res = null;
            int hi = rd.ReadSByte(); float gT = rd.ReadSingle(); bool hasLast = rd.ReadBoolean();
            float lx = 0, ly = 0, age = 0; if (hasLast) { lx = rd.ReadSingle(); ly = rd.ReadSingle(); age = rd.ReadSingle(); }
            int n = Math.Min((int)rd.ReadByte(), Rules.MaxPlayers);
            while (ents.Count > n) ents.RemoveAt(ents.Count - 1);
            foreach (var s in Map.Spots) s.occ = null;
            Ent newMe = null;
            for (int i = 0; i < n; i++)
            {
                string pid = rd.ReadString(), name = U.CleanName(rd.ReadString()); int kit = rd.ReadInt32(); byte f = rd.ReadByte();
                float x = rd.ReadInt16(), y = rd.ReadInt16(), ang = rd.ReadInt16() / 1000f; int si = rd.ReadSByte();
                float passCd = rd.ReadSingle(); float laughCd = rd.ReadSingle(); int score = rd.ReadInt16(), turns = rd.ReadByte(); float stun = rd.ReadSingle(); int tp = rd.ReadInt32();
                Ent e = i < ents.Count ? ents[i] : null; bool first = false;
                if (e == null || e.pid != pid) { e = new Ent(i, pid, name, Kit.Unpack(kit), false); if (i < ents.Count) ents[i] = e; else ents.Add(e); first = true; }
                e.name = name; e.k = Kit.Unpack(kit); e.bot = (f & 1) != 0; e.outOfRound = (f & 2) != 0; e.role = (f & 16) != 0 ? Role.Unice : Role.Hider; e.tired = (f & 32) != 0 ? 1 : 0;
                e.hidden = si >= 0 && si < Map.Spots.Length ? Map.Spots[si] : null; if (e.hidden != null) e.hidden.occ = e;
                e.passCd = passCd; e.laughCd = laughCd; e.score = score; e.turns = turns; e.stun = stun;
                bool isMe = pid == myId;
                if (isMe)
                {
                    newMe = e;
                    if (first || tp != e.tp || e.hidden != null || e.outOfRound || !Live) { e.x = e.tx = x; e.y = e.ty = y; }
                    if (first || tp != e.tp) { e.fx = MathF.Cos(ang); e.fy = MathF.Sin(ang); }
                }
                else
                {
                    e.moving = (f & 4) != 0; e.loud = (f & 8) != 0; e.sneak = (f & 64) != 0;
                    e.fx = MathF.Cos(ang); e.fy = MathF.Sin(ang); e.tx = x; e.ty = y;
                    if (first || tp != e.tp || U.Dist(x, y, e.x, e.y) > 200) { e.x = x; e.y = y; }
                }
                e.tp = tp;
            }
            unice = EntAt(ui); nextU = EntAt(nx);
            doll.holder = EntAt(hi); doll.giggleT = gT; doll.hasLast = hasLast; doll.lastX = lx; doll.lastY = ly; doll.lastT = t - age; doll.lastSpot = null;
            evSeq = rd.ReadInt32(); int ne = rd.ReadByte();
            evs.Clear();
            for (int i = 0; i < ne; i++)
            {
                var v = new GameEvent { seq = rd.ReadInt32(), type = (EvType)rd.ReadByte(), a = rd.ReadInt32(), b = rd.ReadInt32(), c = rd.ReadInt32(), d = rd.ReadInt32(), e = rd.ReadInt32() };
                if (rd.ReadBoolean()) v.s = U.CleanName(rd.ReadString());
                evs.Add(v);
            }
            return newMe;
        }

        public static int ReadEpoch(byte[] data)
        {
            if (data == null || data.Length < 5 || data[0] != SnapVersion) return -1;
            return BitConverter.ToInt32(data, 1);
        }
    }

    /// <summary>Presence on the wire (sent ~15 times a second by every player).</summary>
    public static class Wire
    {
        public static byte[] Write(in Presence p)
        {
            using var ms = new MemoryStream(64);
            using var w = new BinaryWriter(ms);
            w.Write((byte)1); w.Write(p.name ?? ""); w.Write(p.kit); w.Write(p.joinT); w.Write(p.rdy); w.Write(p.tp);
            w.Write(p.hasPos); if (p.hasPos) { w.Write(p.x); w.Write(p.y); w.Write(p.fx); w.Write(p.fy); w.Write(p.sneak); }
            w.Write(p.aSeq); w.Write((byte)p.aType); w.Write((short)p.aArg); w.Write(p.talking);
            w.Flush(); return ms.ToArray();
        }

        /// <summary>Data from other players: anything malformed is dropped.</summary>
        public static bool TryRead(byte[] data, out Presence p)
        {
            p = default;
            try
            {
                using var ms = new MemoryStream(data);
                using var r = new BinaryReader(ms);
                if (r.ReadByte() != 1) return false;
                p.name = U.CleanName(r.ReadString()); p.kit = r.ReadInt32(); p.joinT = r.ReadInt64(); p.rdy = r.ReadBoolean(); p.tp = r.ReadInt32();
                p.hasPos = r.ReadBoolean();
                if (p.hasPos) { p.x = r.ReadSingle(); p.y = r.ReadSingle(); p.fx = r.ReadSingle(); p.fy = r.ReadSingle(); p.sneak = r.ReadBoolean(); }
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.fx) || float.IsNaN(p.fy)) p.hasPos = false;
                p.aSeq = r.ReadInt32(); byte a = r.ReadByte(); p.aType = a <= (byte)Act.Laugh ? (Act)a : Act.None; p.aArg = r.ReadInt16();
                p.talking = r.ReadBoolean();
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
