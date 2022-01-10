using System;
using NHSRemont.Utility;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem
{
    public class DroppedItemSpawnPoint : MonoBehaviour
    {
        [Serializable]
        private struct Spawnable
        {
            public Item type;
            public int amount;

            public Item SpawnInWorld(Vector3 where, Quaternion rotation)
            {
                Item item = Item.CreateInstance(type, where, rotation, true, true);
                item.amount = amount;
                return item;
            }
        }
        [SerializeField] private Spawnable[] spawnables = Array.Empty<Spawnable>();
        public float timeBetweenSpawns = 90f;

        private float spawnTimer = 0f;

        private void Update()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                if (spawnTimer <= 0f)
                {
                    Spawn();
                }
                else
                {
                    spawnTimer -= Time.deltaTime;
                }
            }
        }

        public void Spawn()
        {
            Spawnable chosen = spawnables.ChooseRandom();
            Item spawned = chosen.SpawnInWorld(transform.position, transform.rotation);
            spawned.despawnTime = PhotonNetwork.Time + timeBetweenSpawns;            
            spawnTimer = timeBetweenSpawns;
        }
    }
}
