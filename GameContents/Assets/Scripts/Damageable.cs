// Assets/Scripts/Net/DamageableNet.cs
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class Damageable : NetworkBehaviour
{
    [Header("HP")]
    public float maxHP = 50f;

    // HP는 서버가 기록, 모두 읽기 가능
    public NetworkVariable<float> currentHP = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public System.Action<Damageable> OnDied; // 로컬 이벤트(선택)

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            currentHP.Value = maxHP;
    }

    /// 서버만 호출하도록!
    public virtual void ApplyDamageServer(float amount, Vector3 hitPoint)
    {
        if (!IsServer) return;

        currentHP.Value -= amount;
        if (currentHP.Value <= 0f)
            DieServer();
    }

    protected virtual void DieServer()
    {
        // FX는 ClientRpc로
        PlayBreakFxClientRpc(transform.position);
        OnDied?.Invoke(this);

        // 네트워크 전파 파괴
        var no = GetComponent<NetworkObject>();
        if (no && no.IsSpawned) no.Despawn(); // 모든 클라에서 사라짐
        else Destroy(gameObject);
    }

    [ClientRpc]
    void PlayBreakFxClientRpc(Vector3 pos)
    {
        // 각 클라에서 파편/이펙트 재생하고 싶다면 여기서 처리
    }
}
