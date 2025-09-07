using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Linq;

public class StageNetworkSpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject stagePrefabRef;                  // (선택) 직참조
    [SerializeField] private string stageResourcePath = "Stages/Stage01Object";

    public NetworkVariable<ulong> StageNetId =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ────────────────────── Debug Helper ──────────────────────
    const bool VERBOSE = true;
    string Role()
    {
        var nm = NetworkManager;
        string where = Application.isBatchMode ? "Dedicated" : "Editor/Player";
        if (!nm) return $"{where} (No NM)";
        return $"{where} [Server={nm.IsServer} Client={nm.IsClient} Host={nm.IsHost} Listening={nm.IsListening}]";
    }
    void LOG(string msg) { if (VERBOSE) Debug.Log($"[StageSpawner] {msg} | {Role()}"); }
    void WARN(string msg) { Debug.LogWarning($"[StageSpawner] {msg} | {Role()}"); }
    void ERR(string msg) { Debug.LogError($"[StageSpawner] {msg} | {Role()}"); }

    // ────────────────────── State ──────────────────────
    bool _spawned;
    int _hookedNmId = -1;
    bool _sawListening;

    void Awake()
    {
        LOG($"Awake (GO='{gameObject.name}', Scene='{gameObject.scene.name}')");
        LOG($"Stage Resource Path = 'Resources/{stageResourcePath}'");
        StartCoroutine(WatchServerThenSpawnOnce());
    }

    IEnumerator WatchServerThenSpawnOnce()
    {
        LOG("WatchServerThenSpawnOnce 시작");

        while (true)
        {
            var nm = NetworkManager.Singleton;

            // ① NM 등장/교체 시 재구독
            if (nm != null && nm.GetInstanceID() != _hookedNmId)
            {
                LOG($"NetworkManager 교체 감지 → 재구독 (id: {_hookedNmId} → {nm.GetInstanceID()})");
                UnhookAll();
                _hookedNmId = nm.GetInstanceID();
                nm.OnServerStarted += OnNmServerStarted;
                LOG("OnServerStarted 구독 완료");
            }

            // ② 리스닝 시작 최초 감지(스팸 방지)
            if (nm != null && nm.IsListening && !_sawListening)
            {
                _sawListening = true;
                LOG("IsListening == TRUE 감지");
            }

            // ③ 서버(또는 호스트/배치) && 리스닝이면 1회 스폰
            if (!_spawned && nm != null && nm.IsListening && (nm.IsServer || nm.IsHost || Application.isBatchMode))
            {
                LOG("조건 충족 → TrySpawnStageOnce 호출(코루틴 경로)");
                TrySpawnStageOnce();
                _spawned = true;
                yield break; // 한 번만
            }

            // ④ 아직 조건 미충족일 때 원인 로깅(과도 스팸 방지: 간단히 특정 프레임 주기마다)
            if (Time.frameCount % 120 == 0)
            {
                if (nm == null) LOG("대기중: NetworkManager.Singleton == null");
                else if (!nm.IsListening) LOG("대기중: NetworkManager.IsListening == false");
                else if (!(nm.IsServer || nm.IsHost || Application.isBatchMode))
                    LOG("대기중: 현재 프로세스가 서버/호스트/배치가 아님");
            }

            yield return null;
        }
    }

    void OnNmServerStarted()
    {
        LOG("OnServerStarted 수신");
        if (_spawned) { LOG("이미 스폰됨 → 무시"); return; }

        var nm = NetworkManager.Singleton;
        if (nm != null && (nm.IsServer || nm.IsHost || Application.isBatchMode))
        {
            LOG("조건 충족 → TrySpawnStageOnce 호출(이벤트 경로)");
            TrySpawnStageOnce();
            _spawned = true;
        }
        else
        {
            LOG("OnServerStarted 이지만 서버/호스트/배치 아님 → 스킵");
        }
    }

    void UnhookAll()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnServerStarted -= OnNmServerStarted;
        }
    }

    void OnDisable()
    {
        LOG("OnDisable → 구독 해제");
        UnhookAll();
    }

    // (옵션) 외부에서 강제 호출하고 싶을 때 사용
    public void ForceServerSpawn()
    {
        LOG("ForceServerSpawn 호출");
        TrySpawnStageOnce();
    }

    public void SetResourcePath(string path)
    {
        stageResourcePath = path;
        LOG($"SetResourcePath → 'Resources/{stageResourcePath}'");
    }

    void TrySpawnStageOnce()
    {
        LOG("TrySpawnStageOnce 진입");

        if (!NetworkManager)
        {
            ERR("NetworkManager가 null → 스폰 불가");
            return;
        }

        if (!(NetworkManager.IsServer || NetworkManager.IsHost || Application.isBatchMode))
        {
            LOG("서버/호스트/배치가 아님 → 스폰 스킵");
            return;
        }

        LOG($"StageNetId(Current) = {StageNetId.Value}");
        if (StageNetId.Value != 0)
        {
            LOG("이미 스폰됨 → 스킵");
            return;
        }

        // 씬 인스턴스가 들어왔다면 무시하고 Resources 사용
        NetworkObject prefab = stagePrefabRef;
        if (prefab && prefab.gameObject.scene.IsValid())
        {
            WARN("stagePrefabRef가 씬 인스턴스 → 무시하고 Resources 사용");
            prefab = null;
        }

        // 프리팹 확보
        if (!prefab)
        {
            LOG($"Resources.Load 시도: Resources/{stageResourcePath}");
            var go = Resources.Load<GameObject>(stageResourcePath);
            if (!go) { ERR($"NOT FOUND: Resources/{stageResourcePath}"); return; }

            prefab = go.GetComponent<NetworkObject>();
            if (!prefab) { ERR("Prefab에 NetworkObject 없음"); return; }
            LOG($"Resources OK: {go.name}");
        }
        else
        {
            LOG($"직참조 프리팹 사용: {prefab.name}");
        }

        // 네트워크 프리팹 등록은 이미 정상 전제 → 검사 생략(원하면 아래 주석 해제)
        // var ok = NetworkManager.NetworkConfig.Prefabs.Prefabs.Any(p => p.Prefab && p.Prefab == prefab.gameObject);
        // if (!ok) { ERR($"'{prefab.name}' not in Network Prefabs"); return; }

        LOG("Instantiate & NetworkObject.Spawn(false) 시도");
        var inst = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        inst.Spawn(false);

        StageNetId.Value = inst.NetworkObjectId;
        LOG($"Spawn 성공 → id={StageNetId.Value} name={inst.name}");
    }
}
