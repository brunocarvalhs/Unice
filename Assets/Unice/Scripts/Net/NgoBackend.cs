using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Unice
{
    /// <summary>
    /// Online rooms through Unity Multiplayer Services sessions (join by code) with distributed authority,
    /// so the room survives whoever created it leaving. The game picks its own host on top (see App.ElectHost).
    /// </summary>
    public sealed class NgoBackend : INetBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => NetRegistry.Factory = () => new NgoBackend();

        ISession session;
        bool ready;
        public event Action<string> Lost;

        static NetworkManager NM => NetworkManager.Singleton;
        public bool InRoom => ready && session != null && NM != null && NM.IsConnectedClient;
        public string Code => session?.Code ?? "";
        public string MyId => NM != null ? NM.LocalClientId.ToString() : "";

        public IReadOnlyList<PeerInfo> Peers
        {
            get
            {
                var list = new List<PeerInfo>(NetPlayer.All.Count);
                foreach (var p in NetPlayer.All) if (p != null && p.IsSpawned) list.Add(p.info);
                return list;
            }
        }

        static NetPlayer Local() { foreach (var p in NetPlayer.All) if (p != null && p.IsSpawned && p.IsOwner) return p; return null; }

        public async Task InitAsync()
        {
            if (NM == null)
            {
                var player = Resources.Load<GameObject>("UniceNet/NetPlayer");
                if (player == null) throw new NetException("Falta o objeto de rede do jogador. No Unity, use o menu Unice > Configurar projeto.");
                var go = new GameObject("NetworkManager"); go.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(go);
                var tr = go.AddComponent<UnityTransport>();
                var nm = go.AddComponent<NetworkManager>();
                nm.NetworkConfig = new NetworkConfig { NetworkTransport = tr, PlayerPrefab = player, NetworkTopology = NetworkTopologyTypes.DistributedAuthority };
                go.SetActive(true);
                nm.OnClientStopped += _ => { if (!ready) return; ready = false; Lost?.Invoke("A conexão com a sala caiu."); };   // keep session: LeaveAsync still has to leave it
            }
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (Exception x)
            {
                Metrics.Error(x, "net");
                if (x.ToString().Contains("UnityProjectNotLinkedException"))
                    throw new NetException("O projeto ainda não está ligado ao Unity Cloud. No Unity: Edit > Project Settings > Services, escolha a organização e ligue um projeto.");
                throw new NetException("Não consegui falar com os serviços online. Confira a internet e tente de novo.");
            }
        }

        public async Task<string> CreateAsync()
        {
            NM.NetworkConfig.NetworkTopology = NetworkTopologyTypes.DistributedAuthority;
            try { session = await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = Rules.MaxPlayers, IsPrivate = true }.WithDistributedAuthorityNetwork()); }
            catch (Exception x) { Metrics.Error(x, "net"); throw new NetException("Não deu para criar a sala agora. Tente de novo em instantes."); }
            await Connected();
            return session.Code;
        }

        public async Task JoinAsync(string code)
        {
            NM.NetworkConfig.NetworkTopology = NetworkTopologyTypes.DistributedAuthority;
            try { session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code); }
            catch (Exception x) { Metrics.Error(x, "net"); throw new NetException("Sala não encontrada, cheia ou já encerrada. Confira o código."); }
            await Connected();
        }

        /// <summary>Waits until this device is connected and its player object exists, so it has an id.</summary>
        async Task Connected()
        {
            session.RemovedFromSession += OnRemoved;
            session.Deleted += OnRemoved;
            float t0 = Time.realtimeSinceStartup;
            while (!(NM.IsConnectedClient && Local() != null))
            {
                if (Time.realtimeSinceStartup - t0 > 15) { await LeaveAsync(); throw new NetException("A sala demorou demais para responder. Tente de novo."); }
                await Task.Delay(50);
            }
            ready = true;
        }

        void OnRemoved()
        {
            if (!ready) return;
            ready = false;
            Lost?.Invoke("Você saiu da sala: ela foi encerrada.");
        }

        public async Task LeaveAsync()
        {
            ready = false;
            var s = session; session = null;
            if (s != null)
            {
                s.RemovedFromSession -= OnRemoved; s.Deleted -= OnRemoved;
                try { await s.LeaveAsync(); } catch (Exception x) { Metrics.Error(x, "net"); }
            }
            if (NM != null && NM.IsListening) NM.Shutdown();
        }

        public void SetPresence(Presence p) { var me = Local(); if (me != null) me.SendPresence(p); }
        public void BroadcastSnapshot(byte[] data, int ep) { var me = Local(); if (me != null) me.SendSnapshot(data, ep); }
    }
}
