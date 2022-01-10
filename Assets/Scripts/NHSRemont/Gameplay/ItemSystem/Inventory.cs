using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem
{
    [RequireComponent(typeof(InventoriesManager))]
    public class Inventory : MonoBehaviourPunCallbacks, IOnPhotonViewPreNetDestroy, IOnPhotonViewControllerChange
    {
        private InventoriesManager invManager;
        
        [SerializeField] protected short _size; //exposed to editor
        public short size => _size; //getter only exposed to other scripts
        [SerializeField] protected Item[] slots;
        [SerializeField] private Transform _itemHolder; //exposed to editor
        public Transform itemHolder => _itemHolder; //getter only exposed to other scripts

        /// <summary>
        /// Arguments: Item in the slot, slot index
        /// </summary>
        public Action<Item, int> onSlotContentsChanged;

        protected virtual void Awake()
        {
            invManager = GetComponent<InventoriesManager>();
            slots = new Item[_size];
        }

        protected virtual void Start()
        {
            if (_itemHolder == null)
                _itemHolder = transform;
            photonView.AddCallbackTarget(this);
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    SendRPCThroughManager(nameof(AddItemRPC), newPlayer, slots[i].photonView.ViewID, (short)i);
                }
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if(Equals(otherPlayer, photonView.Owner))
                Debug.Log(name+": player left room");
        }

        public virtual void OnPreNetDestroy(PhotonView rootView)
        {
            Debug.Log(name+":pre net destroy");
            //FreeItemsFromInvLocally();
        }

        private void OnDestroy()
        {
            photonView.RemoveCallbackTarget(this);
        }

        /// <summary>
        /// Tries to add an item stack to this inventory
        /// </summary>
        /// <returns>Amount of the stack that could fit into this inventory.</returns>
        public int AddItemStack(Item itemStack)
        {
            int transferred = 0;
            int originalAmount = itemStack.amount;
            for (int i = 0; i < slots.Length; i++)
            {
                if (itemStack.amount == 0 || transferred >= originalAmount) break;

                bool slotWasEmpty = slots[i] == null;
                int transferredToSlot = Item.Transfer(itemStack, ref slots[i]);
                if (slotWasEmpty && transferredToSlot > 0)
                {
                    //OnItemAddedToInventory(slots[i]);
                    SendRPCThroughManager(nameof(AddItemRPC), RpcTarget.All, itemStack.photonView.ViewID, (short)i);
                }
                else
                {
                    NotifySlotContentsChanged(
                        i); //this is called in AddItemRPC too, but if we only change the count of the item, we want just this to happen (locally) without the rest of the RPC
                }

                transferred += transferredToSlot;
            }

            return transferred;
        }

        /// <summary>
        /// Calculates how many items from a given stack this inventory could fit
        /// </summary>
        public int HowManyWouldFit(Item itemStack)
        {
            int fit = 0;
            foreach (Item slot in slots)
            {
                if (fit >= itemStack.amount) break;

                fit += Item.GetTransferableAmount(itemStack, slot);
            }

            return Mathf.Min(fit, itemStack.amount);
        }

        public virtual ICollection<Item> DropAllItems(bool individually = true)
        {
            List<Item> dropped = new();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                if (individually)
                {
                    while (slots[i] != null && slots[i].amount > 0)
                    {
                        dropped.Add(DropItemsInSlot(i, 1));
                    }
                }
                else
                {
                    dropped.Add(DropItemsInSlot(i));
                }
            }

            return dropped;
        }
        
        /// <summary>
        /// Locally makes all items no longer part of this inventory.
        /// Use only when inventory should no longer send any RPCs etc (e.g. when it gets network-destroyed).
        /// </summary>
        private void FreeItemsFromInvLocally()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if(slots[i] == null) continue;
                DropItemRPC(i); //call RPC locally
            }
        }

        public virtual Item DropItemsInSlot(int slot, int amount = -1)
        {
            Item item = slots[slot];
            if (item == null || amount == 0) return null;
            if (amount == -1 || amount >= item.amount)
            { 
                //OnItemDroppedFromInventory(item);
                SendRPCThroughManager(nameof(DropItemRPC), RpcTarget.All, (short)slot);
                return item;
            }
            else
            {
                Item dropped = Item.CreateInstance(item, item.transform.position, item.transform.rotation);
                dropped.amount = amount;
                item.amount -= amount;
                NotifySlotContentsChanged(slot);
                return dropped;
            }
        }

        /// <summary>
        /// Transfers items from the specified slot to a different inventory
        /// </summary>
        /// <param name="slot">The slot to transfer the items from</param>
        /// <param name="targetInventory">The inventory to transfer the items to</param>
        /// <param name="amount">The amount of items to transfer</param>
        /// <returns>The amount of items successfully transferred</returns>
        public int TransferSlot(int slot, Inventory targetInventory, int amount = -1)
        {
            if (targetInventory == null)
            {
                Debug.Log("target inv was null");
                return 0;
            }

            if (!targetInventory.photonView.IsMine || !photonView.IsMine)
            {
                Debug.Log(
                    "insufficient ownership (" + targetInventory.photonView.IsMine + "/" + photonView.IsMine + ")");
                return 0; //both inventories must be ours to transfer (TODO allow transfer between different owners?)
            }

            Item item = slots[slot];
            if (item == null)
            {
                Debug.Log("empty slot");
                return 0; //slot was empty
            }

            // int amountInSlot = item.amount;
            int amountTransferred = targetInventory.AddItemStack(item);
            // if (amountTransferred >= amountInSlot)
            // {
            //     slots[slot] = null; //transferred all items out of this slot
            //     NotifySlotContentsChanged(slot);
            // }
            // TODO make sure this commented out code is indeed redundant thanks to the fully transferred/depleted callback

            return amountTransferred;
        }

        protected virtual void OnItemAddedToInventory(Item item)
        {
            item.SetPhysicsEnabled(false);
            item.transform.SetParent(_itemHolder);
            item.transform.localPosition = item.transform.localEulerAngles = Vector3.zero;
            item.gameObject.SetActive(false);
        }

        private void OnItemDroppedFromInventory(Item item)
        {
            Debug.Log(item + " DROPPED FROM " + name + "!", item);
            item.InvokeTransferredFullyOrDepletedCallback();
            item.transform.SetParent(PhysicsManager.instance.transform, true);
            item.SetPhysicsEnabled(true);
            item.gameObject.SetActive(true);
        }

        public Item GetSlot(int index)
        {
            return slots[index];
        }

        private void NotifySlotContentsChanged(int index)
        {
            onSlotContentsChanged?.Invoke(slots[index], index);
        }

        public void OnControllerChange(Player newController, Player previousController)
        {
            if (previousController.IsLocal)
            {
                foreach (PhotonView childPv in GetComponentsInChildren<PhotonView>())
                {
                    Debug.Log("Transferring child pv " + childPv.name + " from " + previousController + " to " +
                              newController);
                    childPv.TransferOwnership(newController);
                }
            }
        }

        private void SendRPCThroughManager(string methodName, RpcTarget target, params object[] args)
        {
            byte idx = invManager.InventoryToIndex(this);
            object[] fullArgs = new object[] { idx }.Concat(args).ToArray();
            photonView.RPC(methodName, target, fullArgs);
        }

        private void SendRPCThroughManager(string methodName, Player target, params object[] args)
        {
            byte idx = invManager.InventoryToIndex(this);
            object[] fullArgs = new object[] { idx }.Concat(args).ToArray();
            photonView.RPC(methodName, target, fullArgs);
        }
        
        public void AddItemRPC(int itemViewId, int slot)
        {
            Item item = PhotonView.Find(itemViewId).GetComponent<Item>();
            OnItemAddedToInventory(item);
            slots[slot] = item;
            NotifySlotContentsChanged(slot);
            
            slots[slot].onTransferredFullyOrDepleted += _ =>
            {
                if(photonView.IsMine)
                    SendRPCThroughManager(nameof(ClearSlotRPC), RpcTarget.All, (short)slot);
            };
        }

        public void DropItemRPC(int slot)
        {
            Item item = slots[slot];
            if(item != null)
                OnItemDroppedFromInventory(item);
        }

        public void ClearSlotRPC(int slot)
        {
            slots[slot] = null;
            NotifySlotContentsChanged(slot);
        }
    }
}