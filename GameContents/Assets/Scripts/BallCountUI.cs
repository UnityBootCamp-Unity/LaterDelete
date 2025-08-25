// Assets/Scripts/BallCountUI.cs
using UnityEngine;
using TMPro;

public class BallCountUI : MonoBehaviour
{
    public BallSpawner spawner;
    public TMP_Text countText;
    [Tooltip("표시 형식: {0}=남은 개수, {1}=최대 개수")]
    public string format = "x {0}"; // "x {0}/{1}" 로 바꾸면 "x 3/6" 같은 형태

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
            // 즉시 1회 반영
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
