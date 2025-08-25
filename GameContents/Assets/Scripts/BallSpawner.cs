// Assets/Scripts/BallSpawner.cs
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BallSpawner : MonoBehaviour
{
    [Header("Config")]
    public StageBallDatabase database;
    [Tooltip("현재 스테이지 ID (비워두면 씬 이름 사용)")]
    public string stageIdOverride;

    [Header("Refs")]
    public Transform playerRig;   // 선택: 회전 기준 등 필요 시
    public Transform birdPoint;   // 여기 위치/회전으로 소환
    public Bird birdPrefab;       // 구(새) 프리팹

    [Header("(선택) 대기열 배치")]
    [Tooltip("여러 개 추가할 때 BirdPoint의 오른쪽으로 줄 세우기")]
    public bool queueAlongRight = false;
    public float spacing = 0.35f; // queueAlongRight가 true일 때만 사용

    int maxBalls;
    int usedBalls;

    public event Action<int, int> OnCountChanged;

    public int MaxBalls => maxBalls;
    public int RemainingBalls => Mathf.Max(0, maxBalls - usedBalls);

    void Start()
    {
        var id = string.IsNullOrEmpty(stageIdOverride)
            ? SceneManager.GetActiveScene().name
            : stageIdOverride;

        maxBalls = database ? database.GetBallCount(id, 3) : 3;
        usedBalls = 0;

        RaiseCountChanged();
    }

    public void OnAddButton()
    {
        if (usedBalls >= maxBalls)
        {
            Debug.Log("No balls left for this stage.");
            RaiseCountChanged(); // 버튼 비활성화 UI 등을 위해 한번 더 알림
            return;
        }

        if (birdPoint == null)
        {
            Debug.LogError("[BallSpawner] birdPoint not assigned.");
            return;
        }

        Vector3 spawnPos = birdPoint.position;
        Quaternion spawnRot = birdPoint.rotation;

        if (queueAlongRight)
            spawnPos += birdPoint.right * spacing * usedBalls;

        Instantiate(birdPrefab, spawnPos, spawnRot);
        usedBalls++;

        RaiseCountChanged();
    }

    public void ResetUsage()
    {
        usedBalls = 0;
        RaiseCountChanged();
    }

    void RaiseCountChanged() => OnCountChanged?.Invoke(RemainingBalls, maxBalls);
}