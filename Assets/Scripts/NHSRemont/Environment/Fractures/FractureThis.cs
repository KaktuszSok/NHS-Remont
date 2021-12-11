using System;
using System.Collections.Generic;
using NHSRemont.Gameplay;
using NHSRemont.Networking;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace NHSRemont.Environment.Fractures
{ 
    public class FractureThis : MonoBehaviourPunCallbacks, IPunObservable
    {
        /// <summary>
        /// The state of a loose or destroyed chunk
        /// </summary>
        [Serializable]
        public struct ChunkState
        {
            public bool destroyed;
            public NetworkedPhysicsState physicsState;

            public void Send(PhotonStream stream)
            {
                stream.SendNext(destroyed);
                if(!destroyed)
                    physicsState.Send(stream);
            }

            public void Receive(PhotonStream stream)
            {
                destroyed = stream.ReceiveNext<bool>();
                if(!destroyed)
                    physicsState.Receive(stream);
            }

            public void Apply(ChunkNode chunk, float lag)
            {
                if (destroyed)
                {
                    if (chunk != null)
                    {
                        Destroy(chunk.gameObject);
                    }
                    return;
                }

                if (chunk.frozen)
                {
                    chunk.Unfreeze();
                }

                chunk.syncedRb.ReceivePhysicsState(physicsState, lag);
            }
        }
        
        [SerializeField] public Anchor anchor = Anchor.Bottom;
        [SerializeField] private int chunks = 15;
        [SerializeField] private float density = 2400;
        [Tooltip("How much impulse per unit mass can chunks withstand before being broken off?")]
        [SerializeField] private float internalStrength = 3;
            
        [SerializeField] private Material outsideMaterial;
        [SerializeField] private Material insideMaterial;

        [SerializeField]
        private PhysicsManager.PhysObjectType fragmentsCategory = PhysicsManager.PhysObjectType.DEBRIS_MEDIUM;

        [Tooltip("If true, this object will not be merged into a combined fracture graph")]
        public bool independent = true;
        
        //runtime
        private new Collider collider;
        public float mass { get; private set; }
        [SerializeField, HideInInspector]
        private ChunkGraphManager chunksGraph;
        public ChunkNode[] allChunks;
        public bool fractured { get; private set; }
        private FractureThis combinedFracture = null; //"this" if independent
        private readonly List<FractureThis> mergedChildren = new();
        
        //network-only
        public ChunkState[] chunkStates;

        private void Awake()
        {
            if(!enabled)
                return;
            
            collider = GetComponent<Collider>();
        }

        private void Start()
        {
            if (photonView == null)
            {
                Destroy(this);
                return;
            }

            //combine child graphs
            if (independent)
            {
                Combine();
                photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
                PhysicsManager.instance.onExplosion += OnExplosion;
            }

            mass = 0f;
            for (int i = 0; i < allChunks.Length; i++)
            {
                mass += allChunks[i].mass;
            }
            
            chunksGraph.gameObject.SetActive(false);
        }
        
        private void Combine()
        {
            List<ChunkNode> combinedChunks = new();
            mergedChildren.Clear();
            List<ChunkGraphManager> mergedGraphs = new();
            if(collider != null && collider.enabled)
                AddGraph(this);
            CombineChildGraphs(transform);
            if (mergedChildren.Count > 0)
                RecalculateCombinedMesh();
            combinedFracture = this;

            void CombineChildGraphs(Transform parent)
            {
                foreach (Transform child in parent)
                {
                    FractureThis fracture = child.GetComponent<FractureThis>();
                    if (fracture == null || !fracture.enabled)
                    {
                        CombineChildGraphs(child);
                        continue;
                    }
                    if (fracture.independent) continue;
                    if (fracture.insideMaterial != insideMaterial || fracture.outsideMaterial != outsideMaterial) continue;

                    fracture.combinedFracture = this;
                    mergedChildren.Add(fracture);
                    AddGraph(fracture);
                    CombineChildGraphs(child);
                    fracture.enabled = false;
                }
            }
            void AddGraph(FractureThis fracture)
            {
                if (fracture.chunksGraph != null)
                {
                    combinedChunks.AddRange(fracture.chunksGraph.GetAllNodes());
                    mergedGraphs.Add(fracture.chunksGraph);
                }
            }
            void RecalculateCombinedMesh()
            {
                List<MeshFilter> meshes = new List<MeshFilter>();
                MeshFilter thisMesh = GetComponent<MeshFilter>();
                if(thisMesh && GetComponent<MeshRenderer>().enabled)
                    meshes.Add(thisMesh);
                int totalVerts = 0;
                for (int i = 0; i < mergedChildren.Count; i++)
                {
                    MeshFilter mesh = mergedChildren[i].GetComponent<MeshFilter>();
                    if (mesh)
                    {
                        meshes.Add(mesh);
                        totalVerts += mesh.sharedMesh.vertexCount;
                    }
                }

                MeshRenderer combinedRend = gameObject.GetOrAddComponent<MeshRenderer>();
                MeshFilter combinedFilter = gameObject.GetOrAddComponent<MeshFilter>();
                Mesh combinedMesh = new Mesh();
                if(totalVerts > ushort.MaxValue)
                    combinedMesh.indexFormat = IndexFormat.UInt32;
                int submeshesCount = 0;

                if (meshes.Count > 0)
                {
                    submeshesCount = meshes[0].sharedMesh.subMeshCount;
                }
                CombineInstance[][] instances = new CombineInstance[submeshesCount][];
                CombineInstance[] submeshes = new CombineInstance[submeshesCount];
                for (int sub = 0; sub < submeshesCount; sub++)
                {
                    instances[sub] = new CombineInstance[meshes.Count];
                    for (int i = 0; i < meshes.Count; i++)
                    {
                        Mesh unfracturedMesh = meshes[i].sharedMesh;
                        instances[sub][i] = new CombineInstance
                        {
                            mesh = unfracturedMesh,
                            transform = meshes[i].transform.localToWorldMatrix,
                            subMeshIndex = sub
                        };
                    
                        if(sub > 0) continue; //only do these once per unfractured mesh:
                        
                        MeshRenderer rend = meshes[i].GetComponent<MeshRenderer>();
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
                combinedMesh.CombineMeshes(submeshes, false);
                combinedMesh.Optimize();
                combinedMesh.RecalculateBounds();
                combinedFilter.sharedMesh = combinedMesh;
                combinedRend.enabled = true;
            }

            if (mergedChildren.Count > 0)
            {
                //create combined graph
                chunksGraph = new GameObject(gameObject.name + " Combined Fractured").AddComponent<ChunkGraphManager>();
                chunksGraph.transform.SetParent(transform.parent, false);
                //reparent chunks
                foreach (ChunkNode chunk in combinedChunks)
                {
                    chunk.transform.SetParent(chunksGraph.transform, true);
                }
                chunksGraph.Setup(combinedChunks);
                //destroy child graph managers
                for (int i = 0; i < mergedGraphs.Count; i++)
                {
                    Destroy(mergedGraphs[i].gameObject);
                }
            }
            
            allChunks = combinedChunks.ToArray();
            chunks = allChunks.Length;
            chunkStates = new ChunkState[allChunks.Length];
        }

        public void OnDestroy()
        {
            PhysicsManager.instance.onExplosion -= OnExplosion;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if(!PhotonNetwork.IsMasterClient || fractured)
                return;
            
            float chunkMass = mass / chunks;
            float impulseToFracture = chunkMass * internalStrength;

            if (collision.impulse.sqrMagnitude >= impulseToFracture*impulseToFracture)
            {
                Vector3 point = collision.GetContact(0).point;
                Fracture();
                combinedFracture.chunksGraph.GetClosestNodeTo(point).OnCollisionEnter(collision);
            }
        }

        private void OnExplosion(ExplosionInfo explosionInfo)
        {
            if(!independent || !PhotonNetwork.IsMasterClient || fractured || collider == null)
                return;

            float chunkMass = mass / chunks;
            float impulseToFracture = chunkMass * internalStrength;
            (Vector3 impulse, Vector3 _) =
                explosionInfo.CalculateImpulseAndPoint(transform, collider, chunkMass);
            if (impulse.sqrMagnitude < impulseToFracture * impulseToFracture)
                return;

            Fracture();
            combinedFracture.chunksGraph.OnExplosion(explosionInfo);
        }

        public void Fracture()
        {
            if(fractured) return;

            fractured = true;
            foreach (Collider component in GetComponents<Collider>())
                component.enabled = false;
            PhysicsManager.instance.onExplosion -= OnExplosion;
            
            if (!independent)
            {
                combinedFracture.Fracture();
                return;
            }
            for (var i = 0; i < mergedChildren.Count; i++)
            {
                mergedChildren[i].Fracture(); //disable collision for all children
            }
            
            GetComponent<Renderer>().enabled = false;
            
            chunksGraph.gameObject.SetActive(true);
            chunksGraph.InitialiseRuntimeFromPrecalculated();
        }

        public ChunkGraphManager PrepareFracture()
        {
            MeshRenderer rend = GetComponent<MeshRenderer>();
            if (rend == null || !rend.enabled) //most likely a combined graph that will be generated at runtime from children
                return null;
            
            AutoDetectMaterials();

            int seed = new Random().Next();
            ChunkGraphManager fracture = Fracturing.FractureGameObject(
                gameObject,
                anchor,
                seed,
                chunks,
                insideMaterial,
                outsideMaterial,
                internalStrength,
                density,
                fragmentsCategory
            );
            fracture.transform.SetParent(transform.parent);
            fracture.gameObject.SetActive(false);
            chunksGraph = fracture;

            return fracture;
        }

        private void AutoDetectMaterials()
        {
            if (outsideMaterial == null)
            {
                outsideMaterial = GetComponent<Renderer>()?.sharedMaterial;
            }

            if (insideMaterial == null)
            {
                Renderer rend = GetComponent<Renderer>();
                if (!rend)
                {
                    insideMaterial = outsideMaterial;
                }
                else
                {
                    var mats = rend.sharedMaterials;
                    insideMaterial = mats.Length > 1 ? mats[1] : mats[0];
                }
            }
        }

        public void SetMaterials(Material inside, Material outside)
        {
            insideMaterial = inside;
            outsideMaterial = outside;
        }
        
        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (newMasterClient.IsLocal)
            {
                foreach (ChunkNode chunkNode in allChunks)
                {
                    if (chunkNode != null && !chunkNode.frozen)
                    {
                        chunkNode.syncedRb.isSimulatedLocally = true;
                    }
                }
            }
        }


        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (!enabled)
                return;

            if (stream.IsWriting)
            {
                if (!fractured)
                {
                    return;
                }

                //write states of all chunks
                for (int i = 0; i < chunkStates.Length; i++)
                {
                    ChunkState prevState = chunkStates[i];
                    if (prevState.destroyed)
                    {
                        stream.SendNext(true); //true = this chunk is loose or destroyed
                        prevState.Send(stream);
                        continue;
                    }

                    ChunkNode chunk = allChunks[i];
                    if (chunk == null) //chunk got destroyed
                    {
                        chunkStates[i].destroyed = true;
                        stream.SendNext(true); //true = this chunk is loose or destroyed
                        chunkStates[i].Send(stream);
                        continue;
                    }

                    if (chunk.frozen) //don't send frozen chunk state
                    {
                        stream.SendNext(false); //false = this chunk is not loose and not destroyed
                        continue;
                    }

                    if (chunkStates[i].physicsState.velocity == Vector3.zero
                        && chunkStates[i].physicsState.angularVelocity == Vector3.zero
                        && allChunks[i].syncedRb.rb.IsSleeping())
                    {
                        //send old state, no need to re-encode it as the rb is sleeping
                        stream.SendNext(true); //true = this chunk is loose or destroyed
                        chunkStates[i].Send(stream);
                        continue;
                    }

                    //otherwise, encode state and send it
                    chunkStates[i].physicsState.From(allChunks[i].syncedRb.rb);
                    stream.SendNext(true); //true = this chunk is loose or destroyed
                    chunkStates[i].Send(stream);
                }
                
                Debug.Log(name + " stream sent length: " + stream.Count, this);
            }

            if (stream.IsReading)
            {
                if (!fractured)
                    Fracture();

                float lag = (float) (PhotonNetwork.Time - info.SentServerTime);
                Debug.Log(name + " stream received length: " + stream.Count + " (lag = " + lag + " seconds)", this);

                for (int i = 0; i < chunkStates.Length; i++)
                {
                    bool isChunkLooseOrDestroyed = stream.ReceiveNext<bool>();
                    if (!isChunkLooseOrDestroyed)
                        continue;

                    chunkStates[i].Receive(stream);
                    chunkStates[i].Apply(allChunks[i], lag);
                }
            }
        }

        /// <summary>
        /// Copies the component to a specified gameobject
        /// </summary>
        /// <param name="source">The component to copy</param>
        /// <param name="target">The gameobject to copy it to</param>
        /// <param name="scaleFactor">The relative difference in scale of the target compared to the source. 0.5f,0.5f,0.5f means an object half as big in each direction.</param>
        public static void CopyToSimilarObject(FractureThis source, GameObject target, Vector3 scaleFactor, bool independent = true)
        {
            CopyToSimilarObject(source, target, scaleFactor, source.anchor, independent);
        }
        
        /// <summary>
        /// Copies the component to a specified gameobject
        /// </summary>
        /// <param name="source">The component to copy</param>
        /// <param name="target">The gameobject to copy it to</param>
        /// <param name="scaleFactor">The relative difference in scale of the target compared to the source. 0.5f,0.5f,0.5f means an object half as big in each direction.</param>
        /// <param name="newAnchors">The anchors that the target will have</param>
        public static void CopyToSimilarObject(FractureThis source, GameObject target, Vector3 scaleFactor, Anchor newAnchors, bool independent = true)
        {
            FractureThis copy = target.AddComponent<FractureThis>();
            copy.anchor = newAnchors;
            copy.chunks = Mathf.Max(2, Mathf.RoundToInt(source.chunks * scaleFactor.x * scaleFactor.y * scaleFactor.z));
            copy.density = source.density;
            copy.internalStrength = source.internalStrength;
            copy.insideMaterial = source.insideMaterial;
            copy.outsideMaterial = source.outsideMaterial;
            copy.fragmentsCategory = source.fragmentsCategory;
            copy.independent = independent;

            if(independent)
            {
                target.AddComponent<PhotonView>().Synchronization = ViewSynchronization.ReliableDeltaCompressed;
            }
        }
    }
}