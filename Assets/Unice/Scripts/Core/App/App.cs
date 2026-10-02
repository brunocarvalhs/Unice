using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Unice
{
    /// <summary>
    /// The game on this device. Creates every part (world, sound, UI, room, player) and runs the main loop: a fixed step
    /// 20 times a second (rules, network, sounds — like the web version) and a render every frame.
    /// Runs the match when hosting and mirrors the host otherwise. Flow: home → waiting room → rounds → final.
    /// The screens talk to the rest of the game through the public members at the bottom.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        /// <summary>The fixed step: 20 times a second, like the web version.</summary>
        const float StepSeconds = .05f;
        /// <summary>After a long stall (app in background), don't try to catch up more than this.</summary>
        const float MaxCatchUpSeconds = 2;
        /// <summary>How fast other characters slide to where the host says they are (guests only).</summary>
        const float RemoteSmoothing = 14;

        public static App Instance;
        public readonly Game game = new Game();

        // ---------- parts ----------
        [NonSerialized] public Sfx sfx;
        [NonSerialized] public WorldView worldView;
        [NonSerialized] public Ui ui;
        internal RoomSession room;
        internal PlayerControl player;
        internal Perspective perspective;
        internal Soundscape soundscape;
        internal Heartbeat heartbeat;
        internal EventFeed eventFeed;
        internal CameraRig cameraRig;
        /// <summary>Proximity voice for the room; null if Vivox isn't installed.</summary>
        internal IVoiceChat voice;
        ProfileStore profile;

        // ---------- state ----------
        /// <summary>This device's character in the match, or null (at home, in the waiting room, or only watching).</summary>
        [NonSerialized] public Character myCharacter;
        /// <summary>The pause menu is open (solo: the match is frozen).</summary>
        public bool paused;
        Phase lastPhase = (Phase)255;   // none yet: the first step logs the screen
        float stepAccumulator;
        bool keyboardWasVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;   // Play mode without domain reload

        /// <summary>The game needs no scene setup: it creates itself after the first scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Instance == null) new GameObject("Unice").AddComponent<App>(); }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60; Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Metrics.Init();
            profile = new ProfileStore();
            cameraRig = new CameraRig();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem"); DontDestroyOnLoad(eventSystem);
                eventSystem.AddComponent<EventSystem>(); eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            worldView = new WorldView(transform);
            sfx = new Sfx(transform);
            perspective = new Perspective(this);
            soundscape = new Soundscape(this); heartbeat = new Heartbeat(this); eventFeed = new EventFeed(this);
            room = new RoomSession(this); player = new PlayerControl(this);
            ui = new Ui(this);
            voice = VoiceRegistry.Factory?.Invoke();
            SystemBars.Hide();
        }

        void OnApplicationFocus(bool focus) { if (focus) SystemBars.Hide(); }

        /// <summary>Solo: going to the background pauses the match.</summary>
        void OnApplicationPause(bool goingToBackground) { if (goingToBackground && game.IsLive && !IsOnline && !paused) TogglePause(); }

        // ---------- main loop ----------
        void Update()
        {
            // Android shows the system bars again after the keyboard closes
            bool keyboardVisible = TouchScreenKeyboard.visible;
            if (keyboardWasVisible && !keyboardVisible) SystemBars.Hide();
            keyboardWasVisible = keyboardVisible;

            player.ReadKeys();
            stepAccumulator += Mathf.Min(MaxCatchUpSeconds, Time.unscaledDeltaTime);
            while (stepAccumulator >= StepSeconds) { stepAccumulator -= StepSeconds; Step(StepSeconds); }
            RenderFrame(Mathf.Min(StepSeconds, Time.unscaledDeltaTime));
        }

        /// <summary>One fixed step: network, rules, events, sounds, UI.</summary>
        void Step(float dt)
        {
            if (IsOnline) { room.sync.ElectHost(); if (!game.isHost) room.sync.ApplyHostSnapshot(); }

            if (game.isHost && game.phase != Phase.Home && !(paused && !IsOnline))
                game.HostStep(dt, IsOnline ? room.connection.Peers : null, myPlayerId, stepDt => { if (myCharacter != null) player.Move(stepDt); }, room.HumansForNextRound);
            else if (!game.isHost && game.IsLive && myCharacter != null)
                player.Move(dt);

            if (game.isHost)
            {
                myCharacter = game.characters.FirstOrDefault(character => character.playerId == myPlayerId);
                eventFeed.PlayNewEvents();   // guests play events when a snapshot arrives
            }
            worldView.SyncMap();
            if (game.phase != lastPhase) { lastPhase = game.phase; Metrics.Screen(game.phase.ToString().ToLowerInvariant()); }
            player.TickCooldowns(dt);
            perspective.Update();
            if (game.IsLive && !paused)
            {
                soundscape.Tick(dt);
                player.UpdateNoiseLevel(dt);
                heartbeat.Tick(dt);
            }
            if (IsOnline) room.sync.Tick(dt);
            voice?.Tick(VoiceRule.CanSpeak(MicOn, myCharacter), myCharacter?.x ?? 0, myCharacter?.y ?? 0, myCharacter != null, Whisper);
            ui.Step(dt);
        }

        /// <summary>Every frame: camera, characters, fog, UI.</summary>
        void RenderFrame(float dt)
        {
            float time = Time.unscaledTime;
            if (!game.isHost) SmoothRemoteCharacters(dt);
            bool inGame = game.IsLive || game.phase == Phase.Result;
            bool followView = inGame && perspective.hasView;
            cameraRig.Follow(dt, followView, perspective.view);
            worldView.SyncCharacters(game, myCharacter, perspective.CanSee, perspective.ShowsTeamInfo, time);
            worldView.UpdateOcclusion(myCharacter, game.IsLive);
            worldView.Tick(dt);
            if (followView) DrawFog(time); else worldView.fogView.Hide();
            ui.Frame(dt, cameraRig.Camera);
        }

        /// <summary>Guests get positions ~15 times a second; in between, other characters slide towards the last one.</summary>
        void SmoothRemoteCharacters(float dt)
        {
            float blend = Mathf.Min(1, dt * RemoteSmoothing);
            foreach (var character in game.characters)
            {
                if (character == myCharacter) continue;
                character.x += (character.targetX - character.x) * blend;
                character.y += (character.targetY - character.y) * blend;
            }
        }

        /// <summary>Darkness around what the player can see, with a slight candle-like flicker.</summary>
        void DrawFog(float time)
        {
            var view = perspective.view;
            float flicker = 1 + Mathf.Sin(time * 9) * .012f + Mathf.Sin(time * 23) * .008f;
            bool hidden = myCharacter != null && myCharacter.hidingSpot != null;
            var darkness = new Color(0, 0, 0, view.spectating ? .6f : hidden ? .985f : .95f);
            worldView.fogView.Draw(view.x, view.y, view.radius * flicker, view.usesLineOfSight, darkness);
        }

        // ---------- what the screens call (kept here so the UI talks to one object) ----------
        public bool IsOnline => room.IsOnline;
        /// <summary>The multiplayer packages are installed (otherwise only solo play).</summary>
        public bool NetAvailable => NetRegistry.Factory != null;
        public string roomCode => room.roomCode;
        public string myPlayerId => room.myPlayerId;
        /// <summary>Ready to start, in the waiting room.</summary>
        public bool ready { get => room.ready; set => room.ready = value; }
        /// <summary>A room operation is in progress (creating, joining, leaving): buttons wait.</summary>
        public bool isBusy => room.isBusy;
        public string netStatusText => room.statusText;
        public bool netStatusIsError => room.statusIsError;

        public string myName { get => profile.Name; set => profile.Name = value; }
        public int[] kit { get => profile.Kit; set => profile.Kit = value; }
        public void SaveProfile() => profile.Save();
        public void SetKit(int[] newKit) { profile.SetKit(newKit); room.sync.PublishSoon(); }

        public bool MicOn { get => voice != null && voice.MicOn; set { if (voice != null) voice.MicOn = value; } }
        /// <summary>Session-only: when on, talking doesn't count as noise for the Unice, and transmits quieter.</summary>
        public bool Whisper { get; set; }
        /// <summary>Real voice activity right now, gated by the whisper toggle — the same rule used locally and sent to the host.</summary>
        internal bool IsTalking => voice != null && voice.IsSpeaking && !Whisper;

        /// <summary>Esc / Android back: pause in a match, leave the room elsewhere, quit from home.</summary>
        public void Back()
        {
            if (ui.Typing) return;
            if (game.IsLive) { TogglePause(); return; }
            if (game.phase == Phase.Home) { if (!ui.MenuBack()) Application.Quit(); return; }
            room.LeaveRoom();
        }

        /// <summary>Solo: freezes the match. Online the match keeps going; the menu only covers the screen.</summary>
        public void TogglePause()
        {
            paused = !paused && game.IsLive;
            ui.ShowPause(paused, IsOnline);
            if (paused) Metrics.Log("pause", ("online", IsOnline));
        }

        public Vector2 MoveInput() => player.MoveInput();
        public void UseNearbySpot() => player.UseNearbySpot();
        public void TryPassDoll() => player.TryPassDoll();
        public void TryLaugh() => player.TryLaugh();
        public PeerInfo FindHost() => room.FindHost();
        public void SetNetStatus(string text, bool isError = false) => room.SetStatus(text, isError);
        public void EnterRoom(bool create, string roomCodeToJoin) => room.EnterRoom(create, roomCodeToJoin);
        public void LeaveRoom() => room.LeaveRoom();
        public void PlaySolo() => room.PlaySolo();
        internal List<RoomSession.LobbyPlayer> LobbyPlayers() => room.LobbyPlayers();
        public void StartMatch(int bots, int rounds) => room.StartMatch(bots, rounds);
        public void BackToLobby() => room.BackToLobby();
        public void ToggleReady() => room.ToggleReady();
    }
}
