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

        [SerializeField] string testIp = "20.33.94.7";
        [SerializeField] ushort testPort = 9100;

        const string StageSceneName = "Stage";

        // ★ 추가: 에디터에서 자동 바인딩 보조
        void OnValidate()
        {
            if (_networkManager == null) _networkManager = FindObjectOfType<NetworkManager>(true);
            if (_transport == null && _networkManager != null) _transport = _networkManager.GetComponent<UnityTransport>();
        }

        // ★ 추가: EnsureSingleNM가 중복 NM을 제거한 뒤 살아있는 싱글턴으로 참조 재설정
        void RebindNetworkRefs()
        {
            var nm = NetworkManager.Singleton ?? _networkManager;
            if (nm != null)
            {
                _networkManager = nm;
                if (_transport == null) _transport = nm.GetComponent<UnityTransport>();
                // 인게임에만 NM이 있으므로 유지하도록 DDOL
                //if (_networkManager != null) DontDestroyOnLoad(_networkManager.gameObject);
            }
        }

        // ★ 추가: 서버가 진짜 Listening 상태인지 잠깐 확인(Spawn/Scene sync 타이밍 안정화)
        static async Task WaitForServerListeningAsync(int timeoutMs = 4000)
        {
            float end = Time.realtimeSinceStartup + timeoutMs / 1000f;
            while (Time.realtimeSinceStartup < end)
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer && nm.IsListening) return;
                await Task.Delay(50);
            }
            Debug.LogWarning("[Bootstrap] Server didn't become Listening within timeout; continuing.");
        }

        private async void Start()
        {
            RebindNetworkRefs();                 // ★ 추가
            await InitializeAsync();
        }

        async Task InitializeAsync()
        {
            RebindNetworkRefs();                 // ★ 추가

            if (!_localTest)
            {
                try { await UnityServices.InitializeAsync(); }
                catch (Exception ex) { Debug.LogException(ex); return; }
            }

            allocationProvider = _localTest ? new MockAllocationProvider()
                                            : new MultiplayAllocationProvider();

            MultiplayerRoleFlags mask = MultiplayerRolesManager.ActiveMultiplayerRoleMask;
            bool isServer = mask.HasFlag(MultiplayerRoleFlags.Server);
            bool isClient = mask.HasFlag(MultiplayerRoleFlags.Client);

#if UNITY_SERVER
            Debug.Log(mask);

            if (isServer)
            {
                Debug.Log($"[{nameof(InGameNetworkBootstrap)}] Role : Server (Dedicated server)");

                await StartServerAsync();        // ★ 서버 먼저
                await WaitForServerListeningAsync(); // ★ 추가: 진짜 리스닝 대기

                HookSceneDebug();
                // ★ 서버가 Netcode SceneManager로 Additive 로드(클라와 동기화)
                _networkManager.SceneManager.LoadScene(StageSceneName, LoadSceneMode.Additive);
            }
#endif

#if UNITY_CLIENT
            if (isClient)
            {
                Debug.Log($"[{nameof(InGameNetworkBootstrap)}] Role : Client");
                HookSceneDebug();
                await StartClientAsync();        // ★ 클라는 씬 직접 로드 안 함
            }
#endif

            if ((isServer && isClient) || (!isServer && !isClient))
            {
#if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
#else
                Application.Quit();
#endif
            }
        }

#if UNITY_SERVER || ENABLE_UCS_SERVER
        async Task StartServerAsync()
        {
            RebindNetworkRefs(); // ★ 추가: 혹시 모를 참조 깨짐 방지
            if (_networkManager == null || _transport == null)
            {
                Debug.LogError("[Bootstrap] NetworkManager/UnityTransport reference is null.");
                return;
            }

            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;

            _transport.SetConnectionData(allocationProvider.ipAddress, allocationProvider.port, allocationProvider.ipAddress);

            Debug.Log($"[Bootstrap] Before StartServer - IsListening={_networkManager.IsListening}");

            if (!_networkManager.StartServer())
                throw new Exception("Failed to start server.");

            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            if (!_localTest)
            {
                var ready = await WaitForAllocationReadyAsync(15000);
                if (ready)
                {
                    try
                    {
                        await Unity.Services.Multiplay.MultiplayService.Instance.ReadyServerForPlayersAsync();
                        Debug.Log("[Bootstrap] ReadyServerForPlayersAsync OK (allocation present)");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[Bootstrap] ReadyServerForPlayersAsync skipped: " + ex.GetType().Name);
                    }
                }
                else
                {
                    Debug.LogWarning("[Bootstrap] No allocation yet → skip ReadyServerForPlayersAsync");
                }
            }
        }

        static async Task<bool> WaitForAllocationReadyAsync(int timeoutMs)
        {
            var deadline = Time.realtimeSinceStartup + (timeoutMs / 1000f);
            while (Time.realtimeSinceStartup < deadline)
            {
                try
                {
                    var cfg = Unity.Services.Multiplay.MultiplayService.Instance.ServerConfig;
                    if (!string.IsNullOrEmpty(cfg.AllocationId))
                        return true;
                }
                catch { }
                await System.Threading.Tasks.Task.Delay(500);
            }
            return false;
        }

        void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[Bootstrap] Client {clientId} connected");
        }

        void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[Bootstrap] Client {clientId} disconnected");
        }
#endif

#if UNITY_CLIENT
        async Task StartClientAsync()
        {
            RebindNetworkRefs(); // ★ 추가

            if (_localTest)
            {
                _transport.SetConnectionData(testIp, testPort);
                if (!_networkManager.StartClient())
                    throw new Exception("Failed to connect to server.");

                Debug.Log("[Bootstrap] Client started (direct connect)");
                return;
            }

            const int timeoutMs = 30000;
            int waited = 0;
            while (waited < timeoutMs)
            {
                await Task.Delay(1000);
                waited += 1000;

                if (MultiplayMatchBlackboard.allocation != null &&
                    MultiplayMatchBlackboard.allocation.IsReady)
                    break;
            }

            if (MultiplayMatchBlackboard.allocation == null ||
                !MultiplayMatchBlackboard.allocation.IsReady)
            {
                Debug.LogError("[Bootstrap] Timeout waiting for allocation ready");
                return;
            }

            string ip = MultiplayMatchBlackboard.allocation.IpAddress;
            ushort port = (ushort)MultiplayMatchBlackboard.allocation.GamePort;

            Debug.Log("=== Connection Attempt ===");
            Debug.Log($"Server IP: {ip}");
            Debug.Log($"Server Port: {port}");
            Debug.Log($"Allocation ID: {MultiplayMatchBlackboard.allocation.AllocationId}");

            _transport.SetConnectionData(ip, port);

            await Task.Delay(1000);

            if (!_networkManager.StartClient())
            {
                Debug.LogError("NetworkManager.StartClient() returned false");
                return;
            }

            Debug.Log("[Bootstrap] Client started");
        }
#endif

        // ── 디버깅 ─────────────────────────────
        void HookSceneDebug()
        {
            if (_networkManager == null || _networkManager.SceneManager == null) return;
            var nsm = _networkManager.SceneManager;

            nsm.OnSceneEvent += (e) =>
            {
                Debug.Log($"[Bootstrap] SceneEvent={e.SceneEventType}, Name={e.SceneName}, IsServer={_networkManager.IsServer}, IsListening={_networkManager.IsListening}");
            };

            nsm.OnLoadEventCompleted += (sceneName, mode, clientsCompleted, clientsTimedOut) =>
            {
                Debug.Log($"[Bootstrap] LoadCompleted: {sceneName}, ok={clientsCompleted.Count}, timeout={clientsTimedOut.Count}");
            };
        }

        // ★ 추가: 정리
        void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback -= OnClientConnected;
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
    }
}
