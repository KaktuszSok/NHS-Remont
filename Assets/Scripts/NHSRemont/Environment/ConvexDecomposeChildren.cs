using UnityEngine;

namespace NHSRemont.Environment
{
    public class ConvexDecomposeChildren : MonoBehaviour
    {
        public VHACD.NHSRemont.Utility.V_HACD.VHACD.Parameters m_parameters;
        /// <summary>
        /// Should old renderers be replaced with many new renderers, one for each new convex mesh?
        /// </summary>
        public bool replaceRenderers = true;
    
        public ConvexDecomposeChildren() { m_parameters.Init(); }

        [ContextMenu("Return to Original Meshes")]
        public void ReturnToOriginalMeshes()
        {
            var colliders = GetComponentsInChildren<MeshCollider>();
            foreach (MeshCollider meshCollider in colliders)
            {
                if (meshCollider.transform.name.StartsWith("convex_"))
                {
                    DestroyImmediate(meshCollider.gameObject); //clear out old convex decompositions
                    continue;
                }

                var rend = replaceRenderers ? meshCollider.GetComponent<MeshRenderer>() : null;
                bool replaceRenderersForThisMesh = replaceRenderers && rend != null;
                if (meshCollider.transform.name.StartsWith("composite_")) //re-enable composite meshes
                {
                    meshCollider.enabled = true;
                    if (replaceRenderersForThisMesh)
                    {
                        rend.enabled = true;
                    }
                    meshCollider.transform.name = meshCollider.transform.name["composite_".Length..]; //remove "composite_" from name
                }
            }
        }

        [ContextMenu("Make Colliders Convex")]
        public void MakeCollidersConvex()
        {
            var colliders = GetComponentsInChildren<MeshCollider>();
            foreach (MeshCollider meshCollider in colliders)
            {
                if (meshCollider.transform.name.StartsWith("convex_"))
                {
                    DestroyImmediate(meshCollider.gameObject); //clear out old convex decompositions
                    continue;
                }

                var rend = replaceRenderers ? meshCollider.GetComponent<MeshRenderer>() : null;
                bool replaceRenderersForThisMesh = replaceRenderers && rend != null;
                if (meshCollider.transform.name.StartsWith("composite_")) //re-enable composite meshes
                {
                    meshCollider.enabled = true;
                    if (replaceRenderersForThisMesh)
                    {
                        rend.enabled = true;
                    }
                    meshCollider.transform.name = meshCollider.transform.name["composite_".Length..]; //remove "composite_" from name
                }
            
                if(meshCollider.convex || !meshCollider.enabled)
                    continue;

                var meshes = VHACD.NHSRemont.Utility.V_HACD.VHACD.GenerateConvexMeshes(meshCollider.sharedMesh, m_parameters);
                for (var i = 0; i < meshes.Count; i++)
                {
                    GameObject part = new GameObject("convex_" + i);
                    part.transform.SetParent(meshCollider.transform, false);
                
                    var partCollision = part.AddComponent<MeshCollider>();
                    partCollision.sharedMesh = meshes[i];
                    partCollision.convex = true;

                    if (replaceRenderersForThisMesh)
                    {
                        part.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                        var partRend = part.AddComponent<MeshRenderer>();
                        partRend.sharedMaterials = rend.sharedMaterials;
                    }
                }

                meshCollider.enabled = false;
                if (replaceRenderersForThisMesh)
                {
                    rend.enabled = false;
                }
                meshCollider.transform.name = "composite_" + meshCollider.transform.name;
            }
        }
    }
}
