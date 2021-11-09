using System.Collections.Generic;
using UnityEngine;

namespace NHSRemont
{
    public readonly struct ExplosionInfo
    {
        /// <summary>
        /// The position of the explosion
        /// </summary>
        public readonly Vector3 position;
        /// <summary>
        /// The radius of the explosion's effect
        /// </summary>
        public readonly float blastRadius;
        /// <summary>
        /// The force of the explosion
        /// </summary>
        public readonly float power;
        /// <summary>
        /// The upwards modifier of the explosion
        /// </summary>
        public readonly float upwardsModifier;

        public ExplosionInfo(Vector3 position, float blastRadius, float power, float upwardsModifier=0f)
        {
            this.position = position;
            this.blastRadius = blastRadius;
            this.power = power;
            this.upwardsModifier = upwardsModifier;
        }

        public void ApplyToRigidbody(Rigidbody rb)
        {
            rb.AddExplosionForce(power, position, blastRadius, upwardsModifier, ForceMode.Impulse);
        }

        public void ApplyToRigidbodies(IEnumerable<Rigidbody> rbs)
        {
            foreach (Rigidbody rb in rbs)
            {
                ApplyToRigidbody(rb);
            }
        }
    }
}