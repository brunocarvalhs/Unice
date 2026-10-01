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

        public string code = "", myId = "me", myName; public int[] kit; public bool rdy; public long joinT;
        [NonSerialized] public Ent me; [NonSerialized] public ViewInfo view; public bool hasView;
        public bool paused, busy;
        public string netText = ""; public bool netErr;
        public float heartI, beat;

        int lastEv = -1; byte[] lastS, lastApplied;
        float pubT, freeze, catchCd, acc, ownT, clockT, heartT, camX = 860, camY = 590;
        int aSeq, aArg, tickS; Act aType;
        readonly Dictionary<int, float> stepT = new Dictionary<int, float>();
        [NonSerialized] public Sfx sfx; [NonSerialized] public WorldView world; [NonSerialized] public Ui ui; Camera cam;
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
            myName = U.CleanName(PlayerPrefs.GetString("unice.name", "Nova"));
            kit = PlayerPrefs.HasKey("unice.kit") ? Kit.Unpack(PlayerPrefs.GetInt("unice.kit")) : Kit.Random();
            cam = Camera.main;
            if (cam == null) { cam = new GameObject("Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.nearClipPlane = .1f; cam.farClipPlane = 50;
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem"); DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            world = new WorldView(transform);
            sfx = new Sfx(transform);
            ui = new Ui(this);
            backend = NetRegistry.Factory?.Invoke();
            if (backend != null) backend.Lost += RoomLost;
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
                    catch (Exception x) { Debug.LogWarning(x); }
                }));
            }
            catch (Exception x) { Debug.LogWarning(x); }
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
            if (G.isHost) { me = G.ents.FirstOrDefault(e => e.pid == myId); HandleEvents(); }
            if (freeze > 0) freeze -= dt;
            if (catchCd > 0) catchCd -= dt;
            if (me != null || G.ph != Phase.Home) { view = Viewer(); hasView = true; }
            if (G.Live && !paused) LocalSounds(dt);
            if (Online) { pubT -= dt; if (pubT <= 0) { pubT = 1 / 15f; Publish(); } }
            ui.Step(dt);
        }

        void Frame(float dt)
        {
            float time = Time.unscaledTime;
            if (!G.isHost) foreach (var e in G.ents) if (e != me) { float k = Mathf.Min(1, dt * 14); e.x += (e.tx - e.x) * k; e.y += (e.ty - e.y) * k; }
            bool inGame = G.Live || G.ph == Phase.Result;
            if (inGame && hasView) { float k = Mathf.Min(1, dt * 6); camX += (view.x - camX) * k; camY += (view.y - 20 - camY) * k; }
            else { camX = 860 + Mathf.Sin(time * .1f) * 520; camY = 590 + Mathf.Sin(time * .16f) * 300; }
            cam.orthographicSize = Mathf.Max(250, 350 / Mathf.Max(.5f, cam.aspect)) / Art.PxPerUnit;
            cam.transform.position = Art.W(camX, camY, -10);
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
            ui.Frame(dt, cam);
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
        public void TogglePause() { paused = !paused && G.Live; ui.ShowPause(paused, Online); }

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
                var h = G.doll.holder; bool ok = h != null && h.x > 0;
                return new ViewInfo { x = ok ? h.x : 860, y = ok ? h.y : 590, r = Rules.ViewSpect, los = false, spect = true };
            }
            return new ViewInfo { x = p.x, y = p.y, r = p.hidden != null ? Rules.ViewHidden : p.IsUnice ? Rules.ViewUnice : Rules.ViewHider, los = true };
        }

        public bool Seen(float x, float y)
        {
            if (!hasView || U.Dist(x, y, view.x, view.y) > view.r) return false;
            return !view.los || Map.SightLine(view.x, view.y, x, y);
        }

        static string Side(float dx, float dy)
        {
            float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
            if (ax < 60 && ay < 60) return "bem do lado";
            string h = dx < 0 ? "à esquerda" : "à direita", v = dy < 0 ? "acima" : "abaixo";
            return ax > ay * 1.6f ? h : ay > ax * 1.6f ? v : v + ", " + h;
        }

        /// <summary>Plays a sound as heard from (x, y): quieter with distance, panned, muffled through walls. Returns the loudness.</summary>
        float Hear(float x, float y, float radius, string label, Color col, string snd, float minG = 0)
        {
            if (!hasView) return 0;
            var L = view; bool hid = me != null && me.hidden != null;
            float r = radius * (hid ? 1.3f : 1), d = U.Dist(x, y, L.x, L.y);
            float g = Mathf.Max(d >= r ? 0 : Mathf.Pow(1 - d / r, 2), minG);
            if (g <= .01f) return 0;
            sfx.Play(snd, g, Mathf.Clamp((x - L.x) / 300, -1, 1), d > 40 && !Map.SightLine(L.x, L.y, x, y));
            if (label != null && me != null && !me.outOfRound && !Seen(x, y))
            {
                ui.Cue(Mathf.Atan2(y - L.y, x - L.x), g, col);
                if (hid) ui.Caption($"{label} · {(g > .45f ? "muito perto" : g > .15f ? "perto" : "longe")} · {Side(x - L.x, y - L.y)}");
            }
            return g;
        }

        // ---------- events → what this player sees and hears ----------
        void HandleEvents()
        {
            if (lastEv < 0) { lastEv = G.evSeq; return; }
            foreach (var e in G.evs.ToArray())
                if (e.seq > lastEv) { lastEv = e.seq; try { OnEvent(e); } catch (Exception x) { Debug.LogWarning(x); } }
            if (G.evSeq < lastEv - 50) lastEv = G.evSeq;
        }

        public string NameOf(Ent e) => e == null ? "alguém" : e == me ? "você" : e.name;
        public string NameUp(Ent e) => e == null ? "Alguém" : e == me ? "Você" : e.name;

        void OnEvent(GameEvent v)
        {
            Ent E(int i) => G.EntAt(i);
            switch (v.type)
            {
                case EvType.Start:
                {
                    var u = E(v.b); var h = E(v.c);
                    if (me == null) ui.Banner($"Rodada {v.a}: você assiste. {NameUp(u)} é a Unice", "info", 3);
                    else if (me == u) ui.Banner("Você é a Unice! Pegaram sua boneca. Espere eles se esconderem", "rose", 3.2f);
                    else if (me == h) ui.Banner("A boneca está com você! Esconda-se", "rose", 3.2f);
                    else ui.Banner($"{NameUp(u)} é a Unice. Esconda-se!", "candle", 3.2f);
                    break;
                }
                case EvType.Enter:
                    sfx.Play("boom"); Hear(Map.DoorIn.x, Map.DoorIn.y, 1400, "A porta da frente abriu", Ui.Rose, "creak", .35f);
                    ui.Banner(G.unice == me ? "Quem mexeu nas minhas coisas?!" : "A Unice chegou em casa!", "rose", 2.4f);
                    break;
                case EvType.Giggle:
                {
                    float x = v.a, y = v.b; var r = Map.RoomOf(x, y);
                    world.Ripple(x, y, "giggle"); ui.Fx(x, y - 30, "hi hi hi!", Ui.Rose, 18, 1.8f, true);
                    Hear(x, y, 2200, "Risadinha", Ui.Rose, "giggle", .3f);
                    if (me != null && me.IsUnice && G.Live) ui.Banner($"Risadinha {r.inPhrase}!", "rose", 2);
                    else if (me != null && G.doll.holder == me) ui.Banner("A boneca riu! Troque de esconderijo", "rose", 2.4f);
                    else ui.Toast($"A boneca riu {r.inPhrase}");
                    break;
                }
                case EvType.Pass:
                {
                    var A = E(v.a); var B = E(v.b); if (A == null || B == null) break;
                    Hear(A.x, A.y, 220, "Cochicho", Ui.Rose, "swish");
                    if (TeamView || Seen(A.x, A.y)) ui.Fx((A.x + B.x) / 2, (A.y + B.y) / 2 - 26, "psiu!", Ui.Rose, 14, 1);
                    if (B == me) ui.Banner("Você recebeu a boneca!", "rose", 2);
                    else if (A == me) ui.Banner($"Boneca com {B.name}", "rose", 1.5f);
                    else if (TeamView) ui.Toast($"{A.name} passou a boneca para {B.name}");
                    break;
                }
                case EvType.Open:
                {
                    if (v.a < 0 || v.a >= Map.Spots.Length) break;
                    var s = Map.Spots[v.a];
                    Hear(s.Cx, s.Cy, 450, "Um móvel rangeu", Ui.Candle, "creak");
                    if (v.b == 0 && Seen(s.Cx, s.Cy)) ui.Fx(s.ax, s.ay - 24, "vazio", Ui.Muted, 14, 1);
                    break;
                }
                case EvType.Catch:
                {
                    var Hh = E(v.b); var Un = E(v.a); bool withDoll = v.c != 0; float x = v.d, y = v.e; if (Hh == null) break;
                    sfx.Play("sting"); ui.Fx(x, y - 26, withDoll ? "É MINHA!" : "PEGO!", Ui.Blood, withDoll ? 24 : 20, 1.8f, true);
                    if (Hh == me) ui.Banner(withDoll ? "A Unice pegou a boneca de volta com você!" : "A Unice te pegou! Você sai da rodada", "blood", 2.6f);
                    else if (Un == me && !withDoll) ui.Banner($"{Hh.name} não estava com a boneca", "candle", 2);
                    else if (!withDoll) ui.Toast($"A Unice pegou {Hh.name} {Map.RoomOf(x, y).inPhrase}");
                    break;
                }
                case EvType.End:
                {
                    bool unWin = v.a != 0;
                    if (me != null) sfx.Play(unWin == me.IsUnice ? "win" : "lose");
                    if (!unWin) ui.Banner("O tempo acabou!", "moss", 1.6f);
                    heartI = 0;
                    break;
                }
                case EvType.Host:
                    if (!G.isHost || U.CleanName(v.s) != myName) ui.Toast($"{U.CleanName(v.s)} agora é o anfitrião da sala.");
                    else ui.Toast("Você agora é o anfitrião. Deixe o jogo aberto.");
                    break;
                case EvType.Left: { var e = E(v.a); if (e != null) ui.Toast($"{e.name} saiu. Um robô assumiu no lugar."); break; }
            }
        }

        // ---------- this player: movement, actions, sounds ----------
        void UpdatePlayer(float dt)
        {
            var p = me; p.moving = false;
            if (!G.CanMoveNow(p) || freeze > 0 || paused) { p.loud = false; return; }
            p.sneak = !p.IsUnice && (kSneak || ui.SneakOn);
            var mv = MoveInput();
            if (mv != Vector2.zero)
            {
                float sp = p.IsUnice ? Rules.SpdUnicePlayer : p.sneak ? Rules.SpdSneak : Rules.SpdRun, ox = p.x, oy = p.y;
                Game.MoveEnt(p, mv.x * sp * dt, mv.y * sp * dt); p.fx = mv.x; p.fy = mv.y;
                p.moving = U.Dist(p.x, p.y, ox, oy) > .01f;
            }
            p.loud = !p.IsUnice && p.moving && !p.sneak;
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
                if (s == null) { ui.Toast("Chegue perto de um móvel com contorno tracejado para olhar dentro."); return; }
                SendAction(Act.Open, s.si); return;
            }
            if (p.hidden != null) { sfx.Play("creak"); SendAction(Act.Exit); return; }
            var f = Game.NearSpotFurn(p);
            if (f == null) { ui.Toast("Chegue perto de um móvel com contorno tracejado para se esconder."); return; }
            if (f.occ != null) { ui.Toast($"{f.occ.name} já está escondido ali."); sfx.Play("bad"); return; }
            sfx.Play("creak"); freeze = .35f; SendAction(Act.Hide, f.si);
        }

        /// <summary>Q: hand the doll to someone right next to you.</summary>
        public void TryPass()
        {
            var p = me; if (p == null || p.outOfRound || p.IsUnice || !G.Live || paused) return;
            if (G.doll.holder != p) { ui.Toast("Você não está com a boneca."); return; }
            if (p.hidden != null) { ui.Toast("Saia do esconderijo para passar a boneca."); return; }
            if (p.passCd > 0) { ui.Toast($"Você acabou de receber a boneca. Espere {Mathf.CeilToInt(p.passCd)} s."); sfx.Play("bad"); return; }
            if (G.PassTarget(p) == null) { ui.Toast("Ninguém perto o bastante. Encoste em alguém para passar."); return; }
            SendAction(Act.Pass);
        }

        void LocalSounds(float dt)
        {
            foreach (var e in G.ents)
            {
                if (e == me || e.outOfRound || e.hidden != null || !e.moving || e.x < 0) continue;
                stepT.TryGetValue(e.idx, out float st); st -= dt; stepT[e.idx] = st;
                if (st > 0) continue;
                if (e.IsUnice) { stepT[e.idx] = .45f; if (TeamView) Hear(e.x, e.y, 380, "Bengala batendo", Ui.Rose, "cane"); }
                else if (e.loud)
                {
                    stepT[e.idx] = .42f;
                    float g = Hear(e.x, e.y, Rules.NoiseR, "Passos correndo", Ui.Candle, "step");
                    if (g > 0 && me != null && me.IsUnice) world.Ripple(e.x, e.y, "step");
                }
            }
            if (me != null && me.loud) { ownT -= dt; if (ownT <= 0) { ownT = .42f; world.Ripple(me.x, me.y, "own"); } }
            clockT -= dt; if (clockT <= 0) { clockT = 1; Hear(1652, 182, 260, null, default, "clock"); }
            if (me != null) me.noise += ((me.loud ? 1 : 0) - me.noise) * Mathf.Min(1, dt * 6);
            var Un = G.unice;
            if (G.ph == Phase.Hunt && me != null && !me.IsUnice && !me.outOfRound && Un != null && Un.x > 0)
            {
                float d = U.Dist(me, Un); heartI = d < Rules.HeartR ? 1 - d / Rules.HeartR : 0;
                heartT -= dt;
                if (heartI > 0 && heartT <= 0) { heartT = .42f + (1 - heartI) * .7f; beat = 1; sfx.Play("heart"); }
            }
            else heartI = 0;
            if (G.ph == Phase.Hunt) { int s = Mathf.CeilToInt(G.tl); if (s <= 10 && s > 0 && s != tickS) { tickS = s; sfx.Play("tick"); } }
        }

        // ---------- rooms ----------
        /// <summary>The host is whoever sends the freshest snapshot with the highest epoch (ties: who joined first).</summary>
        public PeerInfo FindHost()
        {
            if (!Online) return null;
            float now = Time.realtimeSinceStartup; PeerInfo h = null;
            foreach (var p in net.Peers)
            {
                if (p.lastSnap == null || (!p.isMe && now - p.lastSnapAt > 4)) continue;
                if (h == null || p.lastSnapEp > h.lastSnapEp || (p.lastSnapEp == h.lastSnapEp && Older(p, h))) h = p;
            }
            return h;
        }
        static bool Older(PeerInfo a, PeerInfo b) => a.pres.joinT < b.pres.joinT || (a.pres.joinT == b.pres.joinT && string.CompareOrdinal(a.id, b.id) < 0);

        void ElectHost()
        {
            var h = FindHost();
            if (G.isHost)
            {
                if (h != null && !h.isMe)
                {
                    G.isHost = false; lastApplied = null;
                    foreach (var p in net.Peers) if (p.isMe) p.lastSnap = null;
                    ui.Toast($"{U.CleanName(h.pres.name)} é o anfitrião agora.");
                }
                return;
            }
            if (h != null || lastS == null) return;
            PeerInfo oldest = null; float now = Time.realtimeSinceStartup;
            foreach (var p in net.Peers) if ((p.isMe || (p.pres.name != null && now - p.presAt < 4)) && (oldest == null || Older(p, oldest))) oldest = p;
            if (oldest != null && oldest.isMe) G.TakeOver(net.Peers, me, myName);
        }

        void PullSnap()
        {
            var h = FindHost();
            if (h == null || h.lastSnap == lastApplied) return;
            lastApplied = h.lastSnap;
            try { me = G.ApplySnapshot(h.lastSnap, myId, me); lastS = h.lastSnap; }
            catch (Exception x) { Debug.LogWarning("snapshot: " + x.Message); return; }
            HandleEvents();
        }

        void Publish()
        {
            var pr = new Presence { name = myName, kit = Kit.Pack(kit), joinT = joinT, rdy = rdy, tp = me != null ? me.tp : 0, aSeq = aSeq, aType = aType, aArg = aArg };
            if (me != null && G.CanMoveNow(me)) { pr.hasPos = true; pr.x = me.x; pr.y = me.y; pr.fx = me.fx; pr.fy = me.fy; pr.sneak = me.sneak; }
            net.SetPresence(pr);
            if (G.isHost) net.BroadcastSnapshot(G.WriteSnapshot(), G.ep);
        }

        void ResetRoomState()
        {
            code = ""; me = null; lastEv = -1; lastS = null; lastApplied = null; aSeq = 0; aType = Act.None; aArg = -1; rdy = false; paused = false;
            G.ents.Clear(); G.isHost = false; G.evs.Clear(); G.aseq.Clear(); G.evSeq = 0; G.r = 0; G.unice = null; G.doll.holder = null; G.ph = Phase.Home;
            joinT = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public void SetNet(string text, bool err = false) { netText = text ?? ""; netErr = err; }

        public async void EnterRoom(bool create, string joinCode)
        {
            if (busy) return;
            if (backend == null) { SetNet("O modo online não está instalado nesta versão. Jogue sozinho com robôs.", true); return; }
            busy = true; SaveProfile();
            SetNet(create ? "Criando a sala…" : "Procurando a sala…");
            try
            {
                await backend.InitAsync();
                if (create) await backend.CreateAsync(); else await backend.JoinAsync(joinCode);
            }
            catch (Exception x) { busy = false; SetNet(x is NetException ? x.Message : "A conexão falhou. Confira a internet e tente de novo.", true); Debug.LogWarning(x); return; }
            ResetRoomState();
            net = backend; code = backend.Code; myId = backend.MyId;
            Publish();
            if (create) { G.isHost = true; G.ep = 1; G.ph = Phase.Lobby; }
            else
            {
                float t0 = Time.realtimeSinceStartup;
                while (Online && Time.realtimeSinceStartup - t0 < 8 && FindHost() == null) await Task.Delay(250);
                if (!Online) { busy = false; return; }
                if (FindHost() == null) { busy = false; LeaveRoom(); SetNet("A sala não respondeu. Confira o código ou peça um novo.", true); return; }
                PullSnap();
            }
            busy = false; SetNet("");
        }

        void RoomLost(string why) { if (net == null) return; LeaveRoom(); SetNet(why, true); }

        public async void LeaveRoom()
        {
            var b = net; net = null;
            ResetRoomState(); myId = "me"; ui.ShowPause(false, false);
            if (b == null) return;
            busy = true;
            try { await b.LeaveAsync(); } catch (Exception x) { Debug.LogWarning(x); }
            busy = false;
        }

        public void PlaySolo()
        {
            if (busy) return;
            SaveProfile(); ResetRoomState(); net = null; myId = "me"; rdy = true;
            G.isHost = true; G.ep = 1; G.ph = Phase.Lobby;
        }

        public void SaveProfile() { PlayerPrefs.SetString("unice.name", myName); PlayerPrefs.SetInt("unice.kit", Kit.Pack(kit)); PlayerPrefs.Save(); }
        public void SetKit(int[] k) { kit = Kit.Clean(k); SaveProfile(); pubT = 0; }

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
        }

        public void NewMatchLobby() { if (G.isHost && G.ph == Phase.Final) G.BackToLobby(); }
        public void ToggleReady() { rdy = !rdy; pubT = 0; }
    }
}
