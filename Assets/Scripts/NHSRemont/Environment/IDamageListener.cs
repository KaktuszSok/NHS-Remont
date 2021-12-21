using NHSRemont.Gameplay;
using UnityEngine;

namespace NHSRemont.Environment
{
    public interface IDamageListener
    {
        public void OnCollisionEnter(Collision collision);
        public void OnExplosion(ExplosionInfo explosionInfo);
        public void ApplyImpulseAtPoint(Vector3 impulse, Vector3 point);
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
    }
}