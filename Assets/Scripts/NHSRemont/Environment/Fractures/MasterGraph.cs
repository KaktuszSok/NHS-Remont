using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NHSRemont.Gameplay;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Environment.Fractures
{
    /// <summary>
    /// Graph describing the state of a destructible structure
    /// </summary>
    public class MasterGraph : MonoBehaviour
    {

        private const float adjecentChunksProximity = 0.1f; //how close two chunks must be (at any vertex) to be considered as touching

        [Tooltip("Parent of the colliders which will act as the indestructible skeleton of this building")]
        [SerializeField] private Transform skeletonParent;
        [Tooltip("Anchor chunks who's centre of mass is this close to the skeleton (or less) will be indestructible")]
        [SerializeField] private float indestructibleChunksMaxSkeletonDistance = 0.5f;
        
        [SerializeField]
        private List<GraphNode> nodes = new();
        private bool graphChanged;

        private void Start()
        {
            foreach (GraphNode node in nodes)
            {
                node.breakOffCallbackLate += OnNodeBreakOff;
            }
            
            StartCoroutine(SetAnchors()); //must be done during gameplay as physics overlap doesn't work quite right when in prefab edit mode
        }
        
        /// <summary>
        /// Prepare the graph of all nodes in this structure
        /// </summary>
        public void AutoSetup()
        {
            if (skeletonParent == null)
            {
                Debug.LogError($"Can not set up master graph for {name} as skeleton parent is null!", this);
                return;
            }

            ClearNodes();
            AddNodes(GetComponentsInChildren<GraphNode>(true));
            StartCoroutine(GenerateGraphEdges());
        }

        private IEnumerator GenerateGraphEdges()
        {
            Queue<ChunkNode> unprocessedChunks = new Queue<ChunkNode>(nodes.OfType<ChunkNode>());
            //activate fractured gameobjects cause they need to be caught by OverlapBox
            FracturedRenderer[] chunkParents = GetComponentsInChildren<FracturedRenderer>(true);
            bool[] wasChunkParentActive = new bool[chunkParents.Length];
            for (int i = 0; i < chunkParents.Length; i++)
            {
                wasChunkParentActive[i] = chunkParents[i].gameObject.activeSelf;
                chunkParents[i].gameObject.SetActive(true);
            }

            Bounds[] boundingBoxes = new Bounds[unprocessedChunks.Count];
            {
                int i = 0;
                foreach (ChunkNode unprocessedChunk in unprocessedChunks)
                {
                    boundingBoxes[i] = unprocessedChunk.meshCollider.bounds;
                    i++;
                }
            }

            yield return null;
            //connect touching chunks
            int q = 0;
            while (unprocessedChunks.TryDequeue(out ChunkNode chunk))
            {
                Vector3[] verts = chunk.meshCollider.sharedMesh.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i] = chunk.transform.TransformPoint(verts[i]);
                }
                Bounds boundsInflated = boundingBoxes[q];
                boundsInflated.Expand(adjecentChunksProximity);
                int j = q+1;
                foreach (ChunkNode other in unprocessedChunks) //check all other chunks that haven't been checked against this one
                {
                    Bounds otherBounds = boundingBoxes[j];
                    j++;
                    //Debug.Log(boundsInflated + "/" + otherBounds);
                    if (!otherBounds.Intersects(boundsInflated)) //discard any that don't pass bounds check
                        continue;

                    foreach (Vector3 vert in verts)
                    {
                        //check each vertex to see if it is touching the other chunk
                        //Debug.Log("sqrDist=" + (other.meshCollider.ClosestPoint(vert) - vert).sqrMagnitude + "(pt=" + other.meshCollider.ClosestPoint(vert) + ", v=" + vert + ")");
                        //Debug.Log("legal=" + other.collider.bounds.Contains(other.collider.ClosestPoint(vert)) + "/" + other.collider.bounds.Contains(vert) + "/" + other.collider.bounds);
                        if ((other.meshCollider.ClosestPoint(vert) - vert).sqrMagnitude <=
                            adjecentChunksProximity * adjecentChunksProximity)
                        {
                            chunk.AddNeighbour(other);
                            other.AddNeighbour(chunk);
                            break;
                        }
                    }
                }
                chunk.SaveNeighbours();
                //Debug.Log(chunk.name + " connected to " + chunk.neighbours.Count + " neighbours");
                
                //de-activate fractured gameobjects
                for (int i = 0; i < chunkParents.Length; i++)
                {
                    chunkParents[i].gameObject.SetActive(wasChunkParentActive[i]);
                }

                q++;
            }
        }

        private IEnumerator SetAnchors()
        {
            yield return null;

            //activate fractured gameobjects cause they need to be caught by OverlapBox
            FracturedRenderer[] chunkParents = GetComponentsInChildren<FracturedRenderer>(true);
            bool[] wasChunkParentActive = new bool[chunkParents.Length];
            for (int i = 0; i < chunkParents.Length; i++)
            {
                wasChunkParentActive[i] = chunkParents[i].gameObject.activeSelf;
                chunkParents[i].gameObject.SetActive(true);
            }

            yield return null;
            //Physics.SyncTransforms();

            IEnumerable<Collider> skeletonColliders = skeletonParent.GetComponentsInChildren<Collider>();
            //anchor chunks touching skeleton
            foreach (Collider skeletonCollider in skeletonColliders)
            {
                Transform skeletonTransform = skeletonCollider.transform;
                (Vector3 extents, Vector3 centre) = skeletonCollider switch
                {
                    BoxCollider col => (col.size / 2f, col.center),
                    SphereCollider col => (Vector3.one*col.radius, col.center),
                    CapsuleCollider col => (new Vector3(col.radius, col.radius + col.height/2f, col.radius), col.center),
                    _ => (Vector3.one/2f, Vector3.zero)
                };
                Vector3 indestructibleExtents = extents; //skeleton-local extents of the box that will make chunks indestructible if their centre resides within it
                indestructibleExtents += Vector3.one * indestructibleChunksMaxSkeletonDistance;
                extents.Scale(skeletonTransform.lossyScale);
                extents += Vector3.one * adjecentChunksProximity;
                var others = Physics.OverlapBox(skeletonTransform.TransformPoint(centre), extents, skeletonTransform.rotation);
                //Debug.Log("found " + others.Length + " overlaps in box " + skeletonTransform.position + "/" + extents + "/" + skeletonTransform.eulerAngles, skeletonTransform);
                foreach (Collider other in others)
                {
                    //Debug.Log("overlap:" + other.name + "(child of " + name + ": " + other.transform.IsChildOf(transform) + ")", other);
                    if(!other.transform.IsChildOf(transform))
                        continue;

                    ChunkNode chunk = other.GetComponent<ChunkNode>();
                    if(chunk == null)
                        continue;

                    chunk.isAnchor = true;
                    Vector3 localChunkPos = skeletonTransform.InverseTransformPoint(chunk.transform.position) - centre;
                    if (Mathf.Abs(localChunkPos.x) < indestructibleExtents.x
                        && Mathf.Abs(localChunkPos.y) < indestructibleExtents.y
                        && Mathf.Abs(localChunkPos.z) < indestructibleExtents.z)
                    {
                        chunk.indestructible = true;
                    }
                    
                    //Debug.Log("anchored chunk " + chunk, chunk);
                }
            }
            
            //de-activate fractured gameobjects
            for (int i = 0; i < chunkParents.Length; i++)
            {
                chunkParents[i].gameObject.SetActive(wasChunkParentActive[i]);
            }
            
            yield return null;
        }

        public void ClearNodes()
        {
            nodes.Clear();
        }

        public void AddNode(GraphNode node)
        {
            nodes.Add(node);
        }

        public void AddNodes(IEnumerable<GraphNode> nodes)
        {
            this.nodes.AddRange(nodes);
        }

        private void FixedUpdate()
        {
            if (graphChanged)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    DisconnectOrphans();
                }

                graphChanged = false;
            }
        }
        
        public GraphNode GetClosestNodeTo(Vector3 point)
        {
            GraphNode closest = null;
            float closestSqDist = float.PositiveInfinity;
            foreach (GraphNode node in nodes)
            {
                float sqDist = (node.transform.position - point).sqrMagnitude;
                if (sqDist < closestSqDist)
                {
                    closestSqDist = sqDist;
                    closest = node;
                }
            }
            return closest;
        }
        
        /// <summary>
        /// Searches through the graph and disconnects any nodes not connected to anchors
        /// </summary>
        public void DisconnectOrphans()
        {
            if(!PhotonNetwork.IsMasterClient)
                return;
            
            var anchors = nodes.Where(n => n.isAnchor).ToList();
            
            ISet<GraphNode> connected = new HashSet<GraphNode>(); //connected to anchor
            foreach (GraphNode anchor in anchors)
            {
                Traverse(anchor, connected);
            }

            var disconnectedNodes = nodes.Where(x => !connected.Contains(x)).ToList();
            foreach (GraphNode node in disconnectedNodes)
            {
                node.Unfreeze();
            }
        }

        private void Traverse(GraphNode curr, ISet<GraphNode> visited)
        {
            if(visited.Contains(curr))
                return;

            visited.Add(curr);
            foreach (GraphNode neighbour in curr.neighbours)
            {
                Traverse(neighbour, visited);
            }
        }

        private void OnNodeBreakOff(GraphNode node)
        {
            nodes.Remove(node);
            node.GetComponent<MeshRenderer>().enabled = true;
            
            // if(!graphChanged)
            //     Debug.Log("łoła", this);
            graphChanged = true;
        }
    }
}
