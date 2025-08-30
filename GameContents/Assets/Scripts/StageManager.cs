// Assets/Scripts/StageManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject clearPanel;
    public GameObject failPanel;

    [Header("Data")]
    public StageDatabase database;
    [Tooltip("선택된 스테이지 ID가 비면 StageInfoManager 또는 씬 이름 사용")]
    public string stageIdOverride;

    [Header("Refs")]
    public BallSpawner ballSpawner; // 없으면 자동 탐색

    [Header("Timing")]
    [Tooltip("씬 로드 직후 몇 프레임 쉬고 구독 시작 (스폰 타이밍 보호)")]
    public int warmupFrames = 2;

    int targetPigCount;   // SO에서 읽은 '죽여야 하는 총 수'
    int deadCount;        // 지금까지 죽은 수
    bool ready;           // 판정 시작 플래그
    readonly List<Damageable> subscribed = new();

    void Awake()
    {
        if (clearPanel) clearPanel.SetActive(false);
        if (failPanel) failPanel.SetActive(false);
    }

    IEnumerator Start()
    {
        // 스포너/오브젝트 배치가 끝나도록 잠깐 대기
        for (int i = 0; i < warmupFrames; i++) yield return null;

        if (!ballSpawner) ballSpawner = FindObjectOfType<BallSpawner>();

        // 1) 사용할 stageId 결정
        string stageId = stageIdOverride;
        if (string.IsNullOrEmpty(stageId))
            stageId = StageInfoManager.Instance ? StageInfoManager.Instance.selectedStageId : null;
        if (string.IsNullOrEmpty(stageId))
            stageId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // 2) SO에서 목표 개수 읽기 (없으면 0으로)
        targetPigCount = database ? database.GetPigCount(stageId, 0) : 0;

        // 3) 현재 씬의 Pig들 구독 (언제 스폰됐든 '죽을 때'만 필요)
        foreach (var d in FindObjectsOfType<Damageable>())
        {
            // Pig만 집계하고 싶으면 태그 사용 권장
            if (!d.CompareTag("Pig")) continue;
            Subscribe(d);
        }

        ready = true;
    }

    void OnDestroy()
    {
        foreach (var d in subscribed)
            if (d) d.OnDied -= OnPigDied;
        subscribed.Clear();
    }

    void Update()
    {
        if (!ready) return;

        // 클리어: 죽은 수가 목표에 도달
        if (deadCount >= targetPigCount)
        {
            ShowClear();
            return;
        }

        // 실패: 남은 공 0, 새 없음, 아직 못 죽인 피그 있음
        if (ballSpawner && ballSpawner.RemainingBalls == 0 && Bird.AliveCount == 0 && deadCount < targetPigCount)
        {
            ShowFail();
        }
    }

    void Subscribe(Damageable d)
    {
        if (!d || subscribed.Contains(d)) return;
        subscribed.Add(d);
        d.OnDied += OnPigDied;
    }

    void OnPigDied(Damageable _)
    {
        deadCount = Mathf.Clamp(deadCount + 1, 0, int.MaxValue);
        // 즉시 판정 갱신
        if (ready && deadCount >= targetPigCount) ShowClear();
    }

    void ShowClear()
    {
        if (clearPanel) clearPanel.SetActive(true);
        if (failPanel) failPanel.SetActive(false);
        enabled = false;
        Debug.Log("STAGE CLEAR");
    }

    void ShowFail()
    {
        if (failPanel) failPanel.SetActive(true);
        if (clearPanel) clearPanel.SetActive(false);
        enabled = false;
        Debug.Log("STAGE FAIL");
    }
}
