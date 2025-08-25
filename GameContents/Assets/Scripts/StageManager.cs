using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject clearPanel;
    public GameObject failPanel;

    [Header("Refs")]
    public BallSpawner ballSpawner; // 없으면 자동 탐색
    [Tooltip("시작 시 자동으로 씬에서 Pig(또는 Damageable)를 수집")]
    public bool autoCollectPigs = true;

    private readonly List<Damageable> pigs = new();
    private int alivePigCount;
    private bool isEnded;

    void Start()
    {
        if (!ballSpawner) ballSpawner = FindObjectOfType<BallSpawner>();

        // 패널 초기 상태
        if (clearPanel) clearPanel.SetActive(false);
        if (failPanel) failPanel.SetActive(false);

        // 돼지 수집 및 이벤트 구독
        if (autoCollectPigs)
        {
            // Pig만 찾고 싶으면 FindObjectsOfType<Pig>()로 바꿔도 OK
            foreach (var d in FindObjectsOfType<Damageable>())
            {
                // "Pig" 태그를 쓴다면: if (!d.CompareTag("Pig")) continue;
                pigs.Add(d);
                d.OnDied += OnPigDied;
            }
        }

        alivePigCount = pigs.Count;
    }

    void OnDestroy()
    {
        // 이벤트 해제(안전)
        foreach (var p in pigs)
            if (p != null) p.OnDied -= OnPigDied;
    }

    void Update()
    {
        if (isEnded) return;

        // 모든 돼지가 죽었는지 체크(안전망)
        if (alivePigCount <= 0)
        {
            Win();
            return;
        }

        // 실패 조건:
        // 1) 남은 공이 0이고
        // 2) 여전히 돼지가 남아 있으며
        // 3) 씬에 활성화된 Bird도 더 이상 없으면(마지막 공이 이미 사라졌는지 확인)
        if (ballSpawner && ballSpawner.RemainingBalls == 0 && alivePigCount > 0)
        {
            // 날아다니는 새가 남아 있으면 아직 결과 보류
            bool anyBirdAlive = FindObjectsOfType<Bird>().Length > 0;
            if (!anyBirdAlive)
                Fail();
        }
    }

    private void OnPigDied(Damageable _)
    {
        if (isEnded) return;

        alivePigCount = Mathf.Max(0, alivePigCount - 1);
        if (alivePigCount <= 0)
        {
            Win();
        }
        // 남아있으면 승패 보류 (공 소진 여부는 Update에서 확인)
    }

    private void Win()
    {
        if (isEnded) return;
        isEnded = true;
        if (clearPanel) clearPanel.SetActive(true);
        if (failPanel) failPanel.SetActive(false);
        Debug.Log("STAGE CLEAR");
        // 필요하면 Time.timeScale = 0f; 등 추가
    }

    private void Fail()
    {
        if (isEnded) return;
        isEnded = true;
        if (failPanel) failPanel.SetActive(true);
        if (clearPanel) clearPanel.SetActive(false);
        Debug.Log("STAGE FAIL");
    }
}
