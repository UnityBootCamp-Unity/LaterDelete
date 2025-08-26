// Assets/Scripts/StageInfoManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class StageInfoManager : MonoBehaviour
{
    public static StageInfoManager Instance { get; private set; }

    [Serializable]
    public class StagePrefabMap
    {
        public string stageId;        // 예: "Stage1"
        public GameObject prefab;     // Stage 씬에서 배치할 프리팹(구조물/레벨 프리팹 등)
        public string sceneName;      // 해당 스테이지의 씬 이름(예: "Stage")
    }

    [Header("Stage → Prefab/Scene 매핑")]
    public List<StagePrefabMap> stagePrefabs = new();

    [Header("선택된 스테이지 정보 (런타임)")]
    public string selectedStageId;
    public string selectedSceneName;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetSelectedStage(string stageId)
    {
        selectedStageId = stageId;
        // 매핑에서 씬 이름 자동 추출(설정되어 있으면)
        var map = stagePrefabs.Find(m => m.stageId == stageId);
        selectedSceneName = map != null && !string.IsNullOrEmpty(map.sceneName) ? map.sceneName : selectedSceneName;
    }

    public GameObject GetSelectedStagePrefab()
    {
        if (string.IsNullOrEmpty(selectedStageId)) return null;
        var map = stagePrefabs.Find(m => m.stageId == selectedStageId);
        return map != null ? map.prefab : null;
    }

    public string GetSelectedSceneNameOr(string fallback)
    {
        return string.IsNullOrEmpty(selectedSceneName) ? fallback : selectedSceneName;
    }
}
