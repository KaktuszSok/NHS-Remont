using System;
using System.Collections;
using UnityEngine;

namespace NHSRemont.Gameplay.Projectiles
{
    public class RigidbodyProjectile : MonoBehaviour, IProjectile
    {
        public bool pointTowardsVelocity = true;

        private Rigidbody rb;
        
        public void OnLaunched(float velocity)
        {
            rb = GetComponent<Rigidbody>();
            StartCoroutine(SetUpOnFixedUpdate(velocity));
        }

        private IEnumerator SetUpOnFixedUpdate(float velocity)
        {
            yield return new WaitForFixedUpdate();
            rb.velocity = transform.forward * velocity;
        }

        public void SetOwned(bool ownedByLocalClient) { }

        private void FixedUpdate()
        {
            if (pointTowardsVelocity && rb && rb.velocity != Vector3.zero)
            {
                transform.forward = rb.velocity;
            }
        }
    }
}