using System;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// Keeping this device in sync with the room, every step while online: deciding who hosts, applying the host's
    /// snapshots (guests), and sending this device's presence ~15 times a second (plus the snapshot, when hosting).
    /// </summary>
    sealed class RoomSync
    {
        const float PublishInterval = 1 / 15f;

        readonly App app;
        readonly RoomSession session;
        Game game => app.game;
        /// <summary>The last snapshot applied without errors: needed to take over if the host leaves.</summary>
        byte[] lastGoodSnapshot;
        /// <summary>The last snapshot looked at (applied or not), so the same bytes aren't decoded twice.</summary>
        byte[] lastSeenSnapshot;
        float publishTimer;

        public RoomSync(App app, RoomSession session) { this.app = app; this.session = session; }

        /// <summary>Forget the previous room's snapshots.</summary>
        public void Reset() { lastGoodSnapshot = null; lastSeenSnapshot = null; }

        public PeerInfo FindHost() => session.IsOnline ? HostElection.FindHost(session.connection.Peers, Time.realtimeSinceStartup) : null;

        /// <summary>Steps down when another device is the host; takes over when no one is and this device joined first.</summary>
        public void ElectHost()
        {
            var host = FindHost();
            var peers = session.connection.Peers;
            if (game.isHost)
            {
                if (host == null || host.isMe) return;
                game.isHost = false; lastSeenSnapshot = null;
                foreach (var peer in peers) if (peer.isMe) peer.lastSnapshot = null;
                app.ui.Toast(Localization.Get("net.host_changed", TextFormat.CleanName(host.presence.name)));
                return;
            }
            if (host != null || lastGoodSnapshot == null) return;   // there is a host, or there's no match to continue
            var oldest = HostElection.FindOldestPresent(peers, Time.realtimeSinceStartup);
            if (oldest != null && oldest.isMe) game.host.TakeOver(peers, app.myCharacter, app.myName);
        }

        /// <summary>Guest: applies the host's newest snapshot, then plays its new events.</summary>
        public void ApplyHostSnapshot()
        {
            var host = FindHost();
            if (host == null || host.lastSnapshot == lastSeenSnapshot) return;
            lastSeenSnapshot = host.lastSnapshot;
            try
            {
                app.myCharacter = game.ApplySnapshot(host.lastSnapshot, session.myPlayerId, app.myCharacter);
                lastGoodSnapshot = host.lastSnapshot;
            }
            catch (Exception error) { Metrics.Error(error, "snapshot"); return; }
            app.eventFeed.PlayNewEvents();
        }

        /// <summary>Per step while online.</summary>
        public void Tick(float dt)
        {
            publishTimer -= dt;
            if (publishTimer <= 0) { publishTimer = PublishInterval; Publish(); }
        }

        /// <summary>Sends this device's presence on the next step instead of waiting for the regular beat.</summary>
        public void PublishSoon() => publishTimer = 0;

        /// <summary>Sends this device's presence (and, when hosting, the match snapshot).</summary>
        public void Publish()
        {
            var me = app.myCharacter; var player = app.player;
            var presence = new Presence
            {
                name = app.myName, kit = Kit.Pack(app.kit), joinTime = session.joinTime, ready = session.ready,
                teleportSeq = me != null ? me.teleportSeq : 0,
                actionSeq = player.actionSeq, action = player.action, actionArg = player.actionArg,
                talking = app.IsTalking,
                voiceId = app.voice?.VoicePlayerId,
            };
            if (me != null && game.CanMove(me))
            {
                presence.hasPosition = true;
                presence.x = me.x; presence.y = me.y; presence.faceX = me.faceX; presence.faceY = me.faceY; presence.sneaking = me.sneaking;
            }
            session.connection.SetPresence(presence);
            if (game.isHost) session.connection.BroadcastSnapshot(game.WriteSnapshot(), game.epoch);
        }
    }
}
