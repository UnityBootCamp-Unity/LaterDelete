// Assets/Scripts/Bird.cs
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class Bird : NetworkBehaviour
{
    [Header("Damage by Impact")]
    public float damageScale = 0.6f;
    public float minDamageImpulse = 2f;

    [Header("Auto Despawn (no movement)")]
    public float sleepTimeToDestroy = 5f;
    public float sleepSpeedThreshold = 0.15f;

    private Rigidbody rb;
    private float sleepTimer;
    private bool isDespawning;

    // 클라에서도 카운트는 그대로 유지(로컬 표시용)
    public static int AliveCount { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void OnLaunched() => sleepTimer = 0f;

    void Update()
    {
        // 서버만 생존/삭제 판정 수행
        if (!IsServer || isDespawning) return;

        float speedSqr =
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity.sqrMagnitude;
#else
            rb.velocity.sqrMagnitude;
#endif
        bool almostStopped = rb.IsSleeping() || speedSqr < sleepSpeedThreshold * sleepSpeedThreshold;

        if (almostStopped) sleepTimer += Time.deltaTime;
        else sleepTimer = 0f;

        if (sleepTimer >= sleepTimeToDestroy)
            DespawnServer();
    }

    void OnCollisionEnter(Collision collision)
    {
        // 서버만 충돌 판정/데미지/삭제 수행
        if (!IsServer || isDespawning) return;

        // OutOfBounds에 부딪히면 즉시 제거
        if (collision.collider.CompareTag("OutOfBounds"))
        {
            DespawnServer();
            return;
        }

        // 충격량 기반 데미지
        float impulse = collision.impulse.magnitude;
        if (impulse >= minDamageImpulse)
        {
            // 네트워크 Damageable 우선
            if (collision.collider.TryGetComponent<Damageable>(out var dmgNet))
            {
                var hit = collision.GetContact(0).point;
                dmgNet.ApplyDamageServer(impulse * damageScale, hit);
            }
            /*
            else if (collision.collider.TryGetComponent<Damageable>(out var dmg))
            {
                var hit = collision.GetContact(0).point;
                dmg.ApplyDamage(impulse * damageScale, hit);
            }
            */
        }
    }

    // --- 네트워크 파괴(모든 클라이언트 동기화) ---
    void DespawnServer()
    {
        if (isDespawning) return;
        isDespawning = true;

        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn();        // 모든 클라에서 제거
        else
            Destroy(gameObject); // 안전망
    }

    // AliveCount는 각 클라이언트에서 UI 용도로만 사용
    void OnEnable() { AliveCount++; }
    void OnDisable() { AliveCount = Mathf.Max(0, AliveCount - 1); }
}
