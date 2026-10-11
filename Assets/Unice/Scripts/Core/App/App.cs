using System;
using System.Collections.Generic;
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
        /// <summary>
        /// At most this many fixed steps per frame (0.2 s). Further behind (a long stall, a very slow frame), the rest is
        /// dropped instead of fast-forwarding the match in one frame; a guest is corrected by the host's next snapshot.
        /// </summary>
        const int MaxStepsPerFrame = 4;
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
        /// <summary>0..1, this frame: how far it is from the last fixed step to the next. Characters are drawn that far along.</summary>
        internal float stepBlend;
        /// <summary>Back from the background: the first frame's time is the whole time away, so it is not simulated.</summary>
        bool discardElapsedTime;
        bool keyboardWasVisible;
        // made once in Awake: a lambda or method group passed in the loop would allocate a new delegate every step/frame
        Action<float> moveMyCharacter;
        Func<List<Game.Human>> humansForNextRound;
        Func<float, float, bool> canSee;

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
            UsePlayGamesName();
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
            voice = VoiceRegistry.Factory?.Invoke();   // before the UI: its settings buttons only show voice controls with voice
            ui = new Ui(this);
            moveMyCharacter = stepDt => { if (myCharacter != null) player.Move(stepDt); };
            humansForNextRound = room.HumansForNextRound; canSee = perspective.CanSee;
            SystemBars.Hide();
        }

        /// <summary>First run: the profile takes the Play Games name once that sign-in finishes (it never blocks the game).</summary>
        async void UsePlayGamesName()
        {
            if (AccountRegistry.PlayGamesName == null) return;
            try { profile.UsePlayGamesName(await AccountRegistry.PlayGamesName()); }
            catch (Exception error) { Metrics.Error(error, "play_games"); }
        }

        /// <summary>Losing focus (notification shade, another app) can swallow the finger's release: let go of the touch controls.</summary>
        void OnApplicationFocus(bool focus) { if (focus) SystemBars.Hide(); else ui.ReleaseTouchControls(); }

        /// <summary>Background: let go of the touch controls and, solo, pause the match. Back: don't catch up the time away.</summary>
        void OnApplicationPause(bool goingToBackground)
        {
            if (!goingToBackground) { discardElapsedTime = true; return; }
            ui.ReleaseTouchControls();
            if (game.IsLive && !IsOnline && !paused) TogglePause();
        }

        // ---------- main loop ----------
        void Update()
        {
            // Android shows the system bars again after the keyboard closes
            bool keyboardVisible = TouchScreenKeyboard.visible;
            if (keyboardWasVisible && !keyboardVisible) SystemBars.Hide();
            keyboardWasVisible = keyboardVisible;

            player.ReadKeys();
            if (discardElapsedTime) { discardElapsedTime = false; stepAccumulator = 0; }
            else stepAccumulator += Time.unscaledDeltaTime;
            for (int i = 0; i < MaxStepsPerFrame && stepAccumulator >= StepSeconds; i++) { stepAccumulator -= StepSeconds; Step(StepSeconds); }
            stepAccumulator %= StepSeconds;   // still behind after MaxStepsPerFrame: drop the rest
            RenderFrame(Mathf.Min(StepSeconds, Time.unscaledDeltaTime));
        }

        /// <summary>One fixed step: network, rules, events, sounds, UI.</summary>
        void Step(float dt)
        {
            foreach (var character in game.characters) character.KeepPrevious();
            if (IsOnline) { room.sync.ElectHost(); if (!game.isHost) room.sync.ApplyHostSnapshot(); }

            if (game.isHost && game.phase != Phase.Home && !(paused && !IsOnline))
                game.host.Step(dt, IsOnline ? room.connection.Peers : null, myPlayerId, moveMyCharacter, humansForNextRound);
            else if (!game.isHost && game.IsLive && myCharacter != null)
                player.Move(dt);

            if (game.isHost)
            {
                myCharacter = null;
                foreach (var character in game.characters) if (character.playerId == myPlayerId) { myCharacter = character; break; }
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
            if (voice != null) UpdateUnheard();
            ui.Step(dt);
        }

        /// <summary>Every frame: camera, characters, fog, UI.</summary>
        void RenderFrame(float dt)
        {
            float time = Time.unscaledTime;
            stepBlend = stepAccumulator / StepSeconds;
            if (!game.isHost) SmoothRemoteCharacters(dt);
            bool inGame = game.IsLive || game.phase == Phase.Result;
            bool followView = inGame && perspective.hasView;
            var drawnView = perspective.DrawnView(stepBlend);   // the camera and the fog move with the drawn character, not in steps
            cameraRig.Follow(dt, followView, drawnView);
            worldView.SyncCharacters(game, myCharacter, canSee, perspective.ShowsTeamInfo, time, stepBlend);
            worldView.UpdateOcclusion(myCharacter, game.IsLive);
            worldView.Tick(dt);
            if (followView) DrawFog(drawnView, time); else worldView.fogView.Hide();
            ui.Frame(dt, cameraRig.Camera);
        }

        /// <summary>
        /// Guests get positions ~15 times a second; in between, other characters slide towards the last one. They are drawn
        /// right where they slid to (no step blend), so their previous position follows them.
        /// </summary>
        void SmoothRemoteCharacters(float dt)
        {
            float blend = Mathf.Min(1, dt * RemoteSmoothing);
            foreach (var character in game.characters)
            {
                if (character == myCharacter) continue;
                character.x += (character.targetX - character.x) * blend;
                character.y += (character.targetY - character.y) * blend;
                character.KeepPrevious();
            }
        }

        /// <summary>Darkness around what the player can see, with a slight candle-like flicker.</summary>
        void DrawFog(ViewInfo view, float time)
        {
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
        internal int AddOnlineWin() => profile.AddOnlineWin();
        public void SetKit(int[] newKit) { profile.SetKit(newKit); room.sync.PublishSoon(); }

        public bool MicOn { get => voice != null && voice.MicOn; set { if (voice != null) voice.MicOn = value; } }
        /// <summary>Session-only: when on, talking doesn't count as noise for the Unice, and transmits quieter.</summary>
        public bool Whisper { get; set; }
        /// <summary>Real voice activity right now, gated by the whisper toggle — the same rule used locally and sent to the host.</summary>
        internal bool IsTalking => voice != null && voice.IsSpeaking && !Whisper;

        /// <summary>Another player in the room can be muted (they are online with voice, and it isn't this device).</summary>
        public bool CanMutePlayer(string playerId) => voice != null && playerId != myPlayerId && room.VoiceIdOf(playerId) != null;
        /// <summary>This device chose not to hear that player.</summary>
        public bool IsPlayerMuted(string playerId) => voice != null && voice.IsPlayerMuted(room.VoiceIdOf(playerId));
        /// <summary>Stops or resumes hearing a player, on this device only (the moderation the store requires for voice chat).</summary>
        public void TogglePlayerMuted(string playerId)
        {
            var voiceId = room.VoiceIdOf(playerId);
            if (voice == null || voiceId == null) return;
            bool muted = !voice.IsPlayerMuted(voiceId);
            voice.SetPlayerMuted(voiceId, muted);
            Metrics.Log("player_muted", ("muted", muted));
        }

        /// <summary>Reused every step: voice ids this device must not hear right now.</summary>
        readonly HashSet<string> unheard = new HashSet<string>();

        /// <summary>Applies <see cref="VoiceRule.CanHear"/>: while this device is still playing, caught players go silent for it.</summary>
        void UpdateUnheard()
        {
            unheard.Clear();
            foreach (var character in game.characters)
            {
                if (VoiceRule.CanHear(game.IsLive, myCharacter, character)) continue;
                var voiceId = room.VoiceIdOf(character.playerId);
                if (voiceId != null) unheard.Add(voiceId);
            }
            voice.SetUnheard(unheard);
        }

        /// <summary>A round or its result is on screen: the pause menu (with "leave") can open.</summary>
        bool InMatch => game.IsLive || game.phase == Phase.Result || game.phase == Phase.Final;

        /// <summary>
        /// Esc / Android back: the pause menu during a match, including the result screens (an edge swipe over the joystick
        /// must not leave the room), leave the room from the waiting room, quit from home.
        /// </summary>
        public void Back()
        {
            if (ui.Typing) return;
            if (InMatch) { TogglePause(); return; }
            if (game.phase == Phase.Home) { if (!ui.MenuBack()) Application.Quit(); return; }
            room.LeaveRoom();
        }

        /// <summary>Solo: freezes the match. Online the match keeps going; the menu only covers the screen.</summary>
        public void TogglePause()
        {
            paused = !paused && InMatch;
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
