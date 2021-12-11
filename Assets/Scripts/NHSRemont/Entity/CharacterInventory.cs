using System;
using System.Collections.Generic;
using NHSRemont.Gameplay.ItemSystem;
using NHSRemont.Networking;
using Photon.Pun;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NHSRemont.Entity
{
    public class CharacterInventory : Inventory
    {
        private const int slotsCount = 9;
        private const double droppedItemsLifetime = 150;

        public int hotbarSlot { get; private set; }
        private Item heldItem;

        public Action<int> OnHotbarSlotSelected;

        private void Awake()
        {
            size = slotsCount;
        }

        protected override void Start()
        {
            base.Start();
            SelectSlot(0);
            onSlotContentsChanged += (item, idx) =>
            {
                if (idx == hotbarSlot)
                {
                    UpdateHeldItem(hotbarSlot, hotbarSlot);
                }
            };
        }

        public void SelectSlot(int slot)
        {
            slot = (int)Mathf.Repeat(slot, slotsCount);
            int oldSlot = hotbarSlot;
            hotbarSlot = slot;
            UpdateHeldItem(oldSlot, hotbarSlot);
            OnHotbarSlotSelected?.Invoke(slot);
        }

        public void ScrollThroughSlots(int delta)
        {
            SelectSlot(hotbarSlot+delta);
        }

        private void UpdateHeldItem(int oldSlot, int newSlot)
        {
            Item oldItem = slots[oldSlot];
            Item newItem = slots[newSlot];

            if(oldSlot != newSlot && oldItem == newItem) return;
            
            if(oldItem != null)
                oldItem.gameObject.SetActive(false);
            
            if(newItem != null)
                newItem.gameObject.SetActive(true);

            heldItem = newItem;
        }

        public override ICollection<Item> DropAllItems(bool individually = true)
        {
            var drops = base.DropAllItems(individually);
            foreach (Item drop in drops)
            {
                drop.transform.position += Random.insideUnitSphere * 0.025f;
            }

            return drops;
        }

        public override Item DropItemsInSlot(int slot, int amount = -1)
        {
            Item drop = base.DropItemsInSlot(slot, amount);
            if (drop)
            {
                drop.despawnTime = PhotonNetwork.Time + droppedItemsLifetime;
            }
            return drop;
        }

        public override void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            base.OnPhotonSerializeView(stream, info);
            if (stream.IsWriting)
            {
                stream.SendNext(hotbarSlot);
            }
            if (stream.IsReading)
            {
                int oldSlot = hotbarSlot;
                hotbarSlot = stream.ReceiveNext<int>();
                UpdateHeldItem(oldSlot, hotbarSlot);
            }
            
        }
    }
}