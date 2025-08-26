// Assets/Scripts/StageButton.cs
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class StageButton : MonoBehaviour
{
    [Tooltip("이 버튼이 대표하는 스테이지 ID (예: Stage1)")]
    public string stageId;

    public StageSelectionUI selectionUI;

    void Awake()
    {
        if (selectionUI == null) selectionUI = FindObjectOfType<StageSelectionUI>();
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (!string.IsNullOrEmpty(stageId))
        {
            StageInfoManager.Instance?.SetSelectedStage(stageId);
            if (selectionUI != null)
                selectionUI.SelectStage(stageId);
        }
    }
}
