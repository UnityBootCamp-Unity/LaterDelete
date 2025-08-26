// Assets/Scripts/ObjectSpawner.cs
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Spawn 위치")]
    public Transform spawnPoint;            // 비워두면 이름으로 찾음
    public string spawnPointName = "SpawnPoint";

    [Header("옵션")]
    public bool parentUnderThis = true;     // 생성한 오브젝트를 이 Spawner의 자식으로 둘지

    void Start()
    {
        if (StageInfoManager.Instance == null)
        {
            Debug.LogError("[ObjectSpawner] StageInfoManager가 없습니다.");
            return;
        }

        var prefab = StageInfoManager.Instance.GetSelectedStagePrefab();
        if (prefab == null)
        {
            Debug.LogError($"[ObjectSpawner] 선택된 스테이지({StageInfoManager.Instance.selectedStageId})의 프리팹이 설정되지 않았습니다.");
            return;
        }

        if (spawnPoint == null)
        {
            var go = GameObject.Find(spawnPointName);
            if (go != null) spawnPoint = go.transform;
        }

        if (spawnPoint == null)
        {
            Debug.LogError($"[ObjectSpawner] SpawnPoint를 찾을 수 없습니다. 이름: {spawnPointName}");
            return;
        }

        var instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        if (parentUnderThis) instance.transform.SetParent(transform, worldPositionStays: true);
    }
}
