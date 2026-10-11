using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// Online rooms through Unity Multiplayer Services sessions (join by code) with distributed authority,
    /// so the room survives whoever created it leaving. The game picks its own host on top (see RoomSync.ElectHost).
    /// </summary>
    public sealed class NgoBackend : INetBackend
    {
        /// <summary>Waiting for the connection after creating/joining gives up after this long.</summary>
        const float ConnectTimeoutSeconds = 15;
        /// <summary>Session property holding the creator's <see cref="Protocol.Id"/>, checked by whoever joins.</summary>
        const string ProtocolKey = "protocol";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => NetRegistry.Factory = () => new NgoBackend();

        ISession session;
        /// <summary>Connected and the player object exists; false while connecting or after leaving.</summary>
        bool ready;
        public event Action<string> Lost;

        static NetworkManager Network => NetworkManager.Singleton;
        public bool InRoom => ready && session != null && Network != null && Network.IsConnectedClient;
        public string Code => session?.Code ?? "";
        public string LocalId => Network != null ? Network.LocalClientId.ToString() : "";

        public IReadOnlyList<PeerInfo> Peers
        {
            get
            {
                var peers = new List<PeerInfo>(NetPlayer.All.Count);
                foreach (var player in NetPlayer.All) if (player != null && player.IsSpawned) peers.Add(player.peerInfo);
                return peers;
            }
        }

        /// <summary>This device's own player object, once spawned.</summary>
        static NetPlayer Local() { foreach (var player in NetPlayer.All) if (player != null && player.IsSpawned && player.IsOwner) return player; return null; }

        /// <summary>Creates the NetworkManager (once) and signs in to Unity Services (<see cref="PlayerSignIn"/>).</summary>
        public async Task InitAsync()
        {
            if (Network == null)
            {
                var playerPrefab = Resources.Load<GameObject>("UniceNet/NetPlayer");
                if (playerPrefab == null) throw new NetException("Falta o objeto de rede do jogador. No Unity, use o menu Unice > Configurar projeto.");
                var managerObject = new GameObject("NetworkManager"); managerObject.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(managerObject);
                var transport = managerObject.AddComponent<UnityTransport>();
                var manager = managerObject.AddComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = playerPrefab, NetworkTopology = NetworkTopologyTypes.DistributedAuthority };
                managerObject.SetActive(true);
                manager.OnClientStopped += _ => { if (!ready) return; ready = false; Lost?.Invoke("A conexão com a sala caiu."); };   // keep session: LeaveAsync still has to leave it
            }
            try
            {
                await PlayerSignIn.EnsureSignedInAsync();
            }
            catch (Exception error)
            {
                Metrics.Error(error, "net");
                if (error.ToString().Contains("UnityProjectNotLinkedException"))
                    throw new NetException("O projeto ainda não está ligado ao Unity Cloud. No Unity: Edit > Project Settings > Services, escolha a organização e ligue um projeto.");
                throw new NetException("Não consegui falar com os serviços online. Confira a internet e tente de novo.");
            }
        }

        public async Task<string> CreateAsync()
        {
            Network.NetworkConfig.NetworkTopology = NetworkTopologyTypes.DistributedAuthority;
            try { session = await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions
            {
                MaxPlayers = Rules.MaxPlayers, IsPrivate = true,
                SessionProperties = { [ProtocolKey] = new SessionProperty(Protocol.Id.ToString(), VisibilityPropertyOptions.Member) },
            }.WithDistributedAuthorityNetwork()); }
            catch (Exception error) { Metrics.Error(error, "net"); throw new NetException("Não deu para criar a sala agora. Tente de novo em instantes."); }
            await Connected();
            return session.Code;
        }

        public async Task JoinAsync(string roomCode)
        {
            Network.NetworkConfig.NetworkTopology = NetworkTopologyTypes.DistributedAuthority;
            try { session = await MultiplayerService.Instance.JoinSessionByCodeAsync(roomCode); }
            catch (Exception error) { Metrics.Error(error, "net"); throw new NetException("Sala não encontrada, cheia ou já encerrada. Confira o código."); }
            // a room from another app version (or from a build before this check) can't be played together
            if (!session.Properties.TryGetValue(ProtocolKey, out var protocol) || protocol.Value != Protocol.Id.ToString())
            {
                await LeaveAsync();
                throw new NetException(Localization.Get("net.update_app"));
            }
            await Connected();
        }

        /// <summary>Waits until this device is connected and its player object exists, so it has an id.</summary>
        async Task Connected()
        {
            session.RemovedFromSession += OnRemoved;
            session.Deleted += OnRemoved;
            float startedAt = Time.realtimeSinceStartup;
            while (!(Network.IsConnectedClient && Local() != null))
            {
                if (Time.realtimeSinceStartup - startedAt > ConnectTimeoutSeconds) { await LeaveAsync(); throw new NetException("A sala demorou demais para responder. Tente de novo."); }
                await Task.Delay(50);
            }
            ready = true;
        }

        /// <summary>The session ended or kicked this device out.</summary>
        void OnRemoved()
        {
            if (!ready) return;
            ready = false;
            Lost?.Invoke("Você saiu da sala: ela foi encerrada.");
        }

        public async Task LeaveAsync()
        {
            ready = false;
            var leaving = session; session = null;
            if (leaving != null)
            {
                leaving.RemovedFromSession -= OnRemoved; leaving.Deleted -= OnRemoved;
                try { await leaving.LeaveAsync(); } catch (Exception error) { Metrics.Error(error, "net"); }
            }
            if (Network != null && Network.IsListening) Network.Shutdown();
        }

        public void SetPresence(Presence presence) { var me = Local(); if (me != null) me.SendPresence(presence); }
        public void BroadcastSnapshot(byte[] snapshot, int epoch) { var me = Local(); if (me != null) me.SendSnapshot(snapshot, epoch); }
    }
}
