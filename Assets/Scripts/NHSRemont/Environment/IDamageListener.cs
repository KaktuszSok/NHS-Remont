using NHSRemont.Gameplay;
using UnityEngine;

namespace NHSRemont.Environment
{
    /// <summary>
    /// An interface for components which can be damaged by the game world.
    /// </summary>
    public interface IDamageListener
    {
        /// <summary>
        /// Handles the effects of a collision on this component.
        /// </summary>
        /// <param name="collision"></param>
        public void OnCollisionEnter(Collision collision);
        /// <summary>
        /// Handles the effects of an explosion on this component.
        /// </summary>
        /// <param name="explosionInfo"></param>
        public void OnExplosion(ExplosionInfo explosionInfo);
        /// <summary>
        /// Handles the effects of an impulse applied at some point.
        /// </summary>
        /// <param name="impulse"></param>
        /// <param name="point"></param>
        public void OnImpulseAtPoint(Vector3 impulse, Vector3 point);
        /// <summary>
        /// Handles the effects of a bullet hitting this component.
        /// </summary>
        /// <param name="hit"></param>
        /// <param name="damage"></param>
        public void OnBulletDamage(RaycastHit hit, float damage);
    }

    public static class DamageListenerExtensions
    {
        public static void RegisterWithPhysicsManager(this IDamageListener listener)
        {
            PhysicsManager.instance.onExplosion += listener.OnExplosion;
        }

        public static void DeregisterWithPhysicsManager(this IDamageListener listener)
        {
            PhysicsManager.instance.onExplosion -= listener.OnExplosion;
        }

        /// <summary>
        /// Applies an impulse at a point to all IDamageListeners attached to this gameobject, as well as the attached rigidbody (if present).
        /// </summary>
        public static void ApplyImpulseAtPoint(this GameObject target, Vector3 impulse, Vector3 point)
        {
            foreach (IDamageListener damageListener in target.GetComponents<IDamageListener>())
            {
                damageListener.OnImpulseAtPoint(impulse, point);
            }

            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);
            }
        }
    }
}