using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Test
{
    public class Test_LocalNetworkUI : MonoBehaviour
    {
        [SerializeField] NetworkManager _networkManager;
        [SerializeField] UnityTransport _transport;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 150), GUI.skin.box);
            GUILayout.Label("Local network test");

            if (GUILayout.Button("Start server"))
            {
                StartServer();
            }

            if (GUILayout.Button("Start client"))
            {
                StartClient();
            }

            GUILayout.EndArea();
        }

        void StartServer()
        {
            _transport.SetConnectionData("0.0.0.0", 7777, "0.0.0.0");
            _networkManager.OnClientConnectedCallback += clientId =>
            {
                Debug.Log($"Client {clientId} connected");
            };
            _networkManager.StartServer();
            Debug.Log("[Server] Listening on port 7777");
        }

        void StartClient()
        {
            _transport.SetConnectionData("127.0.0.1", 7777);
            _networkManager.StartClient();
            _networkManager.OnClientConnectedCallback += clientId =>
            {
                Debug.Log($"Client {clientId} joined");
            };
            Debug.Log("[Client] Connecting to 127.0.0.1:7777");
        }
    }
}