// Assets/Scripts/GameStart.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStart : MonoBehaviour
{
    [Tooltip("선택된 스테이지가 없거나 매핑에 씬 이름이 없으면 이 이름으로 로드")]
    public string fallbackStageSceneName = "Stage";

    /// <summary>Start 버튼에 연결하세요.</summary>
    public void OnClickStart()
    {
        if (StageInfoManager.Instance == null)
        {
            Debug.LogError("[GameStart] StageInfoManager가 없습니다.");
            return;
        }

        // 선택된 씬 이름 얻기
        string sceneName = StageInfoManager.Instance.GetSelectedSceneNameOr(fallbackStageSceneName);

        if (string.IsNullOrEmpty(StageInfoManager.Instance.selectedStageId))
        {
            Debug.LogWarning("[GameStart] 아직 스테이지가 선택되지 않았습니다. 기본 씬으로 이동합니다.");
        }

        SceneManager.LoadScene(sceneName);
    }
}
