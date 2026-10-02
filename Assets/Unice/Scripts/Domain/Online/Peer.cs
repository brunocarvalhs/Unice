namespace Unice
{
    /// <summary>
    /// What a player tells everyone else about themselves, ~15 times a second (the web version's "presence").
    /// The host reads it to move that player's character and apply their actions; see <see cref="MatchHost.ApplyPeerInputs"/>.
    /// </summary>
    public struct Presence
    {
        public string name;
        /// <summary>The look, packed with <see cref="Kit.Pack"/>.</summary>
        public int kit;
        /// <summary>When they joined the room (Unix ms). Earlier joiners win ties in the host election.</summary>
        public long joinTime;
        /// <summary>Ready to start, in the waiting room.</summary>
        public bool ready;
        /// <summary>The last <see cref="Character.teleportSeq"/> this player saw: positions sent before a teleport are ignored.</summary>
        public int teleportSeq;

        /// <summary>False while the player can't move (hidden, caught, between rounds): x/y/face are then meaningless.</summary>
        public bool hasPosition;
        public float x, y, faceX, faceY;
        public bool sneaking;
        /// <summary>Voice activity right now (counts as noise for the Unice unless whispering).</summary>
        public bool talking;

        /// <summary>Increases with every action; the host applies <see cref="action"/> once per new value.</summary>
        public int actionSeq;
        public PlayerAction action;
        /// <summary>The action's target: a spot index (Hide/Open) or a character index (Catch).</summary>
        public int actionArg;
    }

    /// <summary>Another device in the room as this one knows it: its last presence and last snapshot.</summary>
    public sealed class PeerInfo
    {
        /// <summary>Network id of the device (also the playerId of its character).</summary>
        public string id;
        public bool isMe;
        public Presence presence;
        /// <summary>When their presence last arrived (Time.realtimeSinceStartup). Old = app paused or gone.</summary>
        public float presenceAt = -999;

        /// <summary>The last snapshot they broadcast; only the host sends them.</summary>
        public byte[] lastSnapshot;
        public int lastSnapshotEpoch;
        public float lastSnapshotAt = -999;
    }
}
