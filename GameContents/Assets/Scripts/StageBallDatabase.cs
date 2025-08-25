// Assets/Scripts/StageBallDatabase.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "AngryVR/Stage Ball Database", fileName = "StageBallDatabase")]
public class StageBallDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string stageId; // 씬 이름 또는 임의의 ID
        public int ballCount = 3;
    }

    public List<Entry> stages = new();

    public int GetBallCount(string id, int defaultValue = 3)
    {
        foreach (var e in stages)
            if (e.stageId == id) return e.ballCount;
        return defaultValue;
    }
}
