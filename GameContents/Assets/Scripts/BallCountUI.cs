// Assets/Scripts/BallCountUI.cs
using UnityEngine;
using TMPro;

public class BallCountUI : MonoBehaviour
{
    public BallSpawner spawner;
    public TMP_Text countText;
    [Tooltip("ǥ�� ����: {0}=���� ����, {1}=�ִ� ����")]
    public string format = "x {0}"; // "x {0}/{1}" �� �ٲٸ� "x 3/6" ���� ����

    void Awake()
    {
        if (!countText) countText = GetComponentInChildren<TMP_Text>();
    }

    void OnEnable()
    {
        if (spawner == null) spawner = FindObjectOfType<BallSpawner>();
        if (spawner != null)
        {
            spawner.OnCountChanged += HandleCountChanged;
            // ��� 1ȸ �ݿ�
            HandleCountChanged(spawner.RemainingBalls, spawner.MaxBalls);
        }
    }

    void OnDisable()
    {
        if (spawner != null)
            spawner.OnCountChanged -= HandleCountChanged;
    }

    void HandleCountChanged(int remaining, int max)
    {
        if (countText)
            countText.text = string.Format(format, remaining, max);
    }
}
