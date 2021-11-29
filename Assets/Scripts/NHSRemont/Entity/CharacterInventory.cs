using System;
using NHSRemont.Gameplay.ItemSystem;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Entity
{
    public class CharacterInventory : EntityInventory
    {
        private const int slotsCount = 9;

        public Transform heldItemParent;
        public override Inventory inventory { get; } = new Inventory(slotsCount);
        private int hotbarSlot;
        private ItemType heldItem;

        public Action<int> OnHotbarSlotSelected;

        private void Start()
        {
            inventory.onSlotContentsChanged += (stack, idx) =>
            {
                if (idx == hotbarSlot)
                    UpdateHeldItem(hotbarSlot, hotbarSlot);
            };
            SelectSlot(0);
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
            ItemType newHeldItem = inventory.GetSlot(newSlot).type;
            if(oldSlot == newSlot && newHeldItem == heldItem)
                return; //same slot and same item type - no need to update held item
            
            //destroy old
            Transform currHeld = heldItemParent.GetChild(0);
            if(currHeld != null)
                PhotonNetwork.Destroy(currHeld.gameObject);
            
            //create new
            PhotonNetwork.Instantiate()
        }

    }
}