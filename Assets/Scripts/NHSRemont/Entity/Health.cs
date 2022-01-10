using System;
using NHSRemont.Environment;
using NHSRemont.Gameplay;
using NHSRemont.Utility;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Entity
{
    public class Health : MonoBehaviourPun, IDamageListener
    {
        [Header("Parameters")]
        [SerializeField] private float mass = 100f;
        public float maxHp = 100f;
        [Tooltip("Sound played when taking damage from an impact")]
        public SFXCollection impactSFX;
        [Tooltip("Profile which dictates how this entity is damaged by explosions")]
        public ExplosionDamageProfile explosionDamageProfile;

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

        private void Start()
        {
            this.RegisterWithPhysicsManager();
        }

        private void OnDestroy()
        {
            this.DeregisterWithPhysicsManager();
        }

        public void TakeImpactDamage(float impulseMagnitude, Vector3 point)
        {
            if(!photonView.IsMine)
                return;
            
            float dmg = (impulseMagnitude / mass)*4f - 40f;
            if (dmg > 10f)
            {
                impactSFX.PlayRandomSoundAtPosition(point, dmg / 40f);
                TakeDamage(dmg);
            }
        }

        private void TakeFallDamage(float fallVelocity, Vector3 impactPoint, Vector3 normal)
        {
            if(!photonView.IsMine)
                return;
            
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
            if(!photonView.IsMine)
                return;
            
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
            if(!photonView.IsMine)
                return;
            
            onDeath?.Invoke();
        }

        public void Revive()
        {
            if(!photonView.IsMine)
                return;
            
            hp = maxHp;
            onHealthChanged?.Invoke(this);
            onRevived?.Invoke();
        }

        public void OnCollisionEnter(Collision collision)
        {
            if(!photonView.IsMine)
                return;
            
            ContactPoint p = collision.GetContact(0);
            if(p.normal.y > 0f)
                TakeFallDamage(collision.relativeVelocity.y, p.point, p.normal);
        }

        public void OnExplosion(ExplosionInfo explosionInfo)
        {
            if(!photonView.IsMine)
                return;
            
            float damage = explosionDamageProfile.CalculateDamage(transform, explosionInfo);
            Debug.Log(name + " received " + damage + " explosion damage at a distance of " + (explosionInfo.position - transform.position).magnitude + " metres.");
            if(damage > 0f)
                TakeDamage(damage);
        }

        public void OnImpulseAtPoint(Vector3 impulse, Vector3 point)
        {
            if(!photonView.IsMine)
                return;
            
            Debug.Log(name + " received impulse " + impulse);
        }

        public void OnBulletDamage(RaycastHit hit, float damage)
        {
            if(!photonView.IsMine)
                return;
            
            TakeDamage(damage);
            impactSFX.PlayRandomSoundAtPosition(hit.point, 0.8f, 1.4f);
        }
    }
}
