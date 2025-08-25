using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject clearPanel;
    public GameObject failPanel;

    [Header("Refs")]
    public BallSpawner ballSpawner; // ������ �ڵ� Ž��
    [Tooltip("���� �� �ڵ����� ������ Pig(�Ǵ� Damageable)�� ����")]
    public bool autoCollectPigs = true;

    private readonly List<Damageable> pigs = new();
    private int alivePigCount;
    private bool isEnded;

    void Start()
    {
        if (!ballSpawner) ballSpawner = FindObjectOfType<BallSpawner>();

        // �г� �ʱ� ����
        if (clearPanel) clearPanel.SetActive(false);
        if (failPanel) failPanel.SetActive(false);

        // ���� ���� �� �̺�Ʈ ����
        if (autoCollectPigs)
        {
            // Pig�� ã�� ������ FindObjectsOfType<Pig>()�� �ٲ㵵 OK
            foreach (var d in FindObjectsOfType<Damageable>())
            {
                // "Pig" �±׸� ���ٸ�: if (!d.CompareTag("Pig")) continue;
                pigs.Add(d);
                d.OnDied += OnPigDied;
            }
        }

        alivePigCount = pigs.Count;
    }

    void OnDestroy()
    {
        // �̺�Ʈ ����(����)
        foreach (var p in pigs)
            if (p != null) p.OnDied -= OnPigDied;
    }

    void Update()
    {
        if (isEnded) return;

        // ��� ������ �׾����� üũ(������)
        if (alivePigCount <= 0)
        {
            Win();
            return;
        }

        // ���� ����:
        // 1) ���� ���� 0�̰�
        // 2) ������ ������ ���� ������
        // 3) ���� Ȱ��ȭ�� Bird�� �� �̻� ������(������ ���� �̹� ��������� Ȯ��)
        if (ballSpawner && ballSpawner.RemainingBalls == 0 && alivePigCount > 0)
        {
            // ���ƴٴϴ� ���� ���� ������ ���� ��� ����
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
        // ���������� ���� ���� (�� ���� ���δ� Update���� Ȯ��)
    }

    private void Win()
    {
        if (isEnded) return;
        isEnded = true;
        if (clearPanel) clearPanel.SetActive(true);
        if (failPanel) failPanel.SetActive(false);
        Debug.Log("STAGE CLEAR");
        // �ʿ��ϸ� Time.timeScale = 0f; �� �߰�
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
