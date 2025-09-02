// Assets/Scripts/BallCountUI.cs
using System.Collections;
using UnityEngine;
using TMPro;

public class BallCountUI : MonoBehaviour
{
    public BallSpawner spawner;
    public TMP_Text countText;
    [Tooltip("{0}=남은, {1}=최대")]
    public string format = "x {0}"; // 필요시 "x {0}/{1}"

    Coroutine bindRoutine;

    void Awake()
    {
        if (!countText) countText = GetComponentInChildren<TMP_Text>(true);
    }

    void OnEnable()
    {
        // 이미 배선돼 있으면 즉시 바인딩
        if (spawner != null) Bind(spawner);
        // 없으면 씬/네트워크 초기화가 끝날 때까지 잠깐 재시도
        if (spawner == null) bindRoutine = StartCoroutine(TryBindRoutine());
    }

    void OnDisable()
    {
        if (bindRoutine != null) { StopCoroutine(bindRoutine); bindRoutine = null; }
        Unbind();
    }

    IEnumerator TryBindRoutine()
    {
        // 최대 2초 정도 20ms 간격으로 찾아보기
        float t = 0f;
        while (t < 2f && spawner == null)
        {
            spawner = FindObjectOfType<BallSpawner>();
            if (spawner != null) { Bind(spawner); yield break; }
            t += 0.02f;
            yield return new WaitForSeconds(0.02f);
        }
    }

    void Bind(BallSpawner target)
    {
        Unbind(); // 중복 구독 방지
        spawner = target;
        spawner.OnCountChanged += HandleCountChanged;

        // 현재 값 즉시 반영
        HandleCountChanged(spawner.RemainingBalls, spawner.MaxBalls);
    }

    void Unbind()
    {
        if (spawner != null)
        {
            spawner.OnCountChanged -= HandleCountChanged;
        }
    }

    void HandleCountChanged(int remaining, int max)
    {
        if (!countText) return;
        countText.text = string.Format(format, remaining, max);
    }
}
