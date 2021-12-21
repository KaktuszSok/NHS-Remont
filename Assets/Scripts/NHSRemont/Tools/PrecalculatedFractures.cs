using NHSRemont.Environment.Fractures;
using UnityEngine;

namespace NHSRemont.Tools
{
    /// <summary>
    /// Precalculates fractures for all children with a Fracturable component
    /// </summary>
    [RequireComponent(typeof(MasterGraph))]
    public class PrecalculatedFractures : MonoBehaviour
    {
        [ContextMenu("Precalculate Fractures")]
        public void Precalculate()
        {
            RemoveFractures();
            var targets = GetComponentsInChildren<Fracturable>(true);
            foreach (Fracturable target in targets)
            {
                if(target.enabled)
                    target.PrepareFracture();
            }

            MasterGraph graph = gameObject.GetOrAddComponent<MasterGraph>();
            graph.AutoSetup();
        }

        [ContextMenu("Remove Fractures")]
        public void RemoveFractures()
        {
            var fractures = GetComponentsInChildren<FracturedRenderer>(true);
            foreach (FracturedRenderer fractured in fractures)
            {
                DestroyImmediate(fractured.gameObject);
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
