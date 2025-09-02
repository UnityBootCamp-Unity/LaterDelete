// Assets/Scripts/Net/PigNet.cs
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class PigNet : Damageable
{
    [Header("Impulse → Damage")]
    public bool useSelfImpulseDamage = true;
    public float minImpulse = 2f;
    public float damageScale = 0.5f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (rb.collisionDetectionMode == CollisionDetectionMode.Discrete)
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void OnCollisionEnter(Collision collision)
    {
        // 서버만 판정 (중복/밀림 방지)
        if (!IsServer) return;

        // OutOfBounds 즉시 파괴
        if (collision.collider.CompareTag("OutOfBounds"))
        {
            DieServer();
            return;
        }

        if (!useSelfImpulseDamage) return;

        // Bird 충돌은 Bird 쪽에서 처리해도 되지만(중복 방지하려면),
        // 여기서는 간단히 자가 데미지 허용
        float impulse = collision.impulse.magnitude;
        if (impulse <= minImpulse) return;

        float damage = impulse * damageScale;
        var hitPoint = collision.GetContact(0).point;
        ApplyDamageServer(damage, hitPoint);
    }
}
