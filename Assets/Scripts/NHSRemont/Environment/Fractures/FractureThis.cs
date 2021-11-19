using System.Collections.Generic;
using NHSRemont.Gameplay;
using NHSRemont.Networking;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;

namespace NHSRemont.Environment.Fractures
{
    [RequireComponent(typeof(NetworkObject))]
    public class FractureThis : NetworkBehaviour
    {
        public struct ChunkState
        {
            public bool destroyed;
            public NetworkedPhysicsState physicsState;
        }
        
        [SerializeField] private Anchor anchor = Anchor.Bottom;
        [SerializeField] private int chunks = 15;
        [SerializeField] private float density = 2400;
        [Tooltip("How much impulse per unit mass can chunks withstand before being broken off?")]
        [SerializeField] private float internalStrength = 3;
            
        [SerializeField] private Material outsideMaterial;
        [SerializeField] private Material insideMaterial;

        [SerializeField]
        private PhysicsManager.PhysObjectType fragmentsCategory = PhysicsManager.PhysObjectType.DEBRIS_MEDIUM;
        
        //runtime
        private new Collider collider;
        private float mass;
        [SerializeField, HideInInspector]
        private ChunkGraphManager chunksGraph;
        public ChunkNode[] allChunks;
        public readonly NetworkVariable<bool> fractured = new();
        
        //network-only
        private bool isServer;
        public ChunkState[] chunkStates;

        private void Awake()
        {
            if(NetworkManager.Singleton == null)
                return;

            isServer = IsServer;
            collider = GetComponent<Collider>();
            Vector3 scale = transform.lossyScale;
            float volume = GetComponent<MeshFilter>().sharedMesh.Volume() * scale.x * scale.y * scale.z;
            mass = volume * density;
            
            allChunks = chunksGraph.GetComponentsInChildren<ChunkNode>(true);
            chunkStates = new ChunkState[allChunks.Length];
            fractured.OnValueChanged += OnFracturedChanged;
            
            chunksGraph.gameObject.SetActive(true);
            chunksGraph.gameObject.SetActive(false);
        }

        private void Start()
        {
            if(isServer)
                PhysicsManager.instance.onExplosion += OnExplosion;
        }

        private void FixedUpdate()
        {
            if (isServer && fractured.Value)
            {
                List<int> changedIndices = new List<int>();
                List<ChunkState> changedStates = new List<ChunkState>();
                for (var i = 0; i < allChunks.Length; i++)
                {
                    ChunkState prevState = chunkStates[i];
                    if(prevState.destroyed)
                        continue;
                    
                    ChunkNode chunk = allChunks[i];
                    if (chunk == null) //chunk got destroyed
                    {
                        chunkStates[i].destroyed = true;
                        changedIndices.Add(i);
                        changedStates.Add(chunkStates[i]);
                        continue;
                    }

                    if (chunk.frozen)
                    {
                        continue;
                    }

                    if (chunkStates[i].physicsState.velocity == Vector3.zero && allChunks[i].rb.velocity == Vector3.zero
                    && chunkStates[i].physicsState.angularVelocity == Vector3.zero && allChunks[i].rb.angularVelocity == Vector3.zero)
                    {
                        continue;
                    }

                    chunkStates[i].physicsState.From(allChunks[i].rb);
                    changedIndices.Add(i);
                    changedStates.Add(chunkStates[i]);
                }

                if (changedIndices.Count > 0)
                {
                    ChunkStatesChangedClientRpc(changedIndices.ToArray(), changedStates.ToArray(), NetworkingUtils.SendToAllButServer());
                }
            }
        }

        [ClientRpc]
        public void ChunkStatesChangedClientRpc(int[] indices, ChunkState[] newStates, ClientRpcParams parameters)
        {
            for (var updateIdx = 0; updateIdx < indices.Length; updateIdx++)
            {
                int chunkIdx = indices[updateIdx];
                if (newStates[updateIdx].destroyed)
                {
                    Destroy(allChunks[chunkIdx].gameObject);
                    continue;
                }

                if (allChunks[chunkIdx].frozen)
                {
                    allChunks[chunkIdx].Unfreeze();
                }
                
                newStates[updateIdx].physicsState.To(allChunks[chunkIdx].rb);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if(isServer)
                PhysicsManager.instance.onExplosion -= OnExplosion;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float chunkMass = mass / chunks;
            float impulseToFracture = chunkMass * internalStrength;

            if (collision.impulse.sqrMagnitude >= impulseToFracture*impulseToFracture)
            {
                Vector3 point = collision.GetContact(0).point;
                Fracture();
                chunksGraph.GetClosestNodeTo(point).OnCollisionEnter(collision);
            }
        }

        private void OnExplosion(ExplosionInfo explosionInfo)
        {
            float chunkMass = mass / chunks;
            float impulseToFracture = chunkMass * internalStrength;
            (Vector3 impulse, Vector3 point) =
                explosionInfo.CalculateImpulseAndPoint(transform, collider.bounds, chunkMass);
            if (impulse.sqrMagnitude < impulseToFracture * impulseToFracture)
                return;

            Fracture();
            chunksGraph.OnExplosion(explosionInfo);
        }

        public void Fracture()
        {
            if(!isServer)
                return;
            
            fractured.Value = true;
        }

        private void OnFracturedChanged(bool oldValue, bool newValue)
        {
            if(oldValue == true)
                return;
            if (newValue == false)
            {
                Debug.LogWarning("Should not un-fracture a gameobject! Ignoring.");
                return;
            }

            foreach (Collider component in GetComponents<Collider>())
                component.enabled = false;
            GetComponent<Renderer>().enabled = false;
            
            chunksGraph.gameObject.SetActive(true);
            chunksGraph.InitialiseRuntimeFromPrecalculated();
            if (isServer)
            {
                PhysicsManager.instance.onExplosion -= OnExplosion;
            }
        }

        public ChunkGraphManager PrepareFracture()
        {
            if (outsideMaterial == null)
                outsideMaterial = GetComponentInChildren<Renderer>().sharedMaterial;
            if (insideMaterial == null)
                insideMaterial = GetComponentInChildren<Renderer>().sharedMaterial;
            
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
    }
}