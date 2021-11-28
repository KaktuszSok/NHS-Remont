using System;
using NHSRemont.Utility;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Parameters")]
    [SerializeField] private float mass = 100f;
    public float maxHp = 100f;
    [Tooltip("Sound played when taking damage from an impact")]
    public SFXCollection impactSFX;

    //Runtime
    public float hp { get; private set; }

    public Action<Health> onHealthChanged;
    public Action onDeath;
    public Action onRevived;

    private void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb)
            mass = rb.mass;

        hp = maxHp;
    }

    public void TakeImpactDamage(float impulseMagnitude, Vector3 point)
    {
        float dmg = (impulseMagnitude / mass)*4f - 40f;
        if (dmg > 10f)
        {
            impactSFX.PlayRandomSoundAtPosition(point, dmg / 40f);
            TakeDamage(dmg);
        }
    }

    private void TakeFallDamage(float fallVelocity, Vector3 impactPoint, Vector3 normal)
    {
        if(fallVelocity < 0) return;
        
        float dmg = ((fallVelocity - 11f) / 10f) * 100f;
        dmg *= normal.y;
        if (dmg > 10f)
        {
            impactSFX.PlayRandomSoundAtPosition(impactPoint, dmg / 40f);
            TakeDamage(dmg);
        }
    }

    public void TakeDamage(float damage)
    {
        if(hp <= 0) return;

        hp -= damage;
        onHealthChanged?.Invoke(this);
        if (hp <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        onDeath?.Invoke();
    }

    public void Revive()
    {
        hp = maxHp;
        onHealthChanged?.Invoke(this);
        onRevived?.Invoke();
    }

    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint p = collision.GetContact(0);
        if(p.normal.y > 0f)
            TakeFallDamage(collision.relativeVelocity.y, p.point, p.normal);
    }
}
