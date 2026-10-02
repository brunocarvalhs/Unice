using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Unice
{
    /// <summary>
    /// The game on this device: runs the match when hosting, mirrors the host otherwise, and drives sound, camera and screens.
    /// Same flow as the web version: home → waiting room → rounds → final.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        public static App I;
        public readonly Game G = new Game();
        internal RoomSession room; internal PlayerControl player;
        public bool Online => room.Online;
        public bool NetAvailable => NetRegistry.Factory != null;

        internal IVoiceChat voice;                        // proximity voice for the room above; null if Vivox isn't installed
        public bool MicOn { get => voice != null && voice.MicOn; set { if (voice != null) voice.MicOn = value; } }
        public bool Whisper { get; set; }        // session-only: when on, talking doesn't count as noise for the Unice, and transmits quieter

        /// <summary>Real voice activity right now, gated by the whisper toggle — the same rule used locally and sent to the host.</summary>
        internal bool Talking => voice != null && voice.IsSpeaking && !Whisper;

        public string code => room.code;
        public string myId => room.myId;
        public bool rdy { get => room.rdy; set => room.rdy = value; }
        public bool busy => room.busy;
        public string netText => room.netText;
        public bool netErr => room.netErr;
        ProfileStore profile;
        public string myName { get => profile.Name; set => profile.Name = value; }
        public int[] kit { get => profile.Kit; set => profile.Kit = value; }
        [NonSerialized] public Ent me;
        public bool paused;

        Phase lastPh = (Phase)255;
        float acc;
        [NonSerialized] public Sfx sfx; [NonSerialized] public WorldView world; [NonSerialized] public Ui ui;
        [NonSerialized] internal Soundscape sound; [NonSerialized] internal EventFeed events; [NonSerialized] internal CameraRig rig; [NonSerialized] internal Perspective pov;

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
            pov = new Perspective(this);
            sound = new Soundscape(this); events = new EventFeed(this);
            room = new RoomSession(this); player = new PlayerControl(this);
            ui = new Ui(this);
            voice = VoiceRegistry.Factory?.Invoke();
            SystemBars.Hide();
        }

        bool keyboardWas;
        void OnApplicationFocus(bool focus) { if (focus) SystemBars.Hide(); }

        // ---------- main loop ----------
        void Update()
        {
            bool kbd = TouchScreenKeyboard.visible; if (keyboardWas && !kbd) SystemBars.Hide(); keyboardWas = kbd;
            player.ReadKeys();
            float dt = Mathf.Min(2f, Time.unscaledDeltaTime);
            acc += dt;
            while (acc >= .05f) { acc -= .05f; Step(.05f); }
            Frame(Mathf.Min(.05f, Time.unscaledDeltaTime));
        }

        /// <summary>20 times a second, like the web version.</summary>
        void Step(float dt)
        {
            if (Online) { room.ElectHost(); if (!G.isHost) room.PullSnap(); }
            if (G.isHost && G.ph != Phase.Home && !(paused && !Online)) G.HostStep(dt, Online ? room.net.Peers : null, myId, d => { if (me != null) player.Move(d); }, room.HumansNow);
            else if (!G.isHost && G.Live && me != null) player.Move(dt);
            if (G.isHost) { me = G.ents.FirstOrDefault(e => e.pid == myId); events.Handle(); }
            world.SyncMap();
            if (G.ph != lastPh) { lastPh = G.ph; Metrics.Screen(G.ph.ToString().ToLowerInvariant()); }
            player.Cooldowns(dt);
            pov.Update();
            if (G.Live && !paused) sound.Tick(dt);
            if (Online) room.Tick(dt);
            if (voice != null) voice.Tick(VoiceRule.CanSpeak(MicOn, me), me?.x ?? 0, me?.y ?? 0, me != null, Whisper);
            ui.Step(dt);
        }

        void Frame(float dt)
        {
            float time = Time.unscaledTime;
            if (!G.isHost) foreach (var e in G.ents) if (e != me) { float k = Mathf.Min(1, dt * 14); e.x += (e.tx - e.x) * k; e.y += (e.ty - e.y) * k; }
            bool inGame = G.Live || G.ph == Phase.Result; var view = pov.view; bool hasView = pov.hasView;
            rig.Frame(dt, inGame && hasView, view);
            world.SyncEntities(G, me, pov.Seen, pov.TeamView, time);
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

        /// <summary>Esc / Android back: pause in a match, leave the room elsewhere, quit from home.</summary>
        public void Back()
        {
            if (ui.Typing) return;
            if (G.Live) { TogglePause(); return; }
            if (G.ph == Phase.Home) { if (!ui.MenuBack()) Application.Quit(); return; }
            room.LeaveRoom();
        }

        /// <summary>Solo: freezes the match. Online the match keeps going; the menu only covers the screen.</summary>
        public void TogglePause() { paused = !paused && G.Live; ui.ShowPause(paused, Online); if (paused) Metrics.Log("pause", ("online", Online)); }

        void OnApplicationPause(bool p) { if (p && G.Live && !Online && !paused) TogglePause(); }

        public void SaveProfile() => profile.Save();
        public void SetKit(int[] k) { profile.SetKit(k); room.PublishSoon(); }

        // ---------- what the screens call (kept here so Ui talks to one object) ----------
        public Vector2 MoveInput() => player.MoveInput();
        public void DoAct() => player.DoAct();
        public void TryPass() => player.TryPass();
        public void TryLaugh() => player.TryLaugh();
        public PeerInfo FindHost() => room.FindHost();
        public void SetNet(string text, bool err = false) => room.SetNet(text, err);
        public void EnterRoom(bool create, string joinCode) => room.EnterRoom(create, joinCode);
        public void LeaveRoom() => room.LeaveRoom();
        public void PlaySolo() => room.PlaySolo();
        internal List<RoomSession.LobbyHuman> LobbyHumans() => room.LobbyHumans();
        public void StartMatch(int bots, int rounds) => room.StartMatch(bots, rounds);
        public void NewMatchLobby() => room.NewMatchLobby();
        public void ToggleReady() => room.ToggleReady();
    }
}
