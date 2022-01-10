using UnityEngine;

namespace NHSRemont.Gameplay.Projectiles
{
    public interface IProjectile
    {
        public void OnLaunched(float velocity);

        /// <summary>
        /// Sets whether this projectile is owned by the local client (true) or is just a visual copy (false)
        /// </summary>
        public void SetOwned(bool ownedByLocalClient);
    }
}