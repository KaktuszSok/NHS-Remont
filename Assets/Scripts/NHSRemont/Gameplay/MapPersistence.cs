using System.Collections.Generic;
using NHSRemont.Environment.Fractures;
using NHSRemont.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace NHSRemont.Networking
{
    public class MapPersistence : INetworkSerializable
    {
        private readonly struct FracturedObjectState
        {
            private readonly List<int> looseOrDestroyedChunkIndices;
            private readonly List<FractureThis.ChunkState> chunkStates;

            public FracturedObjectState(FractureThis reference)
            {
                looseOrDestroyedChunkIndices = new List<int>();
                chunkStates = new List<FractureThis.ChunkState>();
                var allChunks = reference.allChunks;
                var allChunkStates = reference.chunkStates;

                for (int i = 0; i < allChunks.Length; i++)
                {
                    if (allChunkStates[i].destroyed)
                    {
                        looseOrDestroyedChunkIndices.Add(i);
                        chunkStates.Add(allChunkStates[i]);
                        continue;
                    }
                    
                    if(allChunks[i].frozen)
                        continue;
                    
                    looseOrDestroyedChunkIndices.Add(i);
                    chunkStates.Add(allChunkStates[i]);
                }
            }

            public void Apply(FractureThis target)
            {
                target.fractured.Value = true;

                target.ChunkStatesChangedClientRpc(looseOrDestroyedChunkIndices.ToArray(), chunkStates.ToArray(), default);
            }
        }
        
        //network serialised:
        public readonly List<ExplosionInfo> explosionsHistory = new List<ExplosionInfo>();
        
        //save only - not networked:
        private readonly Dictionary<NetworkObjectReference, FracturedObjectState> fracturedObjects = new();
        private readonly Dictionary<NetworkObjectReference, NetworkedPhysicsState> physicsStates = new();

        /// <summary>
        /// Takes a "snapshot" of the state of the currently loaded scene and saves it in this object.
        /// </summary>
        public void PersistLoadedSceneState()
        {
            fracturedObjects.Clear();
            foreach (FractureThis fractureThis in Object.FindObjectsOfType<FractureThis>(true))
            {
                if (fractureThis.fractured.Value)
                {
                    fracturedObjects.Add(fractureThis.NetworkObject, new FracturedObjectState(fractureThis));
                }
            }
            
            Debug.Log("persisted " + fracturedObjects.Count + " fractured objects");

            physicsStates.Clear();
            foreach (Rigidbody rb in PhysicsManager.instance.GetAllRigidbodies())
            {
                NetworkObject networkedObj = rb.GetComponent<NetworkObject>();
                if(networkedObj == null || networkedObj.IsPlayerObject) //TODO store players separately by some UUID thing
                    continue;
                
                NetworkObjectReference reference = new NetworkObjectReference(networkedObj);
                physicsStates.Add(reference, new NetworkedPhysicsState().From(rb));
            }

            Debug.Log("persisted " + physicsStates.Count + " physics objects");
        }

        /// <summary>
        /// Applies the state of all NetworkObjects to the loaded scene.
        /// This does not do more advanced synchronisation such as applying the explosion history to terrains. For that, check GameManager.
        /// </summary>
        public void ApplyLoadedSceneState()
        {
            Debug.Log("trying to apply fractured state to " + fracturedObjects.Count + " objects");
            foreach (var entry in fracturedObjects)
            {
                if (entry.Key.TryGet(out NetworkObject networkObject))
                {
                    entry.Value.Apply(networkObject.GetComponent<FractureThis>());
                }
                else
                {
                    Debug.LogWarning("Could not get FractureThis object with ID " + entry.Key.NetworkObjectId);
                }
            }

            foreach (var networkedPhysicsState in physicsStates)
            {
                if (networkedPhysicsState.Key.TryGet(out NetworkObject networkObject))
                {
                    networkedPhysicsState.Value.To(networkObject.GetComponent<Rigidbody>());
                }
                else
                {
                    Debug.LogWarning("Could not get physics object at " + networkedPhysicsState.Value.position);
                }
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            SerialiseExplosions(serializer);
        }

        private void SerialiseExplosions<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int explosionsCount = 0;
            if (serializer.IsWriter)
            {
                explosionsCount = explosionsHistory.Count;
            }
            serializer.SerializeValue(ref explosionsCount);

            if (serializer.IsReader)
            {
                explosionsHistory.Clear();
                explosionsHistory.Capacity = explosionsCount;
            }

            for (int i = 0; i < explosionsCount; i++)
            {
                ExplosionInfo explosion;
                if (serializer.IsWriter)
                {
                    explosion = explosionsHistory[i];
                }
                else
                {
                    explosion = new ExplosionInfo();
                }
                serializer.SerializeValue(ref explosion);

                if (serializer.IsReader)
                {
                    explosionsHistory.Add(explosion);
                }
            }
        }
    }
}