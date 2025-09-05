// Assets/Scripts/BallSpawner.cs
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkObject))]
public class BallSpawner : NetworkBehaviour
{
    [Header("Config")]
    public StageDatabase database;
    public string stageIdOverride;

    [Header("Refs")]
    public Transform playerRig;   // 선택: 기준점
    public Transform birdPoint;   // 새 소환 위치
    public Bird birdPrefab;       // 반드시 NetworkObject 포함

    [Header("(Optional) Queue")]
    public bool queueAlongRight = false;
    public float spacing = 0.35f;

    // 서버 권위 변수
    private NetworkVariable<int> _maxBalls =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _usedBalls =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // UI 이벤트
    public event Action<int, int> OnCountChanged;

    public int MaxBalls => _maxBalls.Value;
    public int RemainingBalls => Mathf.Max(0, _maxBalls.Value - _usedBalls.Value);

    void Awake()
    {
        if (!birdPrefab)
            Debug.LogWarning("[BallSpawner] birdPrefab 비어 있음");
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            string id =
                !string.IsNullOrEmpty(stageIdOverride) ? stageIdOverride :
                StageInfoManager.Instance ? StageInfoManager.Instance.selectedStageId :
                SceneManager.GetActiveScene().name;

            _maxBalls.Value = database ? database.GetBallCount(id, 3) : 3;
            _usedBalls.Value = 0;
        }

        _maxBalls.OnValueChanged += (_, __) => RaiseCountChanged();
        _usedBalls.OnValueChanged += (_, __) => RaiseCountChanged();
        RaiseCountChanged();
    }

    public override void OnNetworkDespawn()
    {
        _maxBalls.OnValueChanged -= (_, __) => RaiseCountChanged();
        _usedBalls.OnValueChanged -= (_, __) => RaiseCountChanged();
    }

    void RaiseCountChanged() => OnCountChanged?.Invoke(RemainingBalls, MaxBalls);

    /// UI 버튼 연결용
    public void OnAddButton()
    {
        Vector3 pos; Quaternion rot; Vector3 right;

        if (birdPoint)
        {
            pos = birdPoint.position; rot = birdPoint.rotation; right = birdPoint.right;
        }
        else if (playerRig)
        {
            pos = playerRig.position + playerRig.right * 0.6f + Vector3.up * 0.8f;
            rot = playerRig.rotation; right = playerRig.right;
        }
        else
        {
            pos = Vector3.zero; rot = Quaternion.identity; right = Vector3.right;
        }

        RequestAddServerRpc(pos, rot, right, queueAlongRight, spacing);
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestAddServerRpc(Vector3 basePos, Quaternion baseRot, Vector3 right, bool queue, float gap, ServerRpcParams _ = default)
    {
        Debug.Log($"[BallSpawner] RequestAddServerRpc called. used={_usedBalls.Value}, max={_maxBalls.Value}");

        if (_usedBalls.Value >= _maxBalls.Value)
        {
            Debug.Log("[BallSpawner] Max balls reached.");
            return;
        }
        if (!birdPrefab)
        {
            Debug.LogError("[BallSpawner] birdPrefab 없음");
            return;
        }

        Vector3 spawnPos = queue ? basePos + right.normalized * gap * _usedBalls.Value : basePos;
        Debug.Log($"[BallSpawner] Spawning Bird at {spawnPos}");

        var go = Instantiate(birdPrefab, spawnPos, baseRot);
        var no = go.GetComponent<NetworkObject>();
        if (!no)
        {
            Debug.LogError("[BallSpawner] birdPrefab에 NetworkObject 없음");
            Destroy(go);
            return;
        }

        no.Spawn();
        Debug.Log("[BallSpawner] Bird spawned!");
        _usedBalls.Value += 1;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetUsageServerRpc() => _usedBalls.Value = 0;
}
