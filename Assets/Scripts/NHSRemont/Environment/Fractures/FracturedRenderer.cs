using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NHSRemont.Environment.Fractures
{
    /// <summary>
    /// Renderer for part of a structure which has been fractured into chunks.
    /// Frozen chunks are combined into one mesh. Loose chunks have their mesh renderer re-enabled.
    /// </summary>
    public class FracturedRenderer : MonoBehaviour
    {
        [SerializeField] public List<ChunkNode> chunks = new();
        private bool graphChanged = false;

        public void Setup(List<ChunkNode> chunks)
        {
            this.chunks.Clear();
            foreach (ChunkNode chunk in chunks)
            {
                chunk.breakOffCallbackLate += OnChunkBreakOff;
                this.chunks.Add(chunk);
            }

            graphChanged = true;
            RecalculateCombinedMesh();
        }
        
        public void InitialiseRuntimeFromPrecalculated()
        {
            if (chunks != null) //precalculated
            {
                foreach (ChunkNode chunk in chunks)
                {
                    chunk.breakOffCallbackLate += OnChunkBreakOff;
                }

                graphChanged = true;
                RecalculateCombinedMesh();
            }
        }
        
        private void FixedUpdate()
        {
            if (graphChanged)
            {
                RecalculateCombinedMesh();
                graphChanged = false;
            }
        }
        
        public void RecalculateCombinedMesh()
        {
            MeshRenderer combinedRend = gameObject.GetOrAddComponent<MeshRenderer>();
            MeshFilter combinedFilter = gameObject.GetOrAddComponent<MeshFilter>();
            Mesh combinedMesh = new Mesh();
            int submeshesCount = 0;

            if (chunks.Count > 0)
            {
                submeshesCount = ((MeshCollider)chunks[0].collider).sharedMesh.subMeshCount;
            }
            CombineInstance[][] instances = new CombineInstance[submeshesCount][];
            CombineInstance[] submeshes = new CombineInstance[submeshesCount];
            int totalVerts = 0;
            for (int sub = 0; sub < submeshesCount; sub++)
            {
                instances[sub] = new CombineInstance[chunks.Count];
                for (int i = 0; i < chunks.Count; i++)
                {
                    Mesh nodeMesh = ((MeshCollider)chunks[i].collider).sharedMesh;
                    instances[sub][i] = new CombineInstance
                    {
                        mesh = nodeMesh,
                        transform = chunks[i].transform.localToWorldMatrix,
                        subMeshIndex = sub
                    };
                    totalVerts += nodeMesh.vertexCount;
                
                    if(sub > 0) continue; //only do these once per node:
                    
                    MeshRenderer rend = chunks[i].GetComponent<MeshRenderer>();
                    rend.enabled = false;
                    if (i == 0)
                    {
                        combinedRend.sharedMaterials = rend.sharedMaterials;
                    }
                }

                Mesh submesh = new Mesh();
                if(totalVerts > ushort.MaxValue)
                    submesh.indexFormat = IndexFormat.UInt32;
                submesh.CombineMeshes(instances[sub], true, true);
                submeshes[sub] = new CombineInstance
                {
                    mesh = submesh,
                    transform = this.transform.worldToLocalMatrix
                };
            }
            if(totalVerts > ushort.MaxValue)
                combinedMesh.indexFormat = IndexFormat.UInt32;
            combinedMesh.CombineMeshes(submeshes, false);
            combinedMesh.Optimize();
            combinedMesh.RecalculateBounds();
            combinedFilter.sharedMesh = combinedMesh;
        }

        private void OnChunkBreakOff(GraphNode node)
        {
            chunks.Remove((ChunkNode)node);
            node.GetComponent<MeshRenderer>().enabled = true;
            if (!graphChanged)
            {
                node.GetComponent<NHSWall>().material.breakOffSound.PlayRandomSoundAtPosition(node.transform.position);
            }

            graphChanged = true;
        }
    }
}