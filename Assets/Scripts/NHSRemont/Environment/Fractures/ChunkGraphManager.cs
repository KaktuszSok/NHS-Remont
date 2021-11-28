using System.Collections.Generic;
using System.Linq;
using NHSRemont.Gameplay;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;

namespace NHSRemont.Environment.Fractures
{
    public class ChunkGraphManager : MonoBehaviour
    {
        private readonly Color[] colors =
        {
            Color.blue,
            Color.green,
            Color.magenta,
            Color.yellow
        };
        
        [SerializeField]
        private List<ChunkNode> nodes = new List<ChunkNode>();
        private bool graphChanged = false;

        public void Setup(List<ChunkNode> chunks)
        {
            nodes.Clear();
            foreach (ChunkNode chunk in chunks)
            {
                chunk.breakOffCallback += OnChunkBreakOff;
                nodes.Add(chunk);
            }

            graphChanged = true;
            RecalculateCombinedMesh();
        }

        public void InitialiseRuntimeFromPrecalculated()
        {
            if (nodes != null) //precalculated
            {
                foreach (ChunkNode chunk in nodes)
                {
                    chunk.breakOffCallback += OnChunkBreakOff;
                }

                graphChanged = true;
                RecalculateCombinedMesh();
            }
        }

        private void Start()
        {
            PhysicsManager.instance.onExplosion += OnExplosion;
        }

        private void OnDestroy()
        {
            PhysicsManager.instance.onExplosion -= OnExplosion;
            
            foreach (ChunkNode chunkNode in nodes)
            {
                chunkNode.breakOffCallback -= OnChunkBreakOff;
            }
        }

        private void FixedUpdate()
        {
            if (graphChanged)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    SearchGraph(nodes);
                }
                RecalculateCombinedMesh();

                graphChanged = false;
            }
        }

        public ChunkNode GetClosestNodeTo(Vector3 point)
        {
            ChunkNode closest = null;
            float closestSqDist = float.PositiveInfinity;
            foreach (ChunkNode chunkNode in nodes)
            {
                float sqDist = (chunkNode.transform.position - point).sqrMagnitude;
                if (sqDist < closestSqDist)
                {
                    closestSqDist = sqDist;
                    closest = chunkNode;
                }
            }
            return closest;
        }

        public IEnumerable<ChunkNode> GetAllNodes()
        {
            return nodes;
        }

        public void RecalculateCombinedMesh()
        {
            MeshRenderer combinedRend = gameObject.GetOrAddComponent<MeshRenderer>();
            MeshFilter combinedFilter = gameObject.GetOrAddComponent<MeshFilter>();
            Mesh combinedMesh = new Mesh();
            int submeshesCount = 0;

            if (nodes.Count > 0)
            {
                submeshesCount = nodes[0].collider.sharedMesh.subMeshCount;
            }
            CombineInstance[][] instances = new CombineInstance[submeshesCount][];
            CombineInstance[] submeshes = new CombineInstance[submeshesCount];
            int totalVerts = 0;
            for (int sub = 0; sub < submeshesCount; sub++)
            {
                instances[sub] = new CombineInstance[nodes.Count];
                for (var i = 0; i < nodes.Count; i++)
                {
                    var nodeMesh = nodes[i].collider.sharedMesh;
                    instances[sub][i] = new CombineInstance
                    {
                        mesh = nodeMesh,
                        transform = nodes[i].transform.localToWorldMatrix,
                        subMeshIndex = sub
                    };
                    totalVerts += nodeMesh.vertexCount;
                
                    if(sub > 0) continue; //only do these once per node:
                    
                    var rend = nodes[i].GetComponent<MeshRenderer>();
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

        public void SearchGraph(List<ChunkNode> objects)
        {
            if(!PhotonNetwork.IsMasterClient)
                return;
            
            var anchors = objects.Where(o => o.isAnchor).ToList();
                
            ISet<ChunkNode> connected = new HashSet<ChunkNode>(); //connected to anchor
            var index = 0;
            foreach (var anchor in anchors)
            {
                if (Application.isEditor)
                {
                    var subVisited = new HashSet<ChunkNode>();
                    Traverse(anchor, connected, subVisited);
                    var color = colors[index++ % colors.Length];
                    foreach (var sub in subVisited)
                    {
                        sub.Color = color;
                    }

                    connected.UnionWith(subVisited);
                }
                else
                {
                    Traverse(anchor, connected);
                }
            }

            var disconnectedChunks = objects.Where(x => !connected.Contains(x)).ToList();
            foreach (var chunk in disconnectedChunks)
            {
                chunk.Unfreeze();
                chunk.Color = Color.black;
            }
        }

        private void Traverse(ChunkNode curr, ISet<ChunkNode> visited, ISet<ChunkNode> newlyVisited)
        {
            if(visited.Contains(curr))
                return;

            visited.Add(curr);
            newlyVisited.Add(curr);
            foreach (ChunkNode neighbour in curr.GetAllNeighbours())
            {
                Traverse(neighbour, visited, newlyVisited);
            }
        }
        
        private void Traverse(ChunkNode curr, ISet<ChunkNode> visited)
        {
            if(visited.Contains(curr))
                return;

            visited.Add(curr);
            foreach (ChunkNode neighbour in curr.GetAllNeighbours())
            {
                Traverse(neighbour, visited);
            }
        }

        public void OnExplosion(ExplosionInfo explosionInfo)
        {
            foreach (ChunkNode chunkNode in nodes.ToArray())
            {
                chunkNode.OnExplosion(explosionInfo);
            }
        }

        private void OnChunkBreakOff(ChunkNode cn)
        {
            nodes.Remove(cn);
            cn.GetComponent<MeshRenderer>().enabled = true;
            
            graphChanged = true;
        }
    }
}