using System;
using System.Collections.Generic;
using System.Linq;

namespace Unice
{
    /// <summary>
    /// What only the host device does, on top of the rules in <see cref="Game"/>: run one fixed step of the whole match,
    /// apply what the other players send (their position and their actions), and take over the match when the previous
    /// host leaves. Guests never call this; they mirror the host's snapshots instead.
    /// </summary>
    public sealed class MatchHost
    {
        /// <summary>A remote player can't move farther than this between two updates; a bigger jump is a lag spike.</summary>
        const float MaxJump = 260;
        /// <summary>How fast the estimated speed follows each update, and how fast it decays without one.</summary>
        const float SpeedSmoothing = .25f, SpeedDecay = .7f;
        /// <summary>Estimated speed above which a remote player counts as moving, and as running (loud).</summary>
        const float MovingSpeed = 15, RunningSpeed = 60;

        readonly Game game;

        /// <summary>The last action number applied per player, so each action happens once.</summary>
        public readonly Dictionary<string, int> lastActionSeq = new Dictionary<string, int>();

        public MatchHost(Game game) { this.game = game; }

        /// <summary>New room or match: no actions applied yet.</summary>
        public void Reset() => lastActionSeq.Clear();

        /// <summary>
        /// One fixed step of the whole match: the other players' inputs, this device's own movement, the round itself,
        /// and the countdown between rounds.
        /// </summary>
        /// <param name="peers">The players in the room, or null when playing solo.</param>
        /// <param name="moveMyCharacter">Moves this device's character (runs before the rules, like the other players' input).</param>
        /// <param name="humansNow">The humans in the room right now, asked only when the next round starts.</param>
        public void Step(float dt, IReadOnlyList<PeerInfo> peers, string myPlayerId, Action<float> moveMyCharacter, Func<List<Game.Human>> humansNow)
        {
            game.hostClock += dt;
            if (peers != null) ApplyPeerInputs(peers, myPlayerId, dt);
            if (game.IsLive) { moveMyCharacter?.Invoke(dt); game.Simulate(dt); }
            else if (game.phase == Phase.Result)
            {
                game.timeLeft -= dt;
                if (game.timeLeft > 0) return;
                if (game.round >= game.totalRounds) { game.phase = Phase.Final; game.AddEvent(GameEventType.Final); }
                else game.StartRound(humansNow());
            }
        }

        /// <summary>Applies what the other players sent: their position, then their last action (once).</summary>
        public void ApplyPeerInputs(IReadOnlyList<PeerInfo> peers, string myPlayerId, float dt)
        {
            foreach (var character in game.characters)
            {
                if (character.isBot || character.playerId == myPlayerId) continue;
                var peer = peers.FirstOrDefault(candidate => candidate.id == character.playerId);
                if (peer == null)
                {
                    // the player left: a bot finishes the match in their place
                    character.BecomeBot();
                    if (game.IsLive) game.AddEvent(GameEventType.Left, actor: character.index);
                    continue;
                }

                var presence = peer.presence;
                character.talking = presence.talking;
                // position first: an action sent with it (e.g. hide on arriving at the spot) is judged from where the player is now
                ApplyPosition(character, presence, dt);
                int lastSeq = lastActionSeq.TryGetValue(peer.id, out int seq) ? seq : 0;
                if (presence.actionSeq > lastSeq)
                {
                    lastActionSeq[peer.id] = presence.actionSeq;
                    game.ApplyAction(character, presence.action, presence.actionArg);
                }
            }
        }

        /// <summary>
        /// Takes the player's position if it's plausible. The host never trusts it blindly: positions sent before the
        /// last teleport are stale, and an impossible jump sends the player back to where the host has them.
        /// </summary>
        void ApplyPosition(Character character, Presence presence, float dt)
        {
            if (presence.hasPosition && presence.teleportSeq >= character.teleportSeq && game.CanMove(character))
            {
                float jump = Geometry.Distance(presence.x, presence.y, character.x, character.y);
                if (jump < MaxJump && Map.CanStand(presence.x, presence.y))
                {
                    character.speed += (jump / dt - character.speed) * SpeedSmoothing;
                    character.x = presence.x; character.y = presence.y;
                }
                else { character.speed *= SpeedDecay; character.teleportSeq++; }   // lag spike: the player's device snaps back
                float faceLength = MathF.Sqrt(presence.faceX * presence.faceX + presence.faceY * presence.faceY);
                if (faceLength > .1f) { character.faceX = presence.faceX / faceLength; character.faceY = presence.faceY / faceLength; }
                character.sneaking = presence.sneaking;
            }
            else character.speed *= SpeedDecay;
            // remote players don't send "moving" or "loud": the host infers them from the speed
            character.moving = character.speed > MovingSpeed;
            character.loud = !character.IsUnice && character.moving && !character.sneaking && character.speed > RunningSpeed;
        }

        /// <summary>The host left: this device continues the match from the last snapshot it received.</summary>
        public void TakeOver(IReadOnlyList<PeerInfo> peers, Character myCharacter, string myName)
        {
            game.isHost = true; game.epoch += 1;
            lastActionSeq.Clear();
            foreach (var peer in peers) lastActionSeq[peer.id] = peer.presence.actionSeq;   // don't replay actions the old host already applied
            foreach (var character in game.characters)
            {
                if (character != myCharacter) { character.x = character.targetX; character.y = character.targetY; }
                bool stillHere = peers.Any(peer => peer.id == character.playerId);
                if (!character.isBot && !stillHere) character.BecomeBot();
                else character.ResetBot();
            }
            foreach (var spot in Map.Spots) spot.reservedBy = null;
            var doll = game.doll;
            if (doll.hasLastGiggle) doll.lastGiggleSpot = doll.holder != null ? doll.holder.hidingSpot : null;
            game.AddEvent(GameEventType.Host, text: myName);
        }
    }
}
