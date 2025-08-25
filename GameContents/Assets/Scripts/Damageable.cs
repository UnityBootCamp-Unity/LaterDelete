using UnityEngine;

public class Damageable : MonoBehaviour
{
    [Header("HP")]
    public float maxHP = 50f;
    public float currentHP;

    [Header("Optional FX")]
    public ParticleSystem breakFx;

    public System.Action<Damageable> OnDied; // 필요 없으면 지워도 됨

    void Awake()
    {
        currentHP = maxHP;
    }

    /// <summary>충돌 등으로 들어온 피해 처리</summary>
    public virtual void ApplyDamage(float amount, Vector3 hitPoint)
    {
        currentHP -= amount;
        if (currentHP <= 0f) Die();
    }

    protected virtual void Die()
    {
        if (breakFx) Instantiate(breakFx, transform.position, Quaternion.identity);
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }
}
