using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Game.Shared;

[RequireComponent(typeof(NetworkObject))]
public class StageManager : NetworkBehaviour
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
    [Tooltip("게임 상태 매니저 (인스펙터에서 할당 권장)")]
    [SerializeField] private InGameManager inGameManager;

    [Header("Timing")]
    [Tooltip("씬 로드 직후 몇 프레임 쉬고 구독 시작 (스폰 타이밍 보호)")]
    public int warmupFrames = 2;

    // ---- 내부 상태(서버) ----
    int targetPigCount;
    int deadCount;
    bool ended;
    readonly List<Damageable> subscribed = new();

    void Awake()
    {
        if (clearPanel) clearPanel.SetActive(false);
        if (failPanel) failPanel.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) StartCoroutine(ServerInitRoutine());
    }

    IEnumerator ServerInitRoutine()
    {
        // 0) 씬 초기 스폰/동기화 여유
        for (int i = 0; i < warmupFrames; i++) yield return null;

        // 1) InGameManager.Playing까지 대기
        //    (인스펙터 참조가 비어있을 수 있으므로 안전하게 재시도)
        float wait = 0f;
        while (true)
        {
            if (inGameManager != null &&
                inGameManager.state.Value == InGameManager.State.Playing)
                break;

            // 인스펙터에 없고 아직 못 찾았으면 가볍게 재시도 (선택적 백업)
#if UNITY_2023_1_OR_NEWER
            if (inGameManager == null) inGameManager = Object.FindFirstObjectByType<InGameManager>();
#else
            if (inGameManager == null) inGameManager = Object.FindObjectOfType<InGameManager>();
#endif
            // 너무 오래 못 찾으면 경고 한 번
            wait += Time.unscaledDeltaTime;
            if (wait > 5f && inGameManager == null)
            {
                Debug.LogWarning("[StageManager] InGameManager를 찾지 못했습니다. 대기를 계속합니다.");
                wait = 0f;
            }
            yield return null;
        }

        // 2) 이후는 기존 로직 그대로 --------------------
        if (!ballSpawner) ballSpawner = FindObjectOfType<BallSpawner>();

        string stageId = stageIdOverride;
        if (string.IsNullOrEmpty(stageId))
            stageId = StageInfoManager.Instance ? StageInfoManager.Instance.selectedStageId : null;
        if (string.IsNullOrEmpty(stageId))
            stageId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        targetPigCount = database ? database.GetPigCount(stageId, 0) : 0;

        foreach (var d in FindObjectsOfType<Damageable>())
        {
            if (d.CompareTag("Pig")) Subscribe(d);
        }

        StartCoroutine(ServerJudgeLoop());
    }

    IEnumerator ServerJudgeLoop()
    {
        while (!ended)
        {
            if (deadCount >= targetPigCount)
            {
                ended = true;
                ShowClearClientRpc();
                yield break;
            }

            if (ballSpawner && ballSpawner.RemainingBalls == 0 && deadCount < targetPigCount)
            {
                bool anyBirdAlive = FindObjectsOfType<Bird>().Length > 0; // 필요시 최적화 가능
                if (!anyBirdAlive)
                {
                    ended = true;
                    ShowFailClientRpc();
                    yield break;
                }
            }
            yield return null;
        }
    }

    void Subscribe(Damageable d)
    {
        if (!d || subscribed.Contains(d)) return;
        subscribed.Add(d);
        d.OnDied += OnPigDiedServer;
    }

    void OnPigDiedServer(Damageable _)
    {
        if (ended) return;
        deadCount = Mathf.Clamp(deadCount + 1, 0, int.MaxValue);
        if (deadCount >= targetPigCount)
        {
            ended = true;
            ShowClearClientRpc();
        }
    }

    public override void OnNetworkDespawn()
    {
        foreach (var d in subscribed) if (d) d.OnDied -= OnPigDiedServer;
        subscribed.Clear();
    }

    [ClientRpc] void ShowClearClientRpc() { if (clearPanel) clearPanel.SetActive(true); if (failPanel) failPanel.SetActive(false); }
    [ClientRpc] void ShowFailClientRpc() { if (failPanel) failPanel.SetActive(true); if (clearPanel) clearPanel.SetActive(false); }
}
