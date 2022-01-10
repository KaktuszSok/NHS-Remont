using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem
{
    /// <summary>
    /// This is a terrible system but photon's RPCs suck ass
    /// </summary>
    [Tooltip("Allows multiple inventories on one photonView.")]
    public class InventoriesManager : MonoBehaviour
    {
        private Inventory[] inventories;
        private Dictionary<Inventory, byte> inventoryToIndex;

        public enum InventoryMethod
        {
            ADD_ITEM,
            DROP_ITEM,
            CLEAR_SLOT
        }

        private void Awake()
        {
            inventories = GetComponents<Inventory>();
            inventoryToIndex = new();
            for (byte i = 0; i < inventories.Length; i++)
            {
                inventoryToIndex.Add(inventories[i], i);
            }
        }

        public byte InventoryToIndex(Inventory inv) => inventoryToIndex[inv];

        [PunRPC]
        public void AddItemRPC(byte inventoryIdx, int itemViewId, short slot)
        {
            Inventory targetInventory = inventories[inventoryIdx];
            targetInventory.AddItemRPC(itemViewId, slot);
        }
        
        [PunRPC]
        public void DropItemRPC(byte inventoryIdx, short slot)
        {
            Inventory targetInventory = inventories[inventoryIdx];
            targetInventory.DropItemRPC(slot);
        }
        
        [PunRPC]
        public void ClearSlotRPC(byte inventoryIdx, short slot)
        {
            Inventory targetInventory = inventories[inventoryIdx];
            targetInventory.ClearSlotRPC(slot);
        }
    }
}