using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Pig : Damageable
{
    [Header("Impulse Damage (Self)")]
    public bool useSelfImpulseDamage = true;
    public float minImpulse = 2f;
    public float damageScale = 0.5f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (rb.collisionDetectionMode == CollisionDetectionMode.Discrete)
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void OnCollisionEnter(Collision collision)
    {
        // OutOfBounds�� ������ ��� ����
        if (collision.collider.CompareTag("OutOfBounds"))
        {
            Die();
            return;
        }

        if (!useSelfImpulseDamage) return;

        // Bird �浹�� Bird �ʿ��� ó��
        if (collision.collider.GetComponent<Bird>() != null)
            return;

        float impulse = collision.impulse.magnitude;
        if (impulse <= minImpulse) return;

        float damage = impulse * damageScale;
        var hitPoint = collision.GetContact(0).point;
        ApplyDamage(damage, hitPoint);
    }

    protected override void Die()
    {
        Debug.Log($"{name} destroyed!");
        base.Die();
    }
}
