using System;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem
{
    public class Inventory
    {
        public readonly int size;
        private readonly ItemStack[] slots;
        public Action<ItemStack, int> onSlotContentsChanged;

        public Inventory(int size)
        {
            this.size = size;
            slots = new ItemStack[size];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new ItemStack(null, 0);
                int idx = i;
                slots[i].onContentsChanged += () =>
                {
                    NotifySlotContentsChanged(idx);
                };
            }
        }

        /// <summary>
        /// Tries to add an item stack to this inventory
        /// </summary>
        /// <returns>Amount of the stack that could fit into this inventory.</returns>
        public int AddItemStack(ItemStack itemStack)
        {
            int transferred = 0;
            foreach (ItemStack slot in slots)
            {
                if (itemStack.amount == 0) break;

                transferred += ItemStack.Transfer(itemStack, slot);
            }

            return transferred;
        }

        /// <summary>
        /// Calculates how many items from a given stack this inventory could fit
        /// </summary>
        public int HowManyWouldFit(ItemStack itemStack)
        {
            int fit = 0;
            foreach (ItemStack slot in slots)
            {
                if(fit >= itemStack.amount) break;

                fit += ItemStack.GetTransferableAmount(itemStack, slot);
            }
            return Mathf.Min(fit, itemStack.amount);
        }

        public ItemStack GetSlot(int index)
        {
            return slots[index];
        }

        private void NotifySlotContentsChanged(int index)
        {
            onSlotContentsChanged?.Invoke(slots[index], index);
        }

        public void Send(PhotonStream stream)
        {
            foreach (ItemStack slot in slots)
            {
                slot.Send(stream);
            }
        }

        public void Receive(PhotonStream stream)
        {
            foreach (ItemStack slot in slots)
            {
                slot.Receive(stream);
            }
        }
    }
}