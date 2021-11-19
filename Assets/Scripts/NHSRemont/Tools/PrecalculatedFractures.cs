using NHSRemont.Environment.Fractures;
using UnityEngine;

namespace NHSRemont.Tools
{
    /// <summary>
    /// Precalculates fractures for all children with a FractureThis component
    /// </summary>
    public class PrecalculatedFractures : MonoBehaviour
    {
        [ContextMenu("Precalculate Fractures")]
        public void Precalculate()
        {
            RemoveFractures();
            var targets = GetComponentsInChildren<FractureThis>(true);
            foreach (FractureThis target in targets)
            {
                target.PrepareFracture();
            }
        }

        [ContextMenu("Remove Fractures")]
        public void RemoveFractures()
        {
            var fractures = GetComponentsInChildren<ChunkGraphManager>(true);
            foreach (ChunkGraphManager chunkGraphManager in fractures)
            {
                DestroyImmediate(chunkGraphManager.gameObject);
            }
        }
    }
}
