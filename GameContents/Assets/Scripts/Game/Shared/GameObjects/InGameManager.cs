using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Game.Shared
{
    public class InGameManager : NetworkBehaviour
    {
        public enum State { WaitingForPlayers, CountdownToStart, Playing, Finished }
        public enum Role { Lobby, InGame }   // 추가: 이 프리팹이 어느 씬용인지

        [Header("Mode")]
        [SerializeField] private Role role = Role.Lobby; // 로비/인게임 프리팹에서 인스펙터로 구분

        [Header("State")]
        public NetworkVariable<State> state = new(
            State.WaitingForPlayers,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 서버가 정한 "시작 예정 시각"(ServerTime 기준, 로비에서만 사용)
        public NetworkVariable<double> startTime = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ===== 로비에서만 쓰는 필드들 =====
        private readonly HashSet<ulong> _connectedClients = new();
        private readonly HashSet<ulong> _readyClients = new();

        [Header("Lobby Rules (Lobby 역할일 때만 사용)")]
        public int requiredPlayers = 4;
        public float countdownSeconds = 3f;

        void Awake()
        {
            // (원하면 로비용만 유지 / 인게임도 유지할 수 있음)
            // DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log($"[{nameof(InGameManager)}] spawned as {role}");

            if (!IsServer) return;

            if (role == Role.InGame)
            {
                // 인게임 프리팹: 곧바로 Playing으로 강제 세팅
                state.Value = State.Playing;
                startTime.Value = 0;
                // 로비용 콜백/로직 미사용
                return;
            }

            // ===== 여기부터는 Lobby 역할일 때만 =====
            // 기존 접속자 반영
            foreach (var kv in NetworkManager.ConnectedClientsList)
                _connectedClients.Add(kv.ClientId);

            NetworkManager.OnClientConnectedCallback += Server_OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += Server_OnClientDisconnected;

            state.Value = State.WaitingForPlayers;
            startTime.Value = 0;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null && role == Role.Lobby)
            {
                NetworkManager.OnClientConnectedCallback -= Server_OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= Server_OnClientDisconnected;
            }
        }

        // ===== (Lobby 전용) 접속/이탈 =====
        private void Server_OnClientConnected(ulong clientId)
        {
            if (role != Role.Lobby) return;
            _connectedClients.Add(clientId);
            _readyClients.Remove(clientId);
            TryStartCountdown();
        }

        private void Server_OnClientDisconnected(ulong clientId)
        {
            if (role != Role.Lobby) return;
            _connectedClients.Remove(clientId);
            _readyClients.Remove(clientId);

            if (state.Value == State.CountdownToStart && _readyClients.Count < requiredPlayers)
            {
                state.Value = State.WaitingForPlayers;
                startTime.Value = 0;
            }
        }

        // ===== (Lobby 전용) Ready 토글 =====
        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool isReady, ServerRpcParams rpcParams = default)
        {
            if (role != Role.Lobby) return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!_connectedClients.Contains(clientId)) return;

            if (isReady) _readyClients.Add(clientId);
            else _readyClients.Remove(clientId);

            TryStartCountdown();
        }

        private void TryStartCountdown()
        {
            if (!IsServer || role != Role.Lobby) return;

            if (state.Value != State.WaitingForPlayers && state.Value != State.CountdownToStart)
                return;

            if (_connectedClients.Count >= requiredPlayers && _readyClients.Count >= requiredPlayers)
            {
                if (state.Value != State.CountdownToStart)
                {
                    state.Value = State.CountdownToStart;
                    startTime.Value = NetworkManager.ServerTime.Time + countdownSeconds;
                }
            }
            else
            {
                if (state.Value == State.CountdownToStart)
                {
                    state.Value = State.WaitingForPlayers;
                    startTime.Value = 0;
                }
            }
        }

        void Update()
        {
            if (!IsServer) return;

            // 로비에서만 카운트다운→Playing 전환
            if (role == Role.Lobby &&
                state.Value == State.CountdownToStart &&
                startTime.Value > 0 &&
                NetworkManager.ServerTime.Time >= startTime.Value)
            {
                state.Value = State.Playing;
                // (선택) 여기서 서버가 Netcode SceneManager로 Stage 씬 일괄 로드 가능
                // NetworkManager.SceneManager.LoadScene("Stage", LoadSceneMode.Single);
            }
        }

        // ===== 클라 유틸 (로비에서만 사용) =====
        public void SetReadyFromClient(bool isReady)
        {
            if (IsClient && role == Role.Lobby)
                SetReadyServerRpc(isReady);
        }
    }
}
