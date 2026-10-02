using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// One per player in the room. Its owner sends their presence ~15 times a second and, when hosting, the match snapshots.
    /// Everything received is checked: it must come from this object's owner and decode cleanly.
    /// </summary>
    public sealed class NetPlayer : NetworkBehaviour
    {
        /// <summary>Anything bigger is not a real presence / snapshot: dropped.</summary>
        const int MaxPresenceBytes = 512, MaxSnapshotBytes = 8192;

        /// <summary>Every spawned player object in the room, this device's included.</summary>
        public static readonly List<NetPlayer> All = new List<NetPlayer>();
        /// <summary>What this device knows about the player (their last presence and snapshot).</summary>
        public readonly PeerInfo peerInfo = new PeerInfo();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();   // Play mode without domain reload

        public override void OnNetworkSpawn()
        {
            peerInfo.id = OwnerClientId.ToString(); peerInfo.isMe = IsOwner;
            if (!All.Contains(this)) All.Add(this);
        }
        public override void OnNetworkDespawn() => All.Remove(this);
        public override void OnDestroy() { All.Remove(this); base.OnDestroy(); }

        /// <summary>Owner: sends this player's presence to everyone else.</summary>
        public void SendPresence(in Presence presence)
        {
            peerInfo.presence = presence;
            PresenceRpc(PresenceCodec.Write(presence));
        }

        /// <summary>Owner, when hosting: sends the match snapshot to everyone else.</summary>
        public void SendSnapshot(byte[] snapshot, int epoch)
        {
            peerInfo.lastSnapshot = snapshot; peerInfo.lastSnapshotEpoch = epoch; peerInfo.lastSnapshotAt = Time.realtimeSinceStartup;
            SnapshotRpc(snapshot);
        }

        [Rpc(SendTo.NotMe)]
        void PresenceRpc(byte[] data, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || data == null || data.Length > MaxPresenceBytes) return;
            if (PresenceCodec.TryRead(data, out var presence)) { peerInfo.presence = presence; peerInfo.presenceAt = Time.realtimeSinceStartup; }
        }

        [Rpc(SendTo.NotMe)]
        void SnapshotRpc(byte[] data, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || data == null || data.Length > MaxSnapshotBytes) return;
            int epoch = Game.ReadEpoch(data);
            if (epoch < 0) return;   // not a snapshot this build can read
            peerInfo.lastSnapshot = data; peerInfo.lastSnapshotEpoch = epoch; peerInfo.lastSnapshotAt = Time.realtimeSinceStartup;
        }
    }
}
