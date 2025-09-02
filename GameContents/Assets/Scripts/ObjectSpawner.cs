// Assets/Scripts/ObjectSpawnerNet.cs
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ObjectSpawnerNet : NetworkBehaviour
{
    public Transform spawnPoint;
    public string spawnPointName = "SpawnPoint";
    public GameObject prefabOverride; // (선택) 인스펙터로 지정

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return; // 서버만 스폰
        if (spawnPoint == null)
        {
            var go = GameObject.Find(spawnPointName);
            if (go) spawnPoint = go.transform;
        }
        if (spawnPoint == null || prefabOverride == null)
        {
            Debug.LogError("[ObjectSpawnerNet] Missing refs");
            return;
        }

        var go2 = Instantiate(prefabOverride, spawnPoint.position, spawnPoint.rotation);
        var no = go2.GetComponent<NetworkObject>();
        if (no) no.Spawn(); else Debug.LogError("Prefab needs NetworkObject!");
    }
}
