using System;
using System.Collections.Generic;
using NHSRemont.Networking;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem
{
    [Serializable]
    public class ItemStack
    {
        public ItemType type { get; private set; }
        private int _amount = 0;
        public int amount
        {
            get => _amount;
            set
            {
                if (type == null)
                {
                    Clear();
                    return;
                }

                if (value < 0)
                {
                    Debug.LogError($"Can not set stack amount to less than 0 (tried {value}) for item {type.name}");
                    return;
                }
                if (value > type.maxStackSize)
                {
                    Debug.LogError($"Can not set stack amount to greater than {type.maxStackSize} (tried {value}) for item {type.name}");
                    return;
                }

                _amount = value;
                if (value == 0)
                {
                    Clear();
                }
                onContentsChanged?.Invoke();
            }
        }
        public readonly Dictionary<string, object> data = new();

        public Action onContentsChanged;

        public ItemStack(ItemType type, int amount=1)
        {
            this.type = type;
            this.amount = amount;
        }

        public void Clear()
        {
            _amount = 0;
            type = null;
            data.Clear();
        }

        /// <summary>
        /// Tries to transfer items from one stack to another
        /// </summary>
        /// <returns>The amount of items transferred</returns>
        public static int Transfer(ItemStack from, ItemStack to)
        {
            int amt = GetTransferableAmount(from, to);
            if (amt > 0)
            {
                if (to.amount == 0)
                    to.type = from.type;
                
                from.amount -= amt;
                to.amount += amt;
            }
            return amt;
        }

        /// <summary>
        /// Returns how many items could be transferred from one stack to another
        /// </summary>
        /// <returns>The amount of items that could be transferred</returns>
        public static int GetTransferableAmount(ItemStack from, ItemStack to)
        {
            if (to.amount != 0 && from.type != to.type || from.type == null) //type mismatch on non-empty destination or source is empty
                return 0;
            return Mathf.Min(from.amount, from.type.maxStackSize - to.amount);
        }

        public void Send(PhotonStream stream)
        {
            stream.SendNext(type.name);
            stream.SendNext(_amount);
            stream.SendNext(data.Count);
            foreach ((string key, object value) in data)
            {
                stream.SendNext(key);
                stream.SendNext(value);
            }
        }

        public void Receive(PhotonStream stream)
        {
            string name = stream.ReceiveNext<string>();
            type = ItemType.FromName(name);
            _amount = stream.ReceiveNext<int>();
            
            data.Clear();
            int dataCount = stream.ReceiveNext<int>();
            for (int i = 0; i < dataCount; i++)
            {
                string k = stream.ReceiveNext<string>();
                object v = stream.ReceiveNext();
                data.Add(k,v);
            }
            onContentsChanged?.Invoke();
        }
    }
}