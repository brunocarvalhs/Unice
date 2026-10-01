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
        public static readonly List<NetPlayer> All = new List<NetPlayer>();
        public readonly PeerInfo info = new PeerInfo();

        public override void OnNetworkSpawn() { info.id = OwnerClientId.ToString(); info.isMe = IsOwner; if (!All.Contains(this)) All.Add(this); }
        public override void OnNetworkDespawn() => All.Remove(this);
        public override void OnDestroy() { All.Remove(this); base.OnDestroy(); }

        public void SendPresence(in Presence p) { info.pres = p; PresRpc(Wire.Write(p)); }

        public void SendSnapshot(byte[] data, int ep)
        {
            info.lastSnap = data; info.lastSnapEp = ep; info.lastSnapAt = Time.realtimeSinceStartup;
            SnapRpc(data);
        }

        [Rpc(SendTo.NotMe)]
        void PresRpc(byte[] data, RpcParams rp = default)
        {
            if (rp.Receive.SenderClientId != OwnerClientId || data == null || data.Length > 512) return;
            if (Wire.TryRead(data, out var p)) { info.pres = p; info.presAt = Time.realtimeSinceStartup; }
        }

        [Rpc(SendTo.NotMe)]
        void SnapRpc(byte[] data, RpcParams rp = default)
        {
            if (rp.Receive.SenderClientId != OwnerClientId || data == null || data.Length > 8192) return;
            int ep = Game.ReadEpoch(data); if (ep < 0) return;
            info.lastSnap = data; info.lastSnapEp = ep; info.lastSnapAt = Time.realtimeSinceStartup;
        }
    }
}
