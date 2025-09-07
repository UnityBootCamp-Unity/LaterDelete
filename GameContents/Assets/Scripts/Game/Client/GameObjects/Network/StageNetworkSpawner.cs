using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class StageNetworkSpawner : NetworkBehaviour
{
    [SerializeField] NetworkObject stagePrefabRef;
    [SerializeField] string stageResourcePath = "Stages/Stage1Object";

    public NetworkVariable<ulong> StageNetId =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    bool _spawned;
    NetworkManager _hookedNm;

    const bool VERBOSE = true;
    string Info(NetworkManager nm) => nm ? $"id={nm.GetInstanceID()} S={nm.IsServer} L={nm.IsListening}" : "NULL";
    void Log(string m) { if (VERBOSE) Debug.Log($"[StageSpawner] {m} | local[{Info(base.NetworkManager)}] singleton[{Info(NetworkManager.Singleton)}]"); }
    void Err(string m) => Debug.LogError($"[StageSpawner] {m}");

    NetworkManager LocalNM => base.NetworkManager != null ? base.NetworkManager : NetworkManager.Singleton;

    void Awake()
    {
        Log($"Awake (GO='{name}', Scene='{gameObject.scene.name}')");
        StartCoroutine(WaitAndSpawn());
    }

    void OnEnable() => Hook(LocalNM);
    void OnDisable() => Unhook();

    void Hook(NetworkManager nm)
    {
        if (nm == null || _hookedNm == nm) return;
        Unhook();
        _hookedNm = nm;
        nm.OnServerStarted += OnServerStarted;
        if (nm.SceneManager != null) nm.SceneManager.OnSceneEvent += OnSceneEvent;
        Log($"Hook NM: {Info(nm)}");
    }

    void Unhook()
    {
        if (_hookedNm == null) return;
        _hookedNm.OnServerStarted -= OnServerStarted;
        if (_hookedNm.SceneManager != null) _hookedNm.SceneManager.OnSceneEvent -= OnSceneEvent;
        _hookedNm = null;
    }

    IEnumerator WaitAndSpawn()
    {
        Log("WaitAndSpawn 시작");
        while (!_spawned)
        {
            var nm = LocalNM;
            if (nm != _hookedNm) Hook(nm);

            if (nm != null && nm.IsServer && nm.IsListening)
            {
                Log("조건 충족 → TrySpawnStageOnce()");
                TrySpawnStageOnce(nm);
                _spawned = true;
                yield break;
            }

            if (nm == null) Log("대기: local NM == null");
            else if (!nm.IsListening) Log("대기: nm.IsListening == false");
            else if (!nm.IsServer) Log("대기: nm.IsServer == false");

            yield return null;
        }
    }

    void OnServerStarted()
    {
        if (_spawned) return;
        var nm = _hookedNm ?? LocalNM;
        if (nm != null && nm.IsServer && nm.IsListening)
        {
            Log("OnServerStarted → TrySpawnStageOnce()");
            TrySpawnStageOnce(nm);
            _spawned = true;
        }
    }

    void OnSceneEvent(SceneEvent e)
    {
        if (_spawned) return;
        if ((e.SceneEventType == SceneEventType.LoadComplete || e.SceneEventType == SceneEventType.LoadEventCompleted)
            && e.SceneName == "Stage")
        {
            var nm = _hookedNm ?? LocalNM;
            if (nm != null && nm.IsServer && nm.IsListening)
            {
                Log($"SceneEvent {e.SceneEventType}('{e.SceneName}') → TrySpawnStageOnce()");
                TrySpawnStageOnce(nm);
                _spawned = true;
            }
        }
    }

    void TrySpawnStageOnce(NetworkManager nm)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton != nm)
        {
            Debug.LogError($"[StageSpawner] 두 개의 NetworkManager 발견! " +
                           $"local={nm.GetInstanceID()}(S={nm.IsServer},L={nm.IsListening}), " +
                           $"singleton={NetworkManager.Singleton.GetInstanceID()}(S={NetworkManager.Singleton.IsServer},L={NetworkManager.Singleton.IsListening}). " +
                           $"→ 중복 NM 제거하세요 (Stage 씬에서 NM 제거).");
            return;
        }

        Log("TrySpawnStageOnce()");
        if (!nm.IsServer || !nm.IsListening) { Log("서버/리스닝 조건 미충족"); return; }
        if (StageNetId.Value != 0) { Log($"이미 스폰됨 id={StageNetId.Value}"); return; }

        NetworkObject prefab = stagePrefabRef;
        if (prefab && prefab.gameObject.scene.IsValid()) prefab = null;

        if (!prefab)
        {
            Log($"Resources.Load('{stageResourcePath}') 시도");
            var go = Resources.Load<GameObject>(stageResourcePath);
            if (!go) { Err($"NOT FOUND: Resources/{stageResourcePath}"); return; }
            prefab = go.GetComponent<NetworkObject>();
            if (!prefab) { Err("Prefab에 NetworkObject 없음"); return; }
        }

        var inst = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        try
        {
            inst.Spawn(false);
            StageNetId.Value = inst.NetworkObjectId;
            Log($"SPAWN OK → id={StageNetId.Value} name={inst.name}");
        }
        catch (System.Exception ex)
        {
            Err($"Spawn 예외: {ex.GetType().Name} - {ex.Message}");
            Destroy(inst.gameObject);
        }
    }
}
