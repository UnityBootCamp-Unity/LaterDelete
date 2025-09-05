// Assets/Scripts/StageManager.cs
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class StageManager : NetworkBehaviour
{
    [Header("UI Panels")]
    public GameObject clearPanel;
    public GameObject failPanel;

    [Header("Data")]
    public StageDatabase database;
    public string stageIdOverride;

    [Header("Refs")]
    public BallSpawner ballSpawner;

    [Header("Stage Spawn")]
    public Transform stageSpawnPoint;
    public string stageSpawnPointName = "ObjectSpawnPoint";
    public GameObject fallbackStagePrefab;

    [Header("Timing")]
    public int warmupFrames = 2;

    // 내부 상태
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
        for (int i = 0; i < warmupFrames; i++) yield return null;

        // 1) 스테이지 ID 결정
        string stageId =
            !string.IsNullOrEmpty(stageIdOverride) ? stageIdOverride :
            StageInfoManager.Instance ? StageInfoManager.Instance.selectedStageId :
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // 2) 스테이지 프리팹 스폰
        SpawnStagePrefabServer();

        // 3) DB에서 목표 Pig 개수
        targetPigCount = database ? database.GetPigCount(stageId, 0) : 0;

        // 4) 현재 씬의 Pig 구독
        foreach (var d in FindObjectsOfType<Damageable>())
            if (d.CompareTag("Pig")) Subscribe(d);

        // 5) BallSpawner 참조 보정
        if (!ballSpawner) ballSpawner = FindObjectOfType<BallSpawner>();

        // 6) 판정 루프 시작
        StartCoroutine(ServerJudgeLoop());
    }

    void SpawnStagePrefabServer()
    {
        if (!IsServer) return;

        if (!stageSpawnPoint)
        {
            var go = GameObject.Find(stageSpawnPointName);
            if (go) stageSpawnPoint = go.transform;
        }
        if (!stageSpawnPoint) { Debug.LogWarning("[StageManager] Stage spawn point 없음"); return; }

        GameObject prefab =
            StageInfoManager.Instance ? StageInfoManager.Instance.GetSelectedStagePrefab() : null;
        if (!prefab) prefab = fallbackStagePrefab;
        if (!prefab) { Debug.LogWarning("[StageManager] prefab 없음"); return; }

        var inst = Instantiate(prefab, stageSpawnPoint.position, stageSpawnPoint.rotation);
        var no = inst.GetComponent<NetworkObject>();
        if (no) no.Spawn(); else Debug.LogError("[StageManager] prefab에 NetworkObject 없음");
    }

    IEnumerator ServerJudgeLoop()
    {
        while (!ended)
        {
            if (deadCount >= targetPigCount)
            {
                ended = true; ShowClearClientRpc(); yield break;
            }

            if (ballSpawner && ballSpawner.RemainingBalls == 0 && deadCount < targetPigCount)
            {
                bool anyBirdAlive = FindObjectsOfType<Bird>().Length > 0;
                if (!anyBirdAlive)
                {
                    ended = true; ShowFailClientRpc(); yield break;
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
        deadCount++;
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
