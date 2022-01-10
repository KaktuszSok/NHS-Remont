using System.Collections.Generic;
using NHSRemont.Gameplay;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace NHSRemont.Environment.Fractures
{
    /// <summary>
    /// A part of a structure which can be fractured into chunks
    /// </summary>
    [RequireComponent(typeof(NHSWall))]
    public class Fracturable : MonoBehaviour, IDamageListener
    {
        [SerializeField] private int chunks = 15;
        private NHSWall wallComponent;
        public WallMaterial material {
            get
            {
                if (wallComponent == null)
                    wallComponent = GetComponent<NHSWall>();
            
                return wallComponent.material;
            }
        }

        [SerializeField]
        private PhysicsManager.PhysObjectType fragmentsCategory = PhysicsManager.PhysObjectType.DEBRIS_MEDIUM;
        
        [Tooltip("If true, this object will not be merged into a combined fracture graph")]
        public bool independent = true;
        
        //runtime
        private new Collider collider;
        public float mass { get; private set; }
        [SerializeField, HideInInspector]
        private FracturedRenderer chunksParent;
        public ChunkNode[] allChunks;
        public bool fractured { get; private set; }
        private Fracturable combinedFracturable = null; //"this" if independent
        private readonly List<Fracturable> mergedChildren = new();
        
        private void Awake()
        {
            collider = GetComponent<Collider>();
            wallComponent = GetComponent<NHSWall>();
            if (independent)
            {
                //combine child graphs
                Combine();
            }

            mass = 0f;
            for (int i = 0; i < allChunks.Length; i++)
            {
                mass += allChunks[i].mass;
                if (independent)
                {
                    allChunks[i].breakOffCallbackEarly += OnChunkBreakOff;
                }
            }
            
            if (chunksParent == null)
            {
                return;
            }
            
            chunksParent.gameObject.SetActive(false);
        }

        private void Start()
        {
            if(independent || collider)
                PhysicsManager.instance.onExplosion += OnExplosion;
        }

        private void Combine()
        {
            List<ChunkNode> combinedChunks = new();
            List<FracturedRenderer> mergedChunkParents = new();
            mergedChildren.Clear();
            if(collider != null && collider.enabled)
                AddFracturable(this);
            CombineChildren(transform);
            if (mergedChildren.Count > 0)
                RecalculateCombinedMesh();
            combinedFracturable = this;

            void CombineChildren(Transform parent)
            {
                foreach (Transform child in parent)
                {
                    Fracturable fracturable = child.GetComponent<Fracturable>();
                    if (fracturable == null || !fracturable.enabled)
                    {
                        CombineChildren(child);
                        continue;
                    }
                    if (fracturable.independent) continue;
                    if (fracturable.material != material) continue;

                    fracturable.combinedFracturable = this;
                    mergedChildren.Add(fracturable);
                    AddFracturable(fracturable);
                    CombineChildren(child);
                    fracturable.enabled = false;
                }
            }
            void AddFracturable(Fracturable fracturable)
            {
                if (fracturable.chunksParent != null)
                {
                    combinedChunks.AddRange(fracturable.chunksParent.chunks);
                    mergedChunkParents.Add(fracturable.chunksParent);
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
                chunksParent = new GameObject(gameObject.name + " Combined Fractured").AddComponent<FracturedRenderer>();
                chunksParent.transform.SetParent(transform.parent, false);
                //reparent chunks
                foreach (ChunkNode chunk in combinedChunks)
                {
                    chunk.transform.SetParent(chunksParent.transform, true);
                }
                chunksParent.Setup(combinedChunks);
                //destroy original chunk parents
                for (int i = 0; i < mergedChunkParents.Count; i++)
                {
                    Destroy(mergedChunkParents[i].gameObject);
                }
            }
            
            allChunks = combinedChunks.ToArray();
            chunks = allChunks.Length;
        }
        
        public void OnDestroy()
        {
            PhysicsManager.instance.onExplosion -= OnExplosion;
            foreach (ChunkNode chunkNode in allChunks)
            {
                chunkNode.breakOffCallbackEarly -= OnChunkBreakOff;
            }
        }

        public void OnCollisionEnter(Collision collision)
        {
            if(!PhotonNetwork.IsMasterClient || fractured)
                return;
            
            OnImpulseAtPoint(collision.impulse, collision.GetContact(0).point);
        }

        public void OnExplosion(ExplosionInfo explosionInfo)
        {
            if(!PhotonNetwork.IsMasterClient)
                return;

            if (!fractured && collider)
            {
                float chunkMass = mass / chunks;
                float impulseToFracture = material.density * material.internalStrength;
                (Vector3 impulse, _, float sqDist) =
                    explosionInfo.CalculateImpulseAndPoint(transform, collider, chunkMass);
                
                if (impulse.sqrMagnitude >= impulseToFracture * impulseToFracture
                    || explosionInfo.GetOverpressureAt(sqDist) > material.density*material.internalStrength/1000f)
                    Fracture();
                else
                    return;
            }

            if (independent)
            {
                //forward explosion to chunks
                foreach (ChunkNode chunkNode in allChunks)
                {
                    if (chunkNode != null)
                    {
                        chunkNode.OnExplosion(explosionInfo);
                    }
                }
            }
        }

        public void OnImpulseAtPoint(Vector3 impulse, Vector3 point)
        {
            if(combinedFracturable == null) //awake not called yet
                return;
            
            float impulseToFracture = material.density * material.internalStrength;
            
            if (impulse.sqrMagnitude >= impulseToFracture*impulseToFracture)
            {
                Fracture();
                combinedFracturable.GetClosestChunkTo(point).gameObject.ApplyImpulseAtPoint(impulse, point);
            }
        }

        public void OnBulletDamage(RaycastHit hit, float damage)
        {
            //TODO
        }

        private void OnChunkBreakOff(GraphNode obj)
        {
            if(!fractured)
                Fracture();
        }
        
        public ChunkNode GetClosestChunkTo(Vector3 point)
        {
            ChunkNode closest = null;
            float closestSqDist = float.PositiveInfinity;
            foreach (ChunkNode chunk in allChunks)
            {
                float sqDist = (chunk.transform.position - point).sqrMagnitude;
                if (sqDist < closestSqDist)
                {
                    closestSqDist = sqDist;
                    closest = chunk;
                }
            }
            return closest;
        }

        public void Fracture()
        {
            if(fractured) return;

            fractured = true;
            foreach (Collider component in GetComponents<Collider>())
                component.enabled = false;
            
            if (!independent)
            {
                combinedFracturable.Fracture();
                return;
            }
            
            foreach (ChunkNode chunkNode in allChunks)
            {
                chunkNode.breakOffCallbackEarly -= OnChunkBreakOff;
            }
            
            for (int i = 0; i < mergedChildren.Count; i++)
            {
                mergedChildren[i].Fracture(); //disable collision for all children
            }
            
            GetComponent<Renderer>().enabled = false;

            chunksParent.gameObject.SetActive(true);
            chunksParent.InitialiseRuntimeFromPrecalculated();
        }

        public FracturedRenderer PrepareFracture()
        {
            MeshRenderer rend = GetComponent<MeshRenderer>();
            if (rend == null || !rend.enabled) //most likely a combined graph that will be generated at runtime from children
                return null;

            material.AutoDetectMaterials(gameObject);

            int seed = new Random().Next();
            FracturedRenderer fracture = Fracturing.FractureGameObject(
                gameObject,
                seed,
                chunks,
                material,
                fragmentsCategory
            );
            foreach (ChunkNode chunk in fracture.chunks)
            {
                wallComponent.CopyTo(chunk.gameObject);
            }
            fracture.transform.SetParent(transform.parent);
            fracture.gameObject.SetActive(false);
            chunksParent = fracture;

            return fracture;
        }

        /// <summary>
        /// Copies the component to a specified gameobject
        /// </summary>
        /// <param name="source">The component to copy</param>
        /// <param name="target">The gameobject to copy it to</param>
        /// <param name="scaleFactor">The relative difference in scale of the target compared to the source. 0.5f,0.5f,0.5f means an object half as big in each direction.</param>
        /// <param name="independent">Should the copy be independent?</param>
        public static void CopyToSimilarObject(Fracturable source, GameObject target, Vector3 scaleFactor, bool independent = true)
        {
            Fracturable copy = target.AddComponent<Fracturable>();
            copy.chunks = Mathf.Max(2, Mathf.RoundToInt(source.chunks * scaleFactor.x * scaleFactor.y * scaleFactor.z));
            source.GetComponent<NHSWall>().CopyTo(target);
            copy.fragmentsCategory = source.fragmentsCategory;
            copy.independent = independent;

            if(independent)
            {
                target.AddComponent<PhotonView>().Synchronization = ViewSynchronization.ReliableDeltaCompressed;
            }
        }
    }
}