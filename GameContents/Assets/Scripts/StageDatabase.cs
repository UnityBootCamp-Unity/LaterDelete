// Assets/Scripts/StageDatabase.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "AngryVR/Stage Database", fileName = "StageDatabase")]
public class StageDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("스테이지 ID (씬 이름 등)")]
        public string stageId;

        [Tooltip("해당 스테이지의 씬 이름 (로딩에 쓰고 싶다면)")]
        public string sceneName;

        [Tooltip("시작 시 지급되는 공 개수")]
        public int ballCount = 3;

        [Tooltip("스테이지에 배치된 Pig(큐브) 개수 (대기실 UI 표시에 사용)")]
        public int pigCount = 5;

        [Tooltip("Resources에서 불러올 프리뷰 이미지 경로 (예: \"StagePreviews/Stage1\"). 비우면 stageId를 경로명으로 사용")]
        public string previewResourcePath;
    }

    public List<Entry> stages = new();

    public bool TryGet(string id, out Entry entry)
    {
        foreach (var e in stages)
        {
            if (e.stageId == id)
            {
                entry = e;
                return true;
            }
        }
        entry = null;
        return false;
    }

    public Entry GetOrDefault(string id, int defaultBalls = 3, int defaultPigs = 0)
    {
        if (TryGet(id, out var e)) return e;
        return new Entry { stageId = id, ballCount = defaultBalls, pigCount = defaultPigs, previewResourcePath = id };
    }

    public int GetBallCount(string id, int defaultValue = 3)
    {
        return GetOrDefault(id, defaultValue).ballCount;
    }

    public int GetPigCount(string id, int defaultValue = 0)
    {
        return GetOrDefault(id, defaultValue).pigCount;
    }

    public string GetPreviewPath(string id)
    {
        var e = GetOrDefault(id);
        return string.IsNullOrEmpty(e.previewResourcePath) ? e.stageId : e.previewResourcePath;
    }

    public string GetSceneName(string id) => GetOrDefault(id).sceneName;
}
