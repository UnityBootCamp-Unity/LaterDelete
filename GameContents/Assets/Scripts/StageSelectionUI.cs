// Assets/Scripts/StageSelectionUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageSelectionUI : MonoBehaviour
{
    [Header("Data")]
    public StageDatabase database;

    [Header("UI Refs")]
    public TMP_Text stageNameText;
    public TMP_Text ballCountText;
    public TMP_Text pigCountText;
    public Image previewImage;

    [Header("Formats")]
    public string stageNameFormat = "{0}";      // e.g., "Stage1"
    public string ballCountFormat = "공 개수 : {0}";
    public string pigCountFormat = "큐브 개수 : {0}";

    [Header("Fallback")]
    public Sprite fallbackPreview;

    private string currentStageId;

    /// <summary>StageButton에서 호출</summary>
    public void SelectStage(string stageId)
    {
        currentStageId = stageId;

        if (database == null)
        {
            Debug.LogError("[StageSelectionUI] database not assigned.");
            return;
        }

        var entry = database.GetOrDefault(stageId);
        // 이름
        if (stageNameText) stageNameText.text = string.Format(stageNameFormat, stageId);

        // 개수
        if (ballCountText) ballCountText.text = string.Format(ballCountFormat, entry.ballCount);
        if (pigCountText) pigCountText.text = string.Format(pigCountFormat, entry.pigCount);

        // 프리뷰 로드 (Resources)
        Sprite spr = null;
        var path = database.GetPreviewPath(stageId);
        if (!string.IsNullOrEmpty(path))
            spr = Resources.Load<Sprite>(path);

        if (previewImage)
            previewImage.sprite = spr ? spr : fallbackPreview;
    }

    // 필요하면 Start에서 기본 선택값 지정 가능
    void Start()
    {
        // 예: 첫 버튼(stageId가 "Stage1")을 기본 선택하려면:
        // SelectStage("Stage1");
    }

    public string GetSelectedStageId() => currentStageId;
}
