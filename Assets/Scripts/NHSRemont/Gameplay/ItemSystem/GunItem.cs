using JetBrains.Annotations;
using NHSRemont.Entity;
using NHSRemont.Gameplay.ItemSystem.Modules.Slots;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem
{
    public class GunItem : Item
    {
        public override float punchPower => 0f;

        private BarrelModuleSlot barrel;
        private AmmoModuleSlot ammo;

        protected override void Awake()
        {
            base.Awake();
            barrel = GetComponent<BarrelModuleSlot>();
            ammo = GetComponent<AmmoModuleSlot>();
        }

        private void Start()
        {
            if (photonView.IsMine)
            {
                barrel.TryFindModuleToFit(transform);
                ammo.TryFindModuleToFit(transform);
            }
        }

        public GameObject TryShoot([CanBeNull] Transform lookingTransform)
        {
            if (!photonView.IsMine || barrel.currentModule == null)
                return null;

            if (!ammo.CanShoot())
            {
                return null;
            }
            
            (Vector3 position, Vector3 forward) = barrel.currentModule.GetProjectileSpawnLocation(lookingTransform);
            float projectileVelocity = barrel.currentModule.GetBaseMuzzleVelocity();
            GameObject projectile = ammo.currentModule.SpawnProjectile(position, forward, projectileVelocity);
            ammo.Deplete();

            photonView.RPC(nameof(ShootEffectsRPC), RpcTarget.All);
            return projectile;
        }

        public override void WhileHeld(CharacterInventory holderInventory)
        {
            if (Input.GetMouseButtonDown(0))
            {
                TryShoot(holderInventory == null ? null : holderInventory.lookingTransform);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ammo.Reload(holderInventory);
            }
        }

        /// <summary>
        /// Plays visual and sound effects of shooting this weapon
        /// </summary>
        [PunRPC]
        public void ShootEffectsRPC()
        {
            
        }
    }
}