// Assets/Scripts/BallSpawner.cs
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Shared;

[RequireComponent(typeof(NetworkObject))]
public class BallSpawner : NetworkBehaviour
{
    [Header("Config")]
    public StageDatabase database;
    [Tooltip("현재 스테이지 ID (비워두면 씬 이름 사용)")]
    public string stageIdOverride;

    [Header("Refs")]
    public Transform playerRig;   // 선택: 회전 기준 등 필요 시
    public Transform birdPoint;   // 여기 위치/회전으로 소환 (클라에서 기준점 전달)
    public Bird birdPrefab;       // 반드시 NetworkObject 포함 프리팹

    [Header("(선택) 대기열 배치")]
    [Tooltip("여러 개 추가할 때 BirdPoint의 오른쪽으로 줄 세우기")]
    public bool queueAlongRight = false;
    public float spacing = 0.35f; // queueAlongRight가 true일 때만 사용

    [Tooltip("게임 상태 매니저 (인스펙터에서 할당)")]
    [SerializeField] private InGameManager inGameManager;

    // 서버 권위 동기화 값
    private NetworkVariable<int> _maxBallsNV =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _usedBallsNV =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // UI용 이벤트 (remaining, max)
    public event Action<int, int> OnCountChanged;

    public int MaxBalls => _maxBallsNV.Value;
    public int RemainingBalls => Mathf.Max(0, _maxBallsNV.Value - _usedBallsNV.Value);

    void Awake()
    {
        // 참조 누락 체크
        if (!birdPrefab)
            Debug.LogWarning("[BallSpawner] birdPrefab이 비었습니다. 서버 스폰 시 실패합니다.");
    }

    public override void OnNetworkSpawn()
    {
        // 서버에서 초기화
        if (IsServer)
        {
            var id = string.IsNullOrEmpty(stageIdOverride)
                ? SceneManager.GetActiveScene().name
                : stageIdOverride;
            int max = database ? database.GetBallCount(id, 3) : 3;
            _maxBallsNV.Value = max;
            _usedBallsNV.Value = 0;
        }

        // 값 변화 구독 → 로컬 UI 갱신
        _maxBallsNV.OnValueChanged += OnCountsChanged;
        _usedBallsNV.OnValueChanged += OnCountsChanged;

        // 초기 1회 갱신
        RaiseCountChanged();
    }

    public override void OnNetworkDespawn()
    {
        _maxBallsNV.OnValueChanged -= OnCountsChanged;
        _usedBallsNV.OnValueChanged -= OnCountsChanged;
    }

    void OnCountsChanged(int _, int __) => RaiseCountChanged();

    void RaiseCountChanged() => OnCountChanged?.Invoke(RemainingBalls, MaxBalls);

    /// <summary>
    /// UI 버튼에서 호출. 클라/호스트 모두 이 함수를 부르면 됨.
    /// </summary>
    public void OnAddButton()
    {
        // 추가: 클라/호스트 공통 빠른 가드 ? Playing 상태에서만 허용
        if (inGameManager == null || inGameManager.state.Value != InGameManager.State.Playing)
        {
            Debug.Log("[BallSpawner] Not in Playing state. Ignored.");
            return;
        }

        // 기준점(클라 로컬) 계산
        Vector3 basePos;
        Quaternion baseRot;
        Vector3 right;

        if (birdPoint)
        {
            basePos = birdPoint.position;
            baseRot = birdPoint.rotation;
            right = birdPoint.right;
        }
        else if (playerRig)
        {
            basePos = playerRig.position + playerRig.right * 0.6f + Vector3.up * 0.8f;
            baseRot = playerRig.rotation;
            right = playerRig.right;
        }
        else
        {
            basePos = Vector3.zero;
            baseRot = Quaternion.identity;
            right = Vector3.right;
        }

        // 서버에 스폰 요청 (호스트/클라 동일)
        RequestAddServerRpc(basePos, baseRot, right, queueAlongRight, spacing);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestAddServerRpc(
            Vector3 basePos, Quaternion baseRot, Vector3 right, bool queue, float gap,
            ServerRpcParams rpcParams = default)
    {
        // 추가: 서버 측 안전 가드 ? Playing 상태 아닐 때 무시
        if (inGameManager == null || inGameManager.state.Value != InGameManager.State.Playing)
        {
            Debug.Log("[BallSpawner] Server not in Playing state. Request ignored.");
            return;
        }

        // 재확인: 남은 개수
        if (_usedBallsNV.Value >= _maxBallsNV.Value)
            return;

        if (birdPrefab == null)
        {
            Debug.LogError("[BallSpawner] birdPrefab이 없어 스폰 불가");
            return;
        }

        // 서버 기준 스폰 위치 계산 (queue면 줄 세우기)
        Vector3 spawnPos = basePos;
        if (queue)
            spawnPos += right.normalized * gap * _usedBallsNV.Value;

        Quaternion spawnRot = baseRot;

        // 인스턴스 + Network Spawn
        var go = Instantiate(birdPrefab, spawnPos, spawnRot);
        var no = go.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("[BallSpawner] birdPrefab에 NetworkObject가 없습니다.");
            Destroy(go.gameObject);
            return;
        }
        no.Spawn(); // -> 모든 클라에 생성

        // 사용 카운트 증가(서버 권위)
        _usedBallsNV.Value = _usedBallsNV.Value + 1;
        // UI 갱신은 OnValueChanged로 각 클라에서 처리
    }

    /// <summary>스테이지 리셋 등 외부에서 호출</summary>
    [ServerRpc(RequireOwnership = false)]
    public void ResetUsageServerRpc()
    {
        _usedBallsNV.Value = 0;
    }
}
