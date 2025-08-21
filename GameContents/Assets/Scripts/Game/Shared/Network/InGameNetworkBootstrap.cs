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

            Debug.Log(roleflags);
            if (isServer)
            {
                Debug.Log($"[{nameof(InGameNetworkBootstrap)}] Role : Server (Dedicated server)");
                SceneManager.LoadScene("Server", LoadSceneMode.Additive);
                await StartServerAsync();
            }

            if (isClient)
            {
                Debug.Log($"[{nameof(InGameNetworkBootstrap)}] Role : Client");
                SceneManager.LoadScene("Client", LoadSceneMode.Additive);
                await StartClientAsync();
            }

            if ((isServer == true && isClient == true) ||
                (isServer == false && isClient == false))
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
            //LogServerConfig();

            _transport.SetConnectionData(allocationProvider.ipAddress,
                                         allocationProvider.port,
                                         allocationProvider.ipAddress);
            bool ok = _networkManager.StartServer();

            if (ok == false)
                throw new Exception("Failed to start server.");

            _networkManager.OnClientConnectedCallback += OnClientConnected;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            if (_localTest == false)
                await MultiplayService.Instance.ReadyServerForPlayersAsync();

            Debug.Log("Server started");
        }

        //void LogServerConfig()
        //{
        //    var serverConfig = MultiplayService.Instance.ServerConfig;
        //    Debug.Log($"Server ID[{serverConfig.ServerId}]");
        //    Debug.Log($"AllocationID[{serverConfig.AllocationId}]");
        //    Debug.Log($"Port[{serverConfig.Port}]");
        //    Debug.Log($"QueryPort[{serverConfig.QueryPort}");
        //    Debug.Log($"LogDirectory[{serverConfig.ServerLogDirectory}]");
        //}

        void OnClientConnected(ulong clientId)
        {
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
                // Nothing to do.. 
                _transport.SetConnectionData("127.0.0.1", 7777);
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