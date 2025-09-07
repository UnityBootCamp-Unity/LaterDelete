using UnityEngine;
using Unity.Netcode;
using System.Collections;   // ← 코루틴
using System.Linq;

public class StageNetworkSpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject stagePrefabRef;
    [SerializeField] private string stageResourcePath = "Stages/Stage01Object";

    public NetworkVariable<ulong> StageNetId =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── 디버그 헬퍼 ─────────────────────────────────────
    const bool VERBOSE = true;
    string Role()
    {
        var nm = NetworkManager;
        string r = Application.isBatchMode ? "Dedicated" : "Editor/Player";
        if (!nm) return $"{r} (No NM)";
        return $"{r} [Server={nm.IsServer} Client={nm.IsClient} Host={nm.IsHost} Listening={nm.IsListening}]";
    }
    void Log(string msg) { if (VERBOSE) Debug.Log($"[StageSpawner] {msg} | {Role()}"); }
    void Warn(string msg) { Debug.LogWarning($"[StageSpawner] {msg} | {Role()}"); }
    void Err(string msg) { Debug.LogError($"[StageSpawner] {msg} | {Role()}"); }

    void Awake()
    {
        Log($"Awake (GO='{gameObject.name}', Scene='{gameObject.scene.name}')");

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            Log($"Found NM. IsServer={nm.IsServer}, IsListening={nm.IsListening}");
            nm.OnServerStarted += () => { Log("OnServerStarted (event)"); TrySpawnStageOnce(); };
            Log("OnServerStarted 구독 완료");
        }
        else
        {
            Warn("NetworkManager.Singleton == null (Awake 시점)");
        }

        Log($"Stage Resource Path = 'Resources/{stageResourcePath}'");

        // ★★★ 이벤트를 놓쳐도 보장: 서버 준비될 때까지 기다렸다가 1회 스폰
        StartCoroutine(WaitServerThenSpawnOnce());
    }

    IEnumerator WaitServerThenSpawnOnce()
    {
        Log("WaitServerThenSpawnOnce 시작");

        // NM 생성될 때까지
        while (NetworkManager.Singleton == null) yield return null;
        var nm = NetworkManager.Singleton;

        // 리스닝 시작까지
        while (!nm.IsListening) yield return null;

        // 서버가 아니면 종료(에디터 클라이언트인 경우 여기서 끝)
        if (!nm.IsServer)
        {
            Log("로컬은 서버가 아님 → 대기 루틴 종료");
            yield break;
        }

        Log("로컬 서버 준비 완료 → TrySpawnStageOnce 호출");
        TrySpawnStageOnce();
    }

    public override void OnNetworkSpawn()
    {
        Log($"OnNetworkSpawn IsServer={IsServer}");
#if UNITY_SERVER
        TrySpawnStageOnce();
#else
        if (IsServer) TrySpawnStageOnce();
#endif
    }

    void TrySpawnStageOnce()
    {
        Log("TrySpawnStageOnce 진입");

        if (!NetworkManager || !NetworkManager.IsServer)
        {
            Warn("서버가 아니라 스폰 스킵");
            return;
        }

        Log($"StageNetId(Current) = {StageNetId.Value}");
        if (StageNetId.Value != 0)
        {
            Log("이미 스폰됨 → 스킵");
            return;
        }

        // 씬 인스턴스가 참조되어 있으면 무시(프리팹 에셋만 허용)
        if (stagePrefabRef && stagePrefabRef.gameObject.scene.IsValid())
        {
            Warn("Stage Prefab Ref가 '씬 인스턴스'임 → 무시하고 Resources 사용");
            stagePrefabRef = null;
        }

        // 프리팹 확보
        NetworkObject prefab = stagePrefabRef;
        if (!prefab)
        {
            Log($"Resources.Load: Resources/{stageResourcePath}");
            var go = Resources.Load<GameObject>(stageResourcePath);
            if (!go) { Err($"NOT FOUND: Resources/{stageResourcePath}"); return; }

            prefab = go.GetComponent<NetworkObject>();
            if (!prefab) { Err("Prefab에 NetworkObject 없음"); return; }
            Log($"Resources OK: {go.name}");
        }
        else
        {
            Log($"Direct Prefab Ref 사용: {prefab.name}");
        }

        // 네트워크 프리팹 등록 확인
        var list = NetworkManager.NetworkConfig.Prefabs.Prefabs;
        bool registered = list.Any(p => p.Prefab && p.Prefab == prefab.gameObject);
        if (!registered)
        {
            Err($"'{prefab.name}' 가 Network Prefabs에 없음");
            return;
        }

        // 스폰
        Log("Instantiate & Spawn 시도");
        var inst = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        inst.Spawn(false);
        StageNetId.Value = inst.NetworkObjectId;
        Log($"Spawn 성공 → id={StageNetId.Value} name={inst.name}");
    }
}
