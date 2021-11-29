using NHSRemont.Gameplay.ItemSystem;
using Photon.Pun;

namespace NHSRemont.Entity
{ 
    public abstract class EntityInventory : MonoBehaviourPun, IPunObservable
    {
        public abstract Inventory inventory { get; }

        /// <summary>
        /// Add an item stack with no extra data
        /// </summary>
        [PunRPC]
        public void AddSimpleItemStackRPC(string itemType, int amount)
        {
            ItemStack stack = new ItemStack(ItemType.FromName(itemType), amount);
            inventory.AddItemStack(stack);
        }

        /// <summary>
        /// Add an item stack with extra data
        /// </summary>
        [PunRPC]
        public void AddItemStackRPC(string itemType, int amount, object[] dataKeys, object[] dataValues)
        {
            ItemStack stack = new ItemStack(ItemType.FromName(itemType), amount);
            for (int i = 0; i < dataKeys.Length; i++)
            {
                stack.data.Add((string)dataKeys[i], dataValues[i]);
            }
            inventory.AddItemStack(stack);
        }

        public virtual void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                inventory.Send(stream);
            }
            if (stream.IsWriting)
            {
                inventory.Receive(stream);
            }
        }
    }
}