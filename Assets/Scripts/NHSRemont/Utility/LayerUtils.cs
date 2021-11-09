using UnityEngine;

namespace NHSRemont.Utility
{
    public static class LayerUtils
    {
        /// <summary>
        /// Get a layer mask which is on only for layers that the given layer can collide with.
        /// </summary>
        public static LayerMask GetPhysicsCollisionMask(int layer)
        {
            int layerMask = 0;
            for (int i = 0; i < 32; i++)
            {
                if (!Physics.GetIgnoreLayerCollision(layer, i))
                {
                    layerMask |= 1 << i;
                }
            }
            return new LayerMask {value = layerMask};
        }
    }
}