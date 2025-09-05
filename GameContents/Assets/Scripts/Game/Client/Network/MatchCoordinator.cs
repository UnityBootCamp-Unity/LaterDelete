using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Network
{
    public class MatchCoordinator : NetworkBehaviour
    {
        public static MatchCoordinator Instance { get; private set; }
        void Awake() => Instance = this;

        [Rpc(SendTo.Server)]
        public void RequestStartStageServerRpc(string stageSceneName)
        {
            if (!IsServer) return;
            // ★ 이 한 줄이 모든 클라를 Stage로 이동시킴
            NetworkManager.SceneManager.LoadScene(stageSceneName, LoadSceneMode.Single);
        }
    }
}