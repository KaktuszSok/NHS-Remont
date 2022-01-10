using System;
using NHSRemont.Gameplay.Projectiles;
using NHSRemont.Networking;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem.Modules
{
    public class AmmoModule : ItemModule
    {
        [Header("Ammo Module")]
        public GameObject projectilePrefab;
        
        [SerializeField] private bool disappearOnceEmpty = false;
        [SerializeField] private int _maxAmmo = 10;
        public int maxAmmo => _maxAmmo;
        [SerializeField] private int _ammoCount = 10;
        public int ammoCount => _ammoCount;

        /// <summary>
        /// Depletes the stored ammunition by one shot
        /// </summary>
        public void Deplete()
        {
            SetAmmo(ammoCount-1);
        }

        /// <summary>
        /// Fills the ammunition by some amount (does not overfill)
        /// </summary>
        public void Fill(int fillAmount)
        {
            int newAmt = Mathf.Min(maxAmmo, ammoCount + fillAmount);
            SetAmmo(newAmt);
        }

        public void SetAmmo(int ammoCount)
        {
            if (!photonView.IsMine)
            {
                Debug.LogWarning(name + " - only owner can set ammo amount.", this);
                return;   
            }

            _ammoCount = Mathf.Clamp(ammoCount, 0, maxAmmo);
            if (disappearOnceEmpty && this.ammoCount == 0)
            {
                amount--; //reduce stack size by 1 (this is not ammo amount!)
            }
        }

        public GameObject SpawnProjectile(Vector3 position, Vector3 forward, float velocity)
        {
            if (!photonView.IsMine)
            {
                Debug.LogWarning(name + " - only owner can spawn a projectile.", this);
                return null;
            }
            
            if (projectilePrefab.GetComponent<PhotonView>() == null)
            {
                photonView.RPC(nameof(SpawnLocalProjectileRPC), RpcTarget.Others, position, forward, velocity);
                return SpawnLocalProjectileRPC(position, forward, velocity); //call function locally without use of RPC as we need its return value
            }
            else
            {
                GameObject projectile = PhotonNetwork.Instantiate(projectilePrefab.name, position, Quaternion.LookRotation(forward));
                SetUpProjectile(projectile, velocity);
                return projectile;
            }
        }

        [PunRPC]
        public GameObject SpawnLocalProjectileRPC(Vector3 position, Vector3 forward, float velocity)
        {
            GameObject projectile = Instantiate(projectilePrefab, position, Quaternion.LookRotation(forward));
            SetUpProjectile(projectile, velocity);
            return projectile;
        }

        private void SetUpProjectile(GameObject projectile, float velocity)
        {
            foreach (IProjectile component in projectile.GetComponents<IProjectile>())
            {
                component.SetOwned(photonView.IsMine);
                component.OnLaunched(velocity);
            }
        }

        public override bool CanCombineStacks(Item other)
        {
            return base.CanCombineStacks(other) && maxAmmo == 1 && disappearOnceEmpty; //only single-projectile ammo (like bullets) can stack. No ammo container stacking.
        }

        public override void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            try
            {
                base.OnPhotonSerializeView(stream, info);
                if (stream.IsWriting)
                {
                    stream.SendNext((short) ammoCount);
                }

                if (stream.IsReading)
                {
                    _ammoCount = stream.ReceiveNext<short>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e, this);
            }
        }
    }
}