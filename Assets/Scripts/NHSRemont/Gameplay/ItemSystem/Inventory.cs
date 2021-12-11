using System;
using System.Collections.Generic;
using NHSRemont.Networking;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem
{
    public class Inventory : MonoBehaviourPun, IPunObservable, IOnPhotonViewPreNetDestroy
    {
        [SerializeField] protected int size;
        [SerializeField] protected Item[] slots;
        [SerializeField] protected Transform itemHolder;
        /// <summary>
        /// Arguments: Item in the slot, slot index
        /// </summary>
        public Action<Item, int> onSlotContentsChanged;

        protected virtual void Start()
        {
            if (itemHolder == null)
                itemHolder = transform;
            photonView.Synchronization = ViewSynchronization.ReliableDeltaCompressed;
            photonView.AddCallbackTarget(this);
            slots = new Item[size];
        }
        
        public void OnPreNetDestroy(PhotonView rootView)
        {
            DropAllItems();
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
            for(int i = 0; i < slots.Length; i++)
            {
                if (itemStack.amount == 0 || transferred >= originalAmount) break;

                bool slotWasEmpty = slots[i] == null;
                int transferredToSlot = Item.Transfer(itemStack, ref slots[i]);
                if (slotWasEmpty && transferredToSlot > 0)
                {
                    OnItemAddedToInventory(slots[i]);
                }

                transferred += transferredToSlot;
                NotifySlotContentsChanged(i);
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
                if(fit >= itemStack.amount) break;

                fit += Item.GetTransferableAmount(itemStack, slot);
            }
            return Mathf.Min(fit, itemStack.amount);
        }

        public virtual ICollection<Item> DropAllItems(bool individually=true)
        {
            List<Item> dropped = new();
            for (int i = 0; i < slots.Length; i++)
            {
                if(slots[i] == null) continue;
                if (individually)
                {
                    while(slots[i] != null && slots[i].amount > 0)
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
        
        public virtual Item DropItemsInSlot(int slot, int amount=-1)
        {
            Item item = slots[slot];
            if (item == null || amount == 0) return null;
            if (amount == -1 || amount >= item.amount)
            {
                slots[slot] = null;
                OnItemDroppedFromInventory(item);
                NotifySlotContentsChanged(slot);
                return item;
            }
            else
            {
                Item dropped = Item.CreateInstance(item, item.transform.position);
                dropped.amount = amount;
                item.amount -= amount;
                NotifySlotContentsChanged(slot);
                return dropped;
            }
        }

        private void OnItemAddedToInventory(Item item)
        {
            item.SetPhysicsEnabled(false);
            item.transform.SetParent(itemHolder);
            item.transform.localPosition = item.transform.localEulerAngles = Vector3.zero;
            item.gameObject.SetActive(false);
            Debug.Log(item.name + " added to inv", this);
        }

        private void OnItemDroppedFromInventory(Item item)
        {
            item.transform.SetParent(PhysicsManager.instance.transform, true);
            item.transform.rotation = Quaternion.identity;
            item.SetPhysicsEnabled(true);
            item.gameObject.SetActive(true);
            Debug.Log(item.name + " removed from inv", this);
        }

        public Item GetSlot(int index)
        {
            return slots[index];
        }

        private void NotifySlotContentsChanged(int index)
        {
            onSlotContentsChanged?.Invoke(slots[index], index);
        }

        public virtual void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if(slots[i] == null)
                        stream.SendNext(-1);
                    else
                        stream.SendNext(slots[i].photonView.ViewID);
                }
            }
            
            if (stream.IsReading)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    int viewId = stream.ReceiveNext<int>();
                    if (viewId == -1)
                    {
                        Item prev = slots[i];
                        if (prev != null)
                        {
                            OnItemDroppedFromInventory(prev);
                            slots[i] = null;
                        }
                    }
                    else
                    {
                        Item curr = PhotonView.Find(viewId).GetComponent<Item>();
                        if (slots[i] != curr)
                        {
                            OnItemAddedToInventory(curr);
                            slots[i] = curr;
                        }
                    }
                }
            }
        }
    }
}