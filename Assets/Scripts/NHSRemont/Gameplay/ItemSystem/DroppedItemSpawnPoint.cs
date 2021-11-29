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
            public ItemType type;
            public int amount;

            public ItemStack CreateStack()
            {
                return new ItemStack(type, amount);
            }
        }
        [SerializeField] private Spawnable[] spawnables = Array.Empty<Spawnable>();
        public float timeBetweenSpawns = 90f;

        private DroppedItemStack currentDropped;
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
            if (currentDropped != null)
            {
                PhotonNetwork.Destroy(currentDropped.gameObject);
            }
        
            Spawnable chosen = spawnables.ChooseRandom();
            ItemStack stack = chosen.CreateStack();
            DroppedItemStack.CreateFromStack(stack, transform.position);
        
            spawnTimer = timeBetweenSpawns;
        }
    }
}
