// Assets/Scripts/GameStart.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class GameStart : MonoBehaviour
{
    [Tooltip("선택된 스테이지가 없거나 매핑에 씬 이름이 없으면 이 이름으로 로드")]
    public string fallbackStageSceneName = "Stage";

    public void OnClickStart()
    {
        // 1) 선택된 씬 이름 얻기
        if (StageInfoManager.Instance == null)
        {
            Debug.LogError("[GameStart] StageInfoManager가 없습니다.");
            return;
        }

        string sceneName = StageInfoManager.Instance.GetSelectedSceneNameOr(fallbackStageSceneName);
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameStart] 로드할 씬 이름이 비어있습니다.");
            return;
        }

        // 2) 서버가 일괄 로드하도록 요청
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                // 호스트/전용 서버인 경우: 서버가 직접 브로드캐스트 로드
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                // 클라이언트인 경우: 서버에 로드 요청
                RequestStartServerRpc(sceneName);
            }
        }
        else
        {
            // Netcode 안 쓰는 단일 플레이 테스트용 fallback (선택)
            SceneManager.LoadScene(sceneName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestStartServerRpc(string sceneName)
    {
        // (선택) 여기서 서버가 권한/Ready 여부 검증 후에만 로드
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}