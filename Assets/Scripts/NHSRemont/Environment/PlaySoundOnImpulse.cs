using NHSRemont.Environment.Fractures;
using NHSRemont.Gameplay;
using NHSRemont.Utility;
using UnityEngine;

namespace NHSRemont.Environment
{
    public class PlaySoundOnImpulse : MonoBehaviour, IDamageListener
    {
        [Tooltip("Sound that plays when a strong impulse is applied, but the object is not destroyed by it")]
        private SFXCollection hardImpulseSFX;
        private float thresholdMin, fullVolume, thresholdMax;
        private Rigidbody rb;

        private void OnDestroy()
        {
            this.DeregisterWithPhysicsManager();
        }

        /// <summary>
        /// Sets up this component
        /// </summary>
        /// <param name="sound">The sound to play</param>
        /// <param name="thresholdMin">Minimum impulse magnitude to play the sound (inclusive)</param>
        /// <param name="fullVolume">Impulse magnitude at which the sound reaches full volume</param>
        /// <param name="thresholdMax">Maximum impulse magnitude to play the sound (exclusive)</param>
        /// <param name="listenForExplosions">Should this object listen to PhysicsManager.onExplosion?</param>
        public void SetUp(SFXCollection sound, float thresholdMin, float fullVolume, float thresholdMax, bool listenForExplosions = true)
        {
            hardImpulseSFX = sound;
            this.thresholdMin = thresholdMin;
            this.thresholdMax = thresholdMax;
            this.fullVolume = fullVolume;
            
            if(listenForExplosions)
                this.RegisterWithPhysicsManager();
            rb = this.GetOrAddComponent<Rigidbody>();
        }
        
        public void OnCollisionEnter(Collision collision)
        {
            OnImpulseAtPoint(collision.impulse, collision.GetContact(0).point);
        }

        public void OnExplosion(ExplosionInfo explosionInfo)
        {
            if(!explosionInfo.IsPointWithinBlastRadius(transform.position))
                return;

            (Vector3 impulse, Vector3 point, _) = explosionInfo.CalculateImpulseAndPoint(rb);
            OnImpulseAtPoint(impulse, point);
        }

        public void OnImpulseAtPoint(Vector3 impulse, Vector3 point)
        {
            float sqrMag = impulse.sqrMagnitude;
            if (sqrMag >= thresholdMin * thresholdMin && sqrMag < thresholdMax * thresholdMax)
            {
                PlaySound(impulse, point);
            }
        }

        public void OnBulletDamage(RaycastHit hit, float damage)
        {
            
        }

        public void PlaySound(Vector3 impulse, Vector3 point)
        {
            float vol = Mathf.InverseLerp(thresholdMin, fullVolume, impulse.magnitude);
            hardImpulseSFX.PlayRandomSoundAtPosition(point, vol);
        }
    }
}