using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using System;
using Unity.Multiplayer;
using Game.Client.Network;

#if ENABLE_UCS_SERVER
using Unity.Services.Multiplay;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Shared.Network
{
    public class InGameNetworkBootstrap : MonoBehaviour
    {
        [SerializeField] bool _localTest;
        [SerializeField] NetworkManager _networkManager;
        [SerializeField] UnityTransport _transport;

        IAllocationProvider allocationProvider;

        [SerializeField] string testIp = "20.33.94.7";  // ← Test Allocation의 IP
        [SerializeField] ushort testPort = 9100;       // ← Test Allocation의 Port

        private async void Start()
        {
            await InitializeAsync();
        }

        async Task InitializeAsync()
        {
            if (_localTest == false)
            {
                try
                {
                    await UnityServices.InitializeAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return;
                }
            }

            if (_localTest)
                allocationProvider = new MockAllocationProvider();
            else
                allocationProvider = new MultiplayAllocationProvider();

            MultiplayerRoleFlags roleflags = MultiplayerRolesManager.ActiveMultiplayerRoleMask;

            bool isServer = roleflags.HasFlag(MultiplayerRoleFlags.Server);
            bool isClient = roleflags.HasFlag(MultiplayerRoleFlags.Client);

#if UNITY_SERVER
            Debug.Log(roleflags);

            if (isServer)
            {
                Debug.Log($"[{nameof(InGameNetworkBootstrap)}] Role : Server (Dedicated server)");
                SceneManager.LoadScene("Stage", LoadSceneMode.Additive);
                await StartServerAsync();
            }
#endif

#if UNITY_CLIENT
            if (isClient)
            {
                Debug.Log($"[{nameof(InGameNetworkBootstrap)}] Role : Client");
                //SceneManager.LoadScene("Stage", LoadSceneMode.Additive); // ← 여기만 변경
                await StartClientAsync();
            }
#endif

            if ((isServer == true && isClient == true) || (isServer == false && isClient == false))
            {
#if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
#else
                Application.Quit();
#endif
                return;
            }
        }

#if UNITY_SERVER || ENABLE_UCS_SERVER
        async Task StartServerAsync()
        {
            // 서버 최적화
            Application.targetFrameRate = 30;  // FPS 제한
            QualitySettings.vSyncCount = 0;    // VSync 비활성화

            _transport.SetConnectionData(allocationProvider.ipAddress, allocationProvider.port, allocationProvider.ipAddress);

            bool ok = _networkManager.StartServer();
            if (ok == false)
                throw new Exception("Failed to start server.");

            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            if (_localTest == false)
                await MultiplayService.Instance.ReadyServerForPlayersAsync();

            Debug.Log("Server started");
        }

        void OnClientConnected(ulong clientId)
        {
            Debug.Log($"Client {clientId} connected");
            // 이 로그가 나오는지 확인
        }

        void OnClientDisconnected(ulong clientId)
        {
        }
#endif

#if UNITY_CLIENT
        async Task StartClientAsync()
        {
            if (_localTest)
            {
                // Test Allocation 서버로 바로 붙기
                _transport.SetConnectionData(testIp, testPort);

                bool ok1 = _networkManager.StartClient();
                if (!ok1)
                    throw new Exception("Failed to connect to server.");

                Debug.Log("Client started (direct connect).");
                return; // ← 아래 Allocation 로직 진입 X
            }
            else
            {
                float timeout = 30000f;
                float elapsedTime = 0f;
                bool allocationReady = false;

                while (elapsedTime < timeout)
                {
                    await Task.Delay(1000);

                    if (MultiplayMatchBlackboard.allocation != null &&
                        MultiplayMatchBlackboard.allocation.IsReady)
                    {
                        allocationReady = true;
                        break;
                    }
                }

                if (allocationReady)
                {
                    string TserverIp = MultiplayMatchBlackboard.allocation.IpAddress;
                    ushort TserverPort = (ushort)MultiplayMatchBlackboard.allocation.GamePort;

                    Debug.Log($"=== Connection Attempt ===");
                    Debug.Log($"Server IP: {TserverIp}");
                    Debug.Log($"Server Port: {TserverPort}");
                    Debug.Log($"Allocation ID: {MultiplayMatchBlackboard.allocation.AllocationId}");

                    _transport.SetConnectionData(TserverIp, TserverPort);

                    // 연결 시도 전 잠깐 대기
                    await Task.Delay(3000);

                    bool Tok = _networkManager.StartClient();
                    if (!Tok)
                    {
                        Debug.LogError("NetworkManager.StartClient() returned false");
                        return;
                    }
                    else // 아래 클라이언트가 2번 시작되는 문제 방지
                    {
                        Debug.Log("Client started");
                        return;
                    }
                }

                if (allocationReady == false)
                {
                    Debug.LogError("Timeout waiting for allocation ready");
                    return;
                }

                string serverIp = MultiplayMatchBlackboard.allocation.IpAddress;
                ushort serverPort = (ushort)MultiplayMatchBlackboard.allocation.GamePort;

                Debug.Log($"Connecting to allocated server at {serverIp} : {serverPort}");
                _transport.SetConnectionData(serverIp, serverPort);
            }

            bool ok = _networkManager.StartClient();
            if (ok == false)
                throw new Exception("Failed to connect to server.");

            Debug.Log("Client started");
        }
#endif
    }
}
