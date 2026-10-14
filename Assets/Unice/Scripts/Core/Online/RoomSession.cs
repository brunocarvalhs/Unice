using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// The room this device is in (or solo play): create, join, leave, the status line shown in the menu, who is in the
    /// waiting room, and starting a match. Keeping in sync while in a room is <see cref="RoomSync"/>'s job.
    /// </summary>
    sealed class RoomSession
    {
        /// <summary>A guest waits this long for the host's first snapshot before giving up.</summary>
        const float JoinTimeoutSeconds = 8;
        const int JoinPollMs = 250;
        /// <summary>The player id used when not in a room.</summary>
        const string OfflinePlayerId = "me";

        readonly App app;
        Game game => app.game;
        /// <summary>The room's connection while in one; null when solo or at home.</summary>
        public INetBackend connection;
        /// <summary>Created once (if the multiplayer packages are installed) and reused for every room.</summary>
        readonly INetBackend backend;
        public readonly RoomSync sync;

        public bool IsOnline => connection != null && connection.InRoom;
        public string roomCode = "", myPlayerId = OfflinePlayerId;
        /// <summary>Ready to start, in the waiting room.</summary>
        public bool ready;
        /// <summary>A room operation is in progress (creating, joining, leaving).</summary>
        public bool isBusy;
        /// <summary>When this device joined the current room (Unix ms); earlier joiners win host-election ties.</summary>
        public long joinTime;
        /// <summary>The line under the online menu ("Creating room…", an error…).</summary>
        public string statusText = "";
        public bool statusIsError;

        /// <summary>Why this device left the room or the solo match (analytics "room_leave").</summary>
        public enum LeaveReason { Button, Back, Lost, Timeout }

        /// <summary>When this device last entered the waiting room (seconds, device clock); set by <see cref="App"/> on the phase change.</summary>
        public float lobbyEnteredAt;
        /// <summary>Seconds spent in the waiting room so far, for "lobby_wait_s".</summary>
        int LobbyWaitSeconds => (int)(Time.realtimeSinceStartup - lobbyEnteredAt);

        public RoomSession(App app)
        {
            this.app = app;
            sync = new RoomSync(app, this);
            backend = NetRegistry.Factory?.Invoke();
            if (backend != null) backend.Lost += OnRoomLost;
        }

        public PeerInfo FindHost() => sync.FindHost();
        public void SetStatus(string text, bool isError = false) { statusText = text ?? ""; statusIsError = isError; }

        /// <summary>Back to a clean state before entering a room or playing solo.</summary>
        void ResetRoomState()
        {
            roomCode = ""; ready = false;
            app.myCharacter = null; app.paused = false; app.eventFeed.Reset(); app.player.Reset(); sync.Reset();
            game.characters.Clear(); game.isHost = false; game.events.Clear(); game.host.Reset(); game.eventSeq = 0;
            game.round = 0; game.unice = null; game.doll.holder = null; game.phase = Phase.Home;
            joinTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>Creates a room (this device hosts) or joins one by code (waits for the host's first snapshot).</summary>
        public async void EnterRoom(bool create, string roomCodeToJoin)
        {
            if (isBusy) return;
            if (backend == null) { SetStatus(Localization.Get("net.unavailable"), true); return; }
            isBusy = true; app.SaveProfile();
            SetStatus(Localization.Get(create ? "net.creating" : "net.joining"));
            try
            {
                await backend.InitAsync();
                if (create) await backend.CreateAsync(); else await backend.JoinAsync(roomCodeToJoin);
            }
            catch (Exception error)
            {
                isBusy = false;
                SetStatus(error is NetException ? error.Message : Localization.Get("net.conn_failed"), true);
                if (error is NetException) Debug.LogWarning(error.Message); else Metrics.Error(error, "room");   // NetException = already reported by the backend
                Metrics.Log("room_error", ("mode", create ? "create" : "join"), ("error", error.GetType().Name));
                return;
            }

            ResetRoomState();
            connection = backend; roomCode = backend.Code; myPlayerId = backend.LocalId;
            if (app.voice != null) { try { await app.voice.JoinRoomAsync(roomCode); } catch (Exception error) { Metrics.Error(error); } }
            sync.Publish();
            if (create) { game.isHost = true; game.epoch = 1; game.phase = Phase.Lobby; }
            else
            {
                float startedAt = Time.realtimeSinceStartup;
                while (IsOnline && Time.realtimeSinceStartup - startedAt < JoinTimeoutSeconds && FindHost() == null) await Awaitable.WaitForSecondsAsync(JoinPollMs / 1000f);
                if (!IsOnline) { isBusy = false; return; }
                if (FindHost() == null)
                {
                    isBusy = false; LeaveRoom(LeaveReason.Timeout); SetStatus(Localization.Get("net.no_answer"), true);
                    Metrics.Log("room_error", ("mode", "join"), ("error", "no_host"));
                    return;
                }
                sync.ApplyHostSnapshot();
            }
            isBusy = false; SetStatus("");
            Metrics.Log(create ? "room_create" : "room_join", ("voice", app.voice != null));
        }

        /// <summary>The connection dropped for good: back home, showing why.</summary>
        void OnRoomLost(string reason)
        {
            if (connection == null) return;
            Metrics.Log("room_lost", ("phase", game.phase.ToString().ToLowerInvariant()));
            LeaveRoom(LeaveReason.Lost);
            SetStatus(reason, true);
        }

        /// <summary>Back home from a room or a solo match.</summary>
        public async void LeaveRoom(LeaveReason reason)
        {
            var leaving = connection; connection = null;
            var phase = game.phase;
            Metrics.Log("room_leave", ("reason", reason.ToString().ToLowerInvariant()), ("phase", phase.ToString().ToLowerInvariant()), ("round", game.round),
                ("online", leaving != null), ("lobby_wait_s", phase == Phase.Lobby ? LobbyWaitSeconds : 0));
            ResetRoomState(); myPlayerId = OfflinePlayerId; app.ui.ShowPause(false, false);
            if (app.voice != null) { try { await app.voice.LeaveRoomAsync(); } catch (Exception error) { Metrics.Error(error); } }
            if (leaving == null) return;
            isBusy = true;
            try { await leaving.LeaveAsync(); } catch (Exception error) { Metrics.Error(error); }
            isBusy = false;
        }

        /// <summary>Solo: this device hosts a local match with bots.</summary>
        public void PlaySolo()
        {
            if (isBusy) return;
            app.SaveProfile(); ResetRoomState(); connection = null; myPlayerId = OfflinePlayerId; ready = true;
            game.isHost = true; game.epoch = 1; game.phase = Phase.Lobby;
            Metrics.Log("solo_start");
        }

        /// <summary>One person in the waiting room.</summary>
        public struct LobbyPlayer { public string id, name; public int[] kit; public bool ready, isMe; }

        /// <summary>Who is in the waiting room, by arrival.</summary>
        public List<LobbyPlayer> LobbyPlayers()
        {
            var players = new List<LobbyPlayer>();
            if (!IsOnline) { players.Add(new LobbyPlayer { id = myPlayerId, name = app.myName, kit = app.kit, ready = true, isMe = true }); return players; }
            var present = connection.Peers.Where(peer => peer.isMe || peer.presence.name != null)
                                          .OrderBy(peer => peer.presence.joinTime).ThenBy(peer => peer.id, StringComparer.Ordinal);
            foreach (var peer in present)
                players.Add(peer.isMe
                    ? new LobbyPlayer { id = peer.id, name = app.myName, kit = app.kit, ready = ready, isMe = true }
                    : new LobbyPlayer { id = peer.id, name = TextFormat.CleanName(peer.presence.name), kit = Kit.Unpack(peer.presence.kit), ready = peer.presence.ready });
            return players;
        }

        /// <summary>The humans for a new round: this device first, then the others by arrival.</summary>
        public List<Game.Human> HumansForNextRound()
        {
            var humans = new List<Game.Human> { new Game.Human { id = myPlayerId, name = app.myName, kit = (int[])app.kit.Clone(), joinTime = joinTime } };
            if (IsOnline)
                foreach (var peer in connection.Peers.Where(peer => !peer.isMe && peer.presence.name != null).OrderBy(peer => peer.presence.joinTime))
                    humans.Add(new Game.Human { id = peer.id, name = TextFormat.CleanName(peer.presence.name), kit = Kit.Unpack(peer.presence.kit), joinTime = peer.presence.joinTime });
            return humans;
        }

        /// <summary>The voice id another player sent in their presence (null if unknown or without voice).</summary>
        public string VoiceIdOf(string playerId)
        {
            if (!IsOnline) return null;
            var peer = connection.Peers.FirstOrDefault(candidate => candidate.id == playerId);
            return peer?.presence.voiceId;
        }

        /// <summary>Host, in the waiting room: starts the match.</summary>
        public void StartMatch(int bots, int rounds)
        {
            if (!game.isHost || game.phase != Phase.Lobby) return;
            game.botCount = bots; game.totalRounds = rounds; game.NewMatch(HumansForNextRound());
            Metrics.Log("match_start", ("humans", game.characters.Count(character => !character.isBot)), ("bots", game.characters.Count(character => character.isBot)),
                ("rounds", rounds), ("map", game.mapId), ("online", IsOnline), ("hide_time", (int)game.hideTime), ("round_time", (int)game.huntTime), ("lobby_wait_s", LobbyWaitSeconds));
        }

        /// <summary>Host, on the final scoreboard: everyone back to the waiting room.</summary>
        public void BackToLobby() { if (game.isHost && game.phase == Phase.Final) game.BackToLobby(); }

        public void ToggleReady() { ready = !ready; sync.PublishSoon(); }
    }
}
