// Assets/Scripts/BallSpawner.cs
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BallSpawner : MonoBehaviour
{
    [Header("Config")]
    public StageBallDatabase database;
    [Tooltip("���� �������� ID (����θ� �� �̸� ���)")]
    public string stageIdOverride;

    [Header("Refs")]
    public Transform playerRig;   // ����: ȸ�� ���� �� �ʿ� ��
    public Transform birdPoint;   // ���� ��ġ/ȸ������ ��ȯ
    public Bird birdPrefab;       // ��(��) ������

    [Header("(����) ��⿭ ��ġ")]
    [Tooltip("���� �� �߰��� �� BirdPoint�� ���������� �� �����")]
    public bool queueAlongRight = false;
    public float spacing = 0.35f; // queueAlongRight�� true�� ���� ���

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
            RaiseCountChanged(); // ��ư ��Ȱ��ȭ UI ���� ���� �ѹ� �� �˸�
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