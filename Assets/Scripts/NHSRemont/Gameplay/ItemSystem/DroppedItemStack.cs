using System;
using NHSRemont.Entity;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem
{
    [RequireComponent(typeof(Rigidbody), typeof(PhotonView))]
    public class DroppedItemStack : MonoBehaviourPun, IPunObservable
    {
        private static readonly PhysicMaterial physicsMat = new PhysicMaterial
        {
            name = "Dropped Item",
            dynamicFriction = 0.75f,
            staticFriction = 0.75f,
            bounciness = 0.3f
        };

        private Rigidbody rb;
        [SerializeField] private ItemStack stack = new ItemStack(null, 0);

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            PhysicsManager.instance.RegisterRigidbody(rb, PhysicsManager.PhysObjectType.NORMAL);
        }

        private void UpdateModel()
        {
            Transform prevModel = transform.GetChild(0);
            if(prevModel != null)
                Destroy(prevModel.gameObject);
            
            if(stack == null)
                return;
            
            //add model
            Transform model = stack.type.InstantiateModel(transform);
            //configure mass
            rb.mass = stack.amount * stack.type.mass;
        }

        /// <summary>
        /// (Delayed) Pick up the items contained in this dropped stack and put them in the target inventory
        /// </summary>
        /// <param name="targetInventory"></param>
        public void PickUp(EntityInventory targetInventory)
        {
            int amount = targetInventory.inventory.HowManyWouldFit(stack);
            if(amount <= 0)
                return;
            
            photonView.RPC(nameof(PickUpRPC), photonView.Owner, 
                targetInventory.photonView.ViewID, amount);
        }

        /// <summary>
        /// Send this to this object's owner to pick up the contained item stack
        /// </summary>
        /// <param name="targetInventoryViewId">The inventory that should receive the items</param>
        /// <param name="amountToPickUp">The maximum amount of items we want to pick up from this dropped stack</param>
        [PunRPC]
        public void PickUpRPC(int targetInventoryViewId, int amountToPickUp)
        {
            if(!photonView.IsMine)
                return;

            EntityInventory inv = PhotonView.Find(targetInventoryViewId).GetComponent<EntityInventory>();
            amountToPickUp = Mathf.Min(amountToPickUp, stack.amount, inv.inventory.HowManyWouldFit(stack)); //make sure amount doesn't exceed the requested amount, the contained amount or the amount that (we believe) will fit in the target inventory
            if(amountToPickUp <= 0)
                return;
            
            string[] keys = new string[stack.data.Count];
            object[] values = new object[stack.data.Count];
            int i = 0;
            foreach ((string key, object value) in stack.data)
            {
                keys[i] = key;
                values[i] = value;
                i++;
            }
            inv.photonView.RPC(nameof(inv.AddItemStackRPC), inv.photonView.Owner,
                stack.type.name, amountToPickUp, keys, values); //tell the owner to add the items appropriately
            stack.amount -= amountToPickUp; //reduce stack amount

            if(stack.amount == 0)
                PhotonNetwork.Destroy(gameObject);
        }

        public static DroppedItemStack CreateFromStack(ItemStack stack, Vector3 position)
        {
            if (stack.type == null)
                return null;

            GameObject droppedGO = PhotonNetwork.InstantiateRoomObject("Dropped Item", position, Quaternion.identity);
            DroppedItemStack dropped = droppedGO.GetComponent<DroppedItemStack>();
            dropped.stack = stack;
            dropped.UpdateModel();
            return dropped;
        }


        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stack.Send(stream);
            }
            if (stream.IsReading)
            {
                ItemType prevType = stack.type;
                stack.Receive(stream);
                if (stack.type != prevType)
                {
                    UpdateModel();
                }
            }
        }
    }
}