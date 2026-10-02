using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// The room this device is in (or solo play): create/join/leave, who hosts, presence and snapshots on the wire,
    /// and who is in the waiting room. The host runs <see cref="Game"/>; guests mirror its snapshots.
    /// </summary>
    sealed class RoomSession
    {
        readonly App app;
        Game G => app.G;
        public INetBackend net;                 // the room this device is in (null when solo or at home)
        readonly INetBackend backend;           // created once, reused between rooms
        public bool Online => net != null && net.InRoom;
        public string code = "", myId = "me"; public bool rdy, busy; public long joinT;
        public string netText = ""; public bool netErr;
        byte[] lastS, lastApplied;
        float pubT;

        public RoomSession(App app)
        {
            this.app = app;
            backend = NetRegistry.Factory?.Invoke();
            if (backend != null) backend.Lost += RoomLost;
        }

        public PeerInfo FindHost() => Online ? HostElection.Find(net.Peers, Time.realtimeSinceStartup) : null;

        /// <summary>Per step while online: steps down when another host shows up, takes over when there is none.</summary>
        public void ElectHost()
        {
            var h = FindHost();
            if (G.isHost)
            {
                if (h != null && !h.isMe)
                {
                    G.isHost = false; lastApplied = null;
                    foreach (var p in net.Peers) if (p.isMe) p.lastSnap = null;
                    app.ui.Toast(Loc.T("net.host_changed", U.CleanName(h.pres.name)));
                }
                return;
            }
            if (h != null || lastS == null) return;
            var oldest = HostElection.Oldest(net.Peers, Time.realtimeSinceStartup);
            if (oldest != null && oldest.isMe) G.TakeOver(net.Peers, app.me, app.myName);
        }

        /// <summary>Guest: applies the host's newest snapshot.</summary>
        public void PullSnap()
        {
            var h = FindHost();
            if (h == null || h.lastSnap == lastApplied) return;
            lastApplied = h.lastSnap;
            try { app.me = G.ApplySnapshot(h.lastSnap, myId, app.me); lastS = h.lastSnap; }
            catch (Exception x) { Metrics.Error(x, "snapshot"); return; }
            app.events.Handle();
        }

        void Publish()
        {
            var pr = new Presence { name = app.myName, kit = Kit.Pack(app.kit), joinT = joinT, rdy = rdy, tp = app.me != null ? app.me.tp : 0, aSeq = app.player.aSeq, aType = app.player.aType, aArg = app.player.aArg };
            var me = app.me;
            if (me != null && G.CanMoveNow(me)) { pr.hasPos = true; pr.x = me.x; pr.y = me.y; pr.fx = me.fx; pr.fy = me.fy; pr.sneak = me.sneak; }
            pr.talking = app.Talking;
            net.SetPresence(pr);
            if (G.isHost) net.BroadcastSnapshot(G.WriteSnapshot(), G.ep);
        }

        public void ResetRoomState()
        {
            code = ""; app.me = null; app.events.Reset(); lastS = null; lastApplied = null; app.player.Reset(); rdy = false; app.paused = false;
            G.ents.Clear(); G.isHost = false; G.evs.Clear(); G.aseq.Clear(); G.evSeq = 0; G.r = 0; G.unice = null; G.doll.holder = null; G.ph = Phase.Home;
            joinT = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void SetNet(string text, bool err = false) { netText = text ?? ""; netErr = err; }

        public async void EnterRoom(bool create, string joinCode)
        {
            if (busy) return;
            if (backend == null) { SetNet(Loc.T("net.unavailable"), true); return; }
            busy = true; app.SaveProfile();
            SetNet(Loc.T(create ? "net.creating" : "net.joining"));
            try
            {
                await backend.InitAsync();
                if (create) await backend.CreateAsync(); else await backend.JoinAsync(joinCode);
            }
            catch (Exception x)
            {
                busy = false; SetNet(x is NetException ? x.Message : Loc.T("net.conn_failed"), true);
                if (x is NetException) Debug.LogWarning(x.Message); else Metrics.Error(x, "room");   // NetException = already reported by the backend
                Metrics.Log("room_error", ("mode", create ? "create" : "join"), ("error", x.GetType().Name)); return;
            }
            ResetRoomState();
            net = backend; code = backend.Code; myId = backend.MyId;
            if (app.voice != null) { try { await app.voice.JoinRoomAsync(code); } catch (Exception x) { Metrics.Error(x); } }
            Publish();
            if (create) { G.isHost = true; G.ep = 1; G.ph = Phase.Lobby; }
            else
            {
                float t0 = Time.realtimeSinceStartup;
                while (Online && Time.realtimeSinceStartup - t0 < 8 && FindHost() == null) await Task.Delay(250);
                if (!Online) { busy = false; return; }
                if (FindHost() == null) { busy = false; LeaveRoom(); SetNet(Loc.T("net.no_answer"), true); Metrics.Log("room_error", ("mode", "join"), ("error", "no_host")); return; }
                PullSnap();
            }
            busy = false; SetNet("");
            Metrics.Log(create ? "room_create" : "room_join", ("app.voice", app.voice != null));
        }

        public void RoomLost(string why) { if (net == null) return; Metrics.Log("room_lost", ("phase", G.ph.ToString().ToLowerInvariant())); LeaveRoom(); SetNet(why, true); }

        public async void LeaveRoom()
        {
            var b = net; net = null;
            ResetRoomState(); myId = "me"; app.ui.ShowPause(false, false);
            if (app.voice != null) { try { await app.voice.LeaveRoomAsync(); } catch (Exception x) { Metrics.Error(x); } }
            if (b == null) return;
            Metrics.Log("room_leave");
            busy = true;
            try { await b.LeaveAsync(); } catch (Exception x) { Metrics.Error(x); }
            busy = false;
        }

        public void PlaySolo()
        {
            if (busy) return;
            app.SaveProfile(); ResetRoomState(); net = null; myId = "me"; rdy = true;
            G.isHost = true; G.ep = 1; G.ph = Phase.Lobby;
            Metrics.Log("solo_start");
        }

        public struct LobbyHuman { public string id, name; public int[] k; public bool rdy, me; }

        /// <summary>Who is in the waiting room, by arrival.</summary>
        public List<LobbyHuman> LobbyHumans()
        {
            var list = new List<LobbyHuman>();
            if (!Online) { list.Add(new LobbyHuman { id = myId, name = app.myName, k = app.kit, rdy = true, me = true }); return list; }
            foreach (var p in net.Peers.Where(p => p.isMe || p.pres.name != null).OrderBy(p => p.pres.joinT).ThenBy(p => p.id, StringComparer.Ordinal))
                list.Add(p.isMe ? new LobbyHuman { id = p.id, name = app.myName, k = app.kit, rdy = rdy, me = true }
                                : new LobbyHuman { id = p.id, name = U.CleanName(p.pres.name), k = Kit.Unpack(p.pres.kit), rdy = p.pres.rdy });
            return list;
        }

        /// <summary>The humans for a new round: this device first, then the others by arrival.</summary>
        public List<Game.Human> HumansNow()
        {
            var a = this; var list = new List<Game.Human> { new Game.Human { id = a.myId, name = app.myName, k = (int[])app.kit.Clone(), joinT = a.joinT } };
            if (a.Online)
                foreach (var p in a.net.Peers.Where(p => !p.isMe && p.pres.name != null).OrderBy(p => p.pres.joinT))
                    list.Add(new Game.Human { id = p.id, name = U.CleanName(p.pres.name), k = Kit.Unpack(p.pres.kit), joinT = p.pres.joinT });
            return list;
        }

        public void StartMatch(int bots, int rounds)
        {
            if (!G.isHost || G.ph != Phase.Lobby) return;
            G.bots = bots; G.rounds = rounds; G.NewMatch(HumansNow());
            Metrics.Log("match_start", ("humans", G.ents.Count(e => !e.bot)), ("bots", G.ents.Count(e => e.bot)), ("rounds", rounds), ("map", G.mapId), ("online", Online),
                ("hide_time", (int)G.hideTime), ("round_time", (int)G.roundTime));
        }

        public void NewMatchLobby() { if (G.isHost && G.ph == Phase.Final) G.BackToLobby(); }
        public void ToggleReady() { rdy = !rdy; pubT = 0; }
        /// <summary>Sends this device's presence on the next step instead of waiting for the regular beat.</summary>
        public void PublishSoon() => pubT = 0;

        /// <summary>Per step while online.</summary>
        public void Tick(float dt) { pubT -= dt; if (pubT <= 0) { pubT = 1 / 15f; Publish(); } }
    }
}
