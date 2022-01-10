using JetBrains.Annotations;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem.Modules
{
    public class BarrelModule : ItemModule
    {
        [SerializeField]
        private Transform projectileSpawnTransform;
        [SerializeField]
        private float baseMuzzleVelocity = 780f;

        /// <summary>
        /// Gets the appropriate point and direction for a projectile shot from this barrel.
        /// Position is adjusted so that we can not shoot through walls
        /// </summary>
        /// <param name="aimTransform">The transform we are aiming with (e.g. player head), which we use to determine if we would shoot through a wall or not.</param>
        public (Vector3 position, Vector3 forward) GetProjectileSpawnLocation([CanBeNull] Transform aimTransform)
        {
            return (projectileSpawnTransform.position, projectileSpawnTransform.forward); //TODO make it so we can't shoot through walls
        }

        /// <summary>
        /// Gets the base muzzle velocity for projectiles shot out this barrel.
        /// Final muzzle velocity may vary depending on the projectile being shot.
        /// </summary>
        public float GetBaseMuzzleVelocity()
        {
            return baseMuzzleVelocity;
        }
    }
}