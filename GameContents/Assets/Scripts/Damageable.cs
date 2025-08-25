using UnityEngine;

public class Damageable : MonoBehaviour
{
    [Header("HP")]
    public float maxHP = 50f;
    public float currentHP;

    [Header("Optional FX")]
    public ParticleSystem breakFx;

    public System.Action<Damageable> OnDied; // �ʿ� ������ ������ ��

    void Awake()
    {
        currentHP = maxHP;
    }

    /// <summary>�浹 ������ ���� ���� ó��</summary>
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
