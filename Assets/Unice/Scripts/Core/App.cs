using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Unice
{
    public struct ViewInfo { public float x, y, r; public bool los, spect; }

    /// <summary>
    /// The game on this device: runs the match when hosting, mirrors the host otherwise, and drives sound, camera and screens.
    /// Same flow as the web version: home → waiting room → rounds → final.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        public static App I;
        public readonly Game G = new Game();
        public INetBackend net;                 // the room this device is in (null when solo or at home)
        INetBackend backend;                    // created once, reused between rooms
        public bool Online => net != null && net.InRoom;
        public bool NetAvailable => NetRegistry.Factory != null;

        IVoiceChat voice;                        // proximity voice for the room above; null if Vivox isn't installed
        public bool MicOn { get => voice != null && voice.MicOn; set { if (voice != null) voice.MicOn = value; } }
        public bool Whisper { get; set; }        // session-only: when on, talking doesn't count as noise for the Unice, and transmits quieter

        /// <summary>Real voice activity right now, gated by the whisper toggle — the same rule used locally and sent to the host.</summary>
        bool Talking => voice != null && voice.IsSpeaking && !Whisper;

        public string code = "", myId = "me"; public bool rdy; public long joinT;
        ProfileStore profile;
        public string myName { get => profile.Name; set => profile.Name = value; }
        public int[] kit { get => profile.Kit; set => profile.Kit = value; }
        [NonSerialized] public Ent me; [NonSerialized] public ViewInfo view; public bool hasView;
        /// <summary>While spectating, who to follow (their pid) — null means auto (the doll holder). Self-healing: a stale pid just falls back.</summary>
        [NonSerialized] public string spectTarget;
        public bool paused, busy;
        public string netText = ""; public bool netErr;

        byte[] lastS, lastApplied;
        Phase lastPh = (Phase)255;
        float pubT, freeze, catchCd, acc;
        int aSeq, aArg; Act aType;
        [NonSerialized] public Sfx sfx; [NonSerialized] public WorldView world; [NonSerialized] public Ui ui;
        [NonSerialized] internal Soundscape sound; [NonSerialized] internal EventFeed events; [NonSerialized] internal CameraRig rig;
        bool kUp, kDown, kLeft, kRight, kSneak;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;   // Play mode without domain reload

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (I == null) new GameObject("Unice").AddComponent<App>(); }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this; DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60; Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Metrics.Init();
            profile = new ProfileStore();
            rig = new CameraRig();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem"); DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            world = new WorldView(transform);
            sfx = new Sfx(transform);
            sound = new Soundscape(this); events = new EventFeed(this);
            ui = new Ui(this);
            backend = NetRegistry.Factory?.Invoke();
            if (backend != null) backend.Lost += RoomLost;
            voice = VoiceRegistry.Factory?.Invoke();
            HideSystemBars();
        }

        bool keyboardWas;
        void OnApplicationFocus(bool focus) { if (focus) HideSystemBars(); }

        /// <summary>Android: hide the status and navigation bars (they come back after the keyboard or leaving the app).</summary>
        static void HideSystemBars()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using var version = new AndroidJavaClass("android.os.Build$VERSION");
                        var window = activity.Call<AndroidJavaObject>("getWindow");
                        if (version.GetStatic<int>("SDK_INT") >= 30)
                        {
                            using var types = new AndroidJavaClass("android.view.WindowInsets$Type");
                            var ctrl = window.Call<AndroidJavaObject>("getInsetsController");
                            ctrl.Call("hide", types.CallStatic<int>("statusBars") | types.CallStatic<int>("navigationBars"));
                            ctrl.Call("setSystemBarsBehavior", 2);   // BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
                        }
                        else window.Call<AndroidJavaObject>("getDecorView").Call("setSystemUiVisibility", 5894);   // immersive sticky
                    }
                    catch (Exception x) { Metrics.Error(x); }
                }));
            }
            catch (Exception x) { Metrics.Error(x); }
#endif
        }

        // ---------- main loop ----------
        void Update()
        {
            bool kbd = TouchScreenKeyboard.visible; if (keyboardWas && !kbd) HideSystemBars(); keyboardWas = kbd;
            ReadKeys();
            float dt = Mathf.Min(2f, Time.unscaledDeltaTime);
            acc += dt;
            while (acc >= .05f) { acc -= .05f; Step(.05f); }
            Frame(Mathf.Min(.05f, Time.unscaledDeltaTime));
        }

        /// <summary>20 times a second, like the web version.</summary>
        void Step(float dt)
        {
            if (Online) { ElectHost(); if (!G.isHost) PullSnap(); }
            if (G.isHost && G.ph != Phase.Home && !(paused && !Online)) G.HostStep(dt, Online ? net.Peers : null, myId, d => { if (me != null) UpdatePlayer(d); }, HumansNow);
            else if (!G.isHost && G.Live && me != null) UpdatePlayer(dt);
            if (G.isHost) { me = G.ents.FirstOrDefault(e => e.pid == myId); events.Handle(); }
            world.SyncMap();
            if (G.ph != lastPh) { lastPh = G.ph; Metrics.Screen(G.ph.ToString().ToLowerInvariant()); }
            if (freeze > 0) freeze -= dt;
            if (catchCd > 0) catchCd -= dt;
            if (me != null || G.ph != Phase.Home) { view = Viewer(); hasView = true; }
            if (G.Live && !paused) sound.Tick(dt);
            if (Online) { pubT -= dt; if (pubT <= 0) { pubT = 1 / 15f; Publish(); } }
            if (voice != null) voice.Tick(VoiceRule.CanSpeak(MicOn, me), me?.x ?? 0, me?.y ?? 0, me != null, Whisper);
            ui.Step(dt);
        }

        void Frame(float dt)
        {
            float time = Time.unscaledTime;
            if (!G.isHost) foreach (var e in G.ents) if (e != me) { float k = Mathf.Min(1, dt * 14); e.x += (e.tx - e.x) * k; e.y += (e.ty - e.y) * k; }
            bool inGame = G.Live || G.ph == Phase.Result;
            rig.Frame(dt, inGame && hasView, view);
            world.SyncEntities(G, me, Seen, TeamView, time);
            world.UpdateOcclusion(me, G.Live);
            world.Tick(dt);
            if (inGame && hasView)
            {
                float fl = 1 + Mathf.Sin(time * 9) * .012f + Mathf.Sin(time * 23) * .008f;
                var dark = new Color(0, 0, 0, view.spect ? .6f : me != null && me.hidden != null ? .985f : .95f);
                world.fog.Draw(view.x, view.y, view.r * fl, view.los, dark);
            }
            else world.fog.Hide();
            ui.Frame(dt, rig.Cam);
        }

        // ---------- input ----------
        void ReadKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) { kUp = kDown = kLeft = kRight = kSneak = false; return; }
            bool typing = ui.Typing;
            kUp = !typing && (kb.wKey.isPressed || kb.upArrowKey.isPressed);
            kDown = !typing && (kb.sKey.isPressed || kb.downArrowKey.isPressed);
            kLeft = !typing && (kb.aKey.isPressed || kb.leftArrowKey.isPressed);
            kRight = !typing && (kb.dKey.isPressed || kb.rightArrowKey.isPressed);
            kSneak = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.escapeKey.wasPressedThisFrame) Back();   // also the Android back button
            if (typing || !G.Live || paused) return;
            if (kb.eKey.wasPressedThisFrame) DoAct();
            if (kb.qKey.wasPressedThisFrame) TryPass();
            if (kb.rKey.wasPressedThisFrame) TryLaugh();
            if (kb.pKey.wasPressedThisFrame) TogglePause();
        }

        /// <summary>Esc / Android back: pause in a match, leave the room elsewhere, quit from home.</summary>
        public void Back()
        {
            if (ui.Typing) return;
            if (G.Live) { TogglePause(); return; }
            if (G.ph == Phase.Home) { if (!ui.MenuBack()) Application.Quit(); return; }
            LeaveRoom();
        }

        /// <summary>Solo: freezes the match. Online the match keeps going; the menu only covers the screen.</summary>
        public void TogglePause() { paused = !paused && G.Live; ui.ShowPause(paused, Online); if (paused) Metrics.Log("pause", ("online", Online)); }

        void OnApplicationPause(bool p) { if (p && G.Live && !Online && !paused) TogglePause(); }

        public Vector2 MoveInput()
        {
            var v = new Vector2((kRight ? 1 : 0) - (kLeft ? 1 : 0), (kDown ? 1 : 0) - (kUp ? 1 : 0)) + ui.Joy;
            return v.magnitude < .3f ? Vector2.zero : v.normalized;
        }

        // ---------- viewing ----------
        public bool TeamView => me == null || me.outOfRound || !me.IsUnice || !G.Live;

        ViewInfo Viewer()
        {
            var p = me;
            if (p == null || p.outOfRound)
            {
                Ent h = spectTarget != null ? G.ents.FirstOrDefault(e => e.pid == spectTarget && !e.outOfRound) : null;
                h ??= G.doll.holder;
                bool ok = h != null && h.x > 0;
                return new ViewInfo { x = ok ? h.x : 860, y = ok ? h.y : 590, r = Rules.ViewSpect, los = false, spect = true };
            }
            return new ViewInfo { x = p.x, y = p.y, r = p.hidden != null ? Rules.ViewHidden : p.IsUnice ? Rules.ViewUnice : Rules.ViewHider, los = true };
        }

        public bool Seen(float x, float y)
        {
            if (!hasView || U.Dist(x, y, view.x, view.y) > view.r) return false;
            return !view.los || Map.SightLine(view.x, view.y, x, y);
        }

        // ---------- events → what this player sees and hears ----------
        public string NameOf(Ent e) => e == null ? Loc.T("app.someone") : e == me ? Loc.T("app.you") : e.name;
        public string NameUp(Ent e) => e == null ? Loc.T("app.someone.up") : e == me ? Loc.T("app.you.up") : e.name;

        // ---------- this player: movement, actions, sounds ----------
        void UpdatePlayer(float dt)
        {
            var p = me; p.moving = false;
            if (!G.CanMoveNow(p) || freeze > 0 || paused) { p.loud = false; p.talking = false; return; }
            p.sneak = !p.IsUnice && (kSneak || ui.SneakOn);
            var mv = MoveInput();
            if (mv != Vector2.zero)
            {
                float sp = p.IsUnice ? Rules.SpdUnicePlayer : p.sneak ? Rules.SpdSneak : Rules.SpdRun, ox = p.x, oy = p.y;
                Game.MoveEnt(p, mv.x * sp * dt, mv.y * sp * dt); p.fx = mv.x; p.fy = mv.y;
                p.moving = U.Dist(p.x, p.y, ox, oy) > .01f;
            }
            p.loud = !p.IsUnice && p.moving && !p.sneak;
            p.talking = Talking;
            if (p.IsUnice && G.ph == Phase.Hunt && !G.isHost && catchCd <= 0)
                foreach (var h in G.ents)
                    if (!h.IsUnice && !h.outOfRound && h.hidden == null && U.Dist(p, h) < Rules.CatchR) { SendAction(Act.Catch, h.idx); catchCd = .3f; break; }
        }

        void SendAction(Act type, int arg = -1)
        {
            if (G.isHost) { G.DoAction(me, type, arg); return; }
            aSeq++; aType = type; aArg = arg; pubT = 0;
        }

        /// <summary>E: hide / leave the hiding spot, or (Unice) look inside the furniture.</summary>
        public void DoAct()
        {
            var p = me; if (p == null || p.outOfRound || !G.Live || paused) return;
            if (p.IsUnice)
            {
                if (G.ph != Phase.Hunt || p.stun > 0) return;
                var s = Game.NearSpotFurn(p);
                if (s == null) { ui.Toast(Loc.T("act.unice.need_near")); return; }
                SendAction(Act.Open, s.si); Metrics.Log("player_action", ("type", "open")); return;
            }
            if (p.hidden != null) { sfx.Play("creak"); SendAction(Act.Exit); Metrics.Log("player_action", ("type", "exit")); return; }
            var f = Game.NearSpotFurn(p);
            if (f == null) { ui.Toast(Loc.T("act.hide.need_near")); return; }
            if (f.occ != null) { ui.Toast(Loc.T("act.hide.occupied", f.occ.name)); sfx.Play("bad"); return; }
            sfx.Play("creak"); freeze = .35f; SendAction(Act.Hide, f.si); Metrics.Log("player_action", ("type", "hide"), ("spot", f.type));
        }

        /// <summary>Q: hand the doll to someone right next to you.</summary>
        public void TryPass()
        {
            var p = me; if (p == null || p.outOfRound || p.IsUnice || !G.Live || paused) return;
            if (G.doll.holder != p) { ui.Toast(Loc.T("act.pass.no_doll")); return; }
            if (p.passCd > 0) { ui.Toast(Loc.T("act.pass.cooldown", Mathf.CeilToInt(p.passCd))); sfx.Play("bad"); return; }
            if (G.PassTarget(p) == null) { ui.Toast(Loc.T("act.pass.no_target")); return; }
            SendAction(Act.Pass); Metrics.Log("player_action", ("type", "pass"));
        }

        /// <summary>R: laugh on purpose. Anyone can, but it gives away your position to the Unice like the doll's own giggle.</summary>
        public void TryLaugh()
        {
            var p = me; if (p == null || p.outOfRound || p.IsUnice || !G.Live || paused) return;
            if (p.laughCd > 0) { ui.Toast(Loc.T("act.laugh.cooldown", Mathf.CeilToInt(p.laughCd))); sfx.Play("bad"); return; }
            SendAction(Act.Laugh); Metrics.Log("player_action", ("type", "laugh"));
        }

        // ---------- rooms ----------
        public PeerInfo FindHost() => Online ? HostElection.Find(net.Peers, Time.realtimeSinceStartup) : null;

        void ElectHost()
        {
            var h = FindHost();
            if (G.isHost)
            {
                if (h != null && !h.isMe)
                {
                    G.isHost = false; lastApplied = null;
                    foreach (var p in net.Peers) if (p.isMe) p.lastSnap = null;
                    ui.Toast(Loc.T("net.host_changed", U.CleanName(h.pres.name)));
                }
                return;
            }
            if (h != null || lastS == null) return;
            var oldest = HostElection.Oldest(net.Peers, Time.realtimeSinceStartup);
            if (oldest != null && oldest.isMe) G.TakeOver(net.Peers, me, myName);
        }

        void PullSnap()
        {
            var h = FindHost();
            if (h == null || h.lastSnap == lastApplied) return;
            lastApplied = h.lastSnap;
            try { me = G.ApplySnapshot(h.lastSnap, myId, me); lastS = h.lastSnap; }
            catch (Exception x) { Metrics.Error(x, "snapshot"); return; }
            events.Handle();
        }

        void Publish()
        {
            var pr = new Presence { name = myName, kit = Kit.Pack(kit), joinT = joinT, rdy = rdy, tp = me != null ? me.tp : 0, aSeq = aSeq, aType = aType, aArg = aArg };
            if (me != null && G.CanMoveNow(me)) { pr.hasPos = true; pr.x = me.x; pr.y = me.y; pr.fx = me.fx; pr.fy = me.fy; pr.sneak = me.sneak; }
            pr.talking = Talking;
            net.SetPresence(pr);
            if (G.isHost) net.BroadcastSnapshot(G.WriteSnapshot(), G.ep);
        }

        void ResetRoomState()
        {
            code = ""; me = null; events.Reset(); lastS = null; lastApplied = null; aSeq = 0; aType = Act.None; aArg = -1; rdy = false; paused = false;
            G.ents.Clear(); G.isHost = false; G.evs.Clear(); G.aseq.Clear(); G.evSeq = 0; G.r = 0; G.unice = null; G.doll.holder = null; G.ph = Phase.Home;
            joinT = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void SetNet(string text, bool err = false) { netText = text ?? ""; netErr = err; }

        public async void EnterRoom(bool create, string joinCode)
        {
            if (busy) return;
            if (backend == null) { SetNet(Loc.T("net.unavailable"), true); return; }
            busy = true; SaveProfile();
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
            if (voice != null) { try { await voice.JoinRoomAsync(code); } catch (Exception x) { Metrics.Error(x); } }
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
            Metrics.Log(create ? "room_create" : "room_join", ("voice", voice != null));
        }

        void RoomLost(string why) { if (net == null) return; Metrics.Log("room_lost", ("phase", G.ph.ToString().ToLowerInvariant())); LeaveRoom(); SetNet(why, true); }

        public async void LeaveRoom()
        {
            var b = net; net = null;
            ResetRoomState(); myId = "me"; ui.ShowPause(false, false);
            if (voice != null) { try { await voice.LeaveRoomAsync(); } catch (Exception x) { Metrics.Error(x); } }
            if (b == null) return;
            Metrics.Log("room_leave");
            busy = true;
            try { await b.LeaveAsync(); } catch (Exception x) { Metrics.Error(x); }
            busy = false;
        }

        public void PlaySolo()
        {
            if (busy) return;
            SaveProfile(); ResetRoomState(); net = null; myId = "me"; rdy = true;
            G.isHost = true; G.ep = 1; G.ph = Phase.Lobby;
            Metrics.Log("solo_start");
        }

        public void SaveProfile() => profile.Save();
        public void SetKit(int[] k) { profile.SetKit(k); pubT = 0; }

        public struct LobbyHuman { public string id, name; public int[] k; public bool rdy, me; }

        /// <summary>Who is in the waiting room, by arrival.</summary>
        public List<LobbyHuman> LobbyHumans()
        {
            var list = new List<LobbyHuman>();
            if (!Online) { list.Add(new LobbyHuman { id = myId, name = myName, k = kit, rdy = true, me = true }); return list; }
            foreach (var p in net.Peers.Where(p => p.isMe || p.pres.name != null).OrderBy(p => p.pres.joinT).ThenBy(p => p.id, StringComparer.Ordinal))
                list.Add(p.isMe ? new LobbyHuman { id = p.id, name = myName, k = kit, rdy = rdy, me = true }
                                : new LobbyHuman { id = p.id, name = U.CleanName(p.pres.name), k = Kit.Unpack(p.pres.kit), rdy = p.pres.rdy });
            return list;
        }

        /// <summary>The humans for a new round: this device first, then the others by arrival.</summary>
        public static List<Game.Human> HumansNow()
        {
            var a = I; var list = new List<Game.Human> { new Game.Human { id = a.myId, name = a.myName, k = (int[])a.kit.Clone(), joinT = a.joinT } };
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
    }
}
