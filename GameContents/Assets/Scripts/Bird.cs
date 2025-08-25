using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bird : MonoBehaviour
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

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void OnLaunched() => sleepTimer = 0f;

    void Update()
    {
        if (isDespawning) return;

        float speedSqr = rb.linearVelocity.sqrMagnitude;
        bool almostStopped = rb.IsSleeping() || speedSqr < sleepSpeedThreshold * sleepSpeedThreshold;

        if (almostStopped) sleepTimer += Time.deltaTime;
        else sleepTimer = 0f;

        if (sleepTimer >= sleepTimeToDestroy)
            Despawn();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isDespawning) return;

        // OutOfBounds에 부딪히면 즉시 제거
        if (collision.collider.CompareTag("OutOfBounds"))
        {
            Despawn();
            return;
        }

        // 데미지 계산
        float impulse = collision.impulse.magnitude;
        if (impulse >= minDamageImpulse &&
            collision.collider.TryGetComponent<Damageable>(out var dmgable))
        {
            float damage = impulse * damageScale;
            var hitPoint = collision.GetContact(0).point;
            dmgable.ApplyDamage(damage, hitPoint);
        }
    }

    void Despawn()
    {
        if (isDespawning) return;
        isDespawning = true;
        Destroy(gameObject);
    }
}
