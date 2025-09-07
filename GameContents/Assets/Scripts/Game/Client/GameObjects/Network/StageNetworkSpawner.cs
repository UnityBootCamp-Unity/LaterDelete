using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class StageNetworkSpawner : NetworkBehaviour
{
    // ✅ 직참조(더 안전) — 인스펙터에 Resources에 있는 Stage 프리팹을 그대로 드래그
    [SerializeField] private NetworkObject stagePrefabRef;

    // (옵션) Resources 경로도 유지하고 싶다면 남겨둠
    [SerializeField] private string stageResourcePath = "Stages/Stage01Object";

    public NetworkVariable<ulong> StageNetId =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        // ▶ OnNetworkSpawn이 안 불려도 서버 시작 때 한 번 더 시도
        var nm = NetworkManager.Singleton;
        if (nm != null) nm.OnServerStarted += () =>
        {
            Debug.Log("[StageSpawner] OnServerStarted");
            TrySpawnStageOnce();
        };
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[StageSpawner] OnNetworkSpawn IsServer={IsServer}");
#if UNITY_SERVER
        TrySpawnStageOnce();
#else
        if (IsServer) TrySpawnStageOnce();
#endif
    }

    void TrySpawnStageOnce()
    {
        if (!NetworkManager || !NetworkManager.IsServer) return;
        if (StageNetId.Value != 0) return;

        // ① 프리팹 확보: 직참조 우선
        NetworkObject prefab = stagePrefabRef;
        if (!prefab)
        {
            var go = Resources.Load<GameObject>(stageResourcePath);
            if (!go) { Debug.LogError($"[StageSpawner] NOT FOUND: Resources/{stageResourcePath}"); return; }
            prefab = go.GetComponent<NetworkObject>();
            if (!prefab) { Debug.LogError("[StageSpawner] Prefab missing NetworkObject"); return; }
        }

        // ② 네트워크 프리팹 등록 확인(같은 에셋인지 확인)
        var list = NetworkManager.NetworkConfig.Prefabs.Prefabs;
        bool registered = list.Any(p => p.Prefab && p.Prefab == prefab.gameObject);
        if (!registered)
        {
            Debug.LogError($"[StageSpawner] '{prefab.name}' is NOT in Network Prefabs (같은 에셋을 등록하세요)");
            return;
        }

        // ③ 스폰
        var inst = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        inst.Spawn(false);
        StageNetId.Value = inst.NetworkObjectId;
        Debug.Log($"[StageSpawner] Spawned id={StageNetId.Value} name={inst.name}");
    }
}
