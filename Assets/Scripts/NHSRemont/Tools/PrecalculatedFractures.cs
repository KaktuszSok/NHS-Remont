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
                if(target.enabled)
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

        [ContextMenu("Apply Convex Mesh Tools in Children")]
        public void ApplyChildMeshTools()
        {
            var meshTools = GetComponentsInChildren<ConvexWallWithWindows>();
            foreach (ConvexWallWithWindows tool in meshTools)
            {
                tool.GenerateWallWithWindows();
            }
        }

        [ContextMenu("Remove Convex Mesh Tool Effects in Children")]
        public void RemoveChildMeshToolEffects()
        {
            var meshTools = GetComponentsInChildren<ConvexWallWithWindows>();
            foreach (ConvexWallWithWindows tool in meshTools)
            {
                tool.RemoveGeneratedGameobjects();
            }
        }
    }
}
