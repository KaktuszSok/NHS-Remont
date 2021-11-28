using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using NHSRemont.Environment.Fractures;
using NHSRemont.Environment.Terrain;
using NHSRemont.Gameplay;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NHSRemont.Networking
{
    [Serializable]
    public class MapPersistence
    {
        public const byte typeId = 255;
        
        [Serializable]
        public readonly struct FracturedObjectState
        {
            private readonly List<(int, FractureThis.ChunkState)> chunkStates;

            public FracturedObjectState(FractureThis reference)
            {
                chunkStates = new List<(int, FractureThis.ChunkState)>();
                var allChunks = reference.allChunks;
                var allChunkStates = reference.chunkStates;

                for (int i = 0; i < allChunks.Length; i++)
                {
                    if (allChunkStates[i].destroyed)
                    {
                        chunkStates.Add((i, allChunkStates[i]));
                        continue;
                    }
                    
                    if(allChunks[i].frozen)
                        continue;
                    
                    chunkStates.Add((i, allChunkStates[i]));
                }
            }

            /// <summary>
            /// Use this to apply a saved fracture state to a non-fractured object.
            /// </summary>
            public void Apply(FractureThis target)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    if(!target.fractured)
                        target.Fracture();

                    foreach ((int i, FractureThis.ChunkState state) in chunkStates)
                    {
                        state.Apply(target.allChunks[i], 0f);
                    }
                }
            }
        }
        
        //network serialised:
        public readonly List<ITerrainEvent> terrainEventsHistory = new();
        
        //save only - not networked:
        private readonly Dictionary<int, FracturedObjectState> fracturedObjects = new();
        private readonly Dictionary<int, NetworkedPhysicsState> physicsStates = new();

        /// <summary>
        /// For saving game state.
        /// Takes a "snapshot" of the state of the currently loaded scene and saves it in this object.
        /// </summary>
        public void PersistLoadedSceneState()
        {
            fracturedObjects.Clear();
            foreach (FractureThis fractureThis in Object.FindObjectsOfType<FractureThis>(true))
            {
                if (fractureThis.fractured)
                {
                    int id = fractureThis.photonView.sceneViewId;
                    if(id == 0)
                        continue;
                    fracturedObjects[fractureThis.photonView.sceneViewId] = new FracturedObjectState(fractureThis);
                }
            }
            
            physicsStates.Clear();
            foreach (Rigidbody rb in PhysicsManager.instance.GetAllRigidbodies())
            {
                PhotonView photonView = rb.GetComponent<PhotonView>();
                if(photonView == null || rb.CompareTag("Player")) //TODO store players separately by some UUID thing
                    continue;

                int id = photonView.sceneViewId;
                if(id == 0)
                    continue;
                physicsStates.Add(photonView.sceneViewId, new NetworkedPhysicsState().From(rb));
            }
        }

        /// <summary>
        /// For loading game state from save.
        /// Applies the state of all NetworkObjects to the loaded scene.
        /// This does not do more advanced synchronisation such as applying the explosion history to terrains. For that, check GameManager.
        /// </summary>
        public void ApplyLoadedSceneState()
        {
            foreach (var entry in fracturedObjects)
            {
                PhotonView photonView = PhotonView.Find(entry.Key);
                if (photonView != null)
                {
                    entry.Value.Apply(photonView.GetComponent<FractureThis>());
                }
                else
                {
                    Debug.LogWarning("Could not get FractureThis object with ID " + entry.Key);
                }
            }

            foreach (var entry in physicsStates)
            {
                PhotonView photonView = PhotonView.Find(entry.Key);
                if (photonView != null)
                {
                    entry.Value.To(photonView.GetComponent<Rigidbody>());
                }
                else
                {
                    Debug.LogWarning("Could not get physics object at " + entry.Value.position + " with ID " + entry.Key);
                }
            }
        }
        
        public static short Serialise(StreamBuffer outStream, object obj)
        {
            short written = 0;
            
            MapPersistence persistence = (MapPersistence)obj;
            byte[] int1 = new byte[sizeof(int)];
            int offset = 0;

            int terrainEventsCount = persistence.terrainEventsHistory.Count;
            Protocol.Serialize(terrainEventsCount, int1, ref offset);
            outStream.Write(int1, 0, sizeof(int));
            written += sizeof(int);

            for (int i = 0; i < terrainEventsCount; i++)
            {
                ITerrainEvent terrainEvent = persistence.terrainEventsHistory[i];
                
                offset = 0;
                Protocol.Serialize(terrainEvent.GetEventTypeId(), int1, ref offset);
                outStream.Write(int1, 0, sizeof(int));
                written += sizeof(int);

                written += terrainEvent.Serialise(outStream);
            }

            return written;
        }

        public static object Deserialise(StreamBuffer inStream, short length)
        {
            MapPersistence persistence = new MapPersistence();
            byte[] int1 = new byte[sizeof(int)];

            inStream.Read(int1, 0, sizeof(int));
            int offset = 0;
            Protocol.Deserialize(out int terrainEventsCount, int1, ref offset);

            persistence.terrainEventsHistory.Capacity = terrainEventsCount;
            for (int i = 0; i < terrainEventsCount; i++)
            {
                inStream.Read(int1, 0, sizeof(int));
                offset = 0;
                Protocol.Deserialize(out int id, int1, ref offset);

                switch (id)
                {
                    case 0: //Explosion
                        TerrainExplosionEvent explosionEvent = new TerrainExplosionEvent();
                        explosionEvent.Deserialise(inStream);
                        persistence.terrainEventsHistory.Add(explosionEvent);
                        break;
                    case 1: //Edit
                        TerrainEdit editEvent = new TerrainEdit();
                        editEvent.Deserialise(inStream);
                        persistence.terrainEventsHistory.Add(editEvent);
                        break;
                }
            }

            return persistence;
        }
    }
}