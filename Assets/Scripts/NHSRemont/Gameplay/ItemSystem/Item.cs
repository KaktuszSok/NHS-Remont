using System.Collections;
using NHSRemont.Entity;
using NHSRemont.Environment.Fractures;
using NHSRemont.Networking;
using Photon.Pun;
using UnityEngine;
using Player = Photon.Realtime.Player;

namespace NHSRemont.Gameplay.ItemSystem
{
    /// <summary>
    /// Represents an item. All items are network objects, no matter if they are dropped, in an inventory, held by a character, mounted on a vehicle, etc.
    /// </summary>
    [RequireComponent(typeof(PhotonView), typeof(PhotonTransformView))]
    public class Item : MonoBehaviourPun, IOnPhotonViewOwnerChange, IPunObservable
    {
        public string typeName = null;
        public int maxStackAmount = 1;
        public float mass = 1f;

        private int _amount = 1;
        public int amount
        {
            get => _amount;
            set
            {
                if (value < 0)
                {
                    Debug.LogError($"Can not set stack amount to less than 0 (tried {value}) for item {name}", this);
                    return;
                }
                if (value > maxStackAmount)
                {
                    Debug.LogError($"Can not set stack amount to greater than {maxStackAmount} (tried {value}) for item {name}", this);
                    return;
                }

                _amount = value;
                if (_amount == 0)
                {
                    RemoveSelf();
                }
            }
        }

        public bool dropped { get; private set; } = true;
        public double despawnTime = 0; //When this item will be despawned (0 to not despawn)
        private double droppedTime = 0; //When this item was last dropped to the ground

        private Inventory targetInventoryOnOwnershipGranted = null;

        private void Awake()
        {
            if (gameObject.name.Contains("(Clone)"))
                gameObject.name = gameObject.name.Replace("(Clone)", "");
            if (string.IsNullOrEmpty(typeName))
                typeName = gameObject.name;
         
            photonView.AddCallbackTarget(this);
            photonView.Synchronization = ViewSynchronization.ReliableDeltaCompressed;
            
            Rigidbody rb = transform.GetOrAddComponent<Rigidbody>();
            rb.mass = mass;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            PhysicsManager.instance.RegisterRigidbody(rb, PhysicsManager.PhysObjectType.NORMAL);
            transform.GetComponent<Collider>().sharedMaterial = new PhysicMaterial
            {
                name = "Dropped Item",
                dynamicFriction = 0.75f,
                staticFriction = 0.75f,
                bounciness = 0.3f
            };
            SetPhysicsEnabled(false);
            droppedTime = double.NegativeInfinity;
        }

        private void FixedUpdate()
        {
            if (photonView.IsMine && dropped)
            {
                if(despawnTime == 0) return;
                if (PhotonNetwork.Time >= despawnTime)
                {
                    despawnTime = 0;
                    RemoveSelf();
                }
            }
        }

        private void OnDestroy()
        {
            photonView.RemoveCallbackTarget(this);
        }

        public void RemoveSelf()
        {
            PhotonNetwork.Destroy(gameObject);
        }

        public void SetPhysicsEnabled(bool physicsEnabled)
        {
            if(dropped == physicsEnabled)
                return;
            
            var colliders = GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
            {
                c.enabled = physicsEnabled;
            }

            GetComponent<Rigidbody>().isKinematic = !physicsEnabled;
            dropped = physicsEnabled;
            if (!dropped)
            {
                despawnTime = 0;
            }
            else
            {
                droppedTime = PhotonNetwork.Time;
            }
        }

        /// <summary>
        /// (Delayed) Pick up the items contained in this dropped stack and put them in the target inventory
        /// </summary>
        /// <param name="targetInventory"></param>
        public void PickUp(Inventory targetInventory)
        {
            if(!targetInventory.photonView.IsMine)
                return;
            
            int fit = targetInventory.HowManyWouldFit(this);
            if(fit <= 0)
                return;

            if (!photonView.IsMine)
            {
                targetInventoryOnOwnershipGranted = targetInventory;
                photonView.RequestOwnership();
            }
            else
            {
                targetInventory.AddItemStack(this);
            }
        }

        public void OnOwnerChange(Player newOwner, Player previousOwner)
        {
            if (newOwner.IsLocal && targetInventoryOnOwnershipGranted != null)
            {
                StartCoroutine(WaitThenAddItems());
            }
        }

        private IEnumerator WaitThenAddItems()
        {
            yield return null;
            while (!photonView.IsMine)
            {
                yield return null;
            }
            
            if (targetInventoryOnOwnershipGranted != null && targetInventoryOnOwnershipGranted.photonView.IsMine)
            {
                targetInventoryOnOwnershipGranted.AddItemStack(this);
                targetInventoryOnOwnershipGranted = null;
            }
        }

        [ContextMenu("Print Details to Console")]
        private void PrintDetailsToConsole()
        {
            Debug.Log(this + ", dropped=" + dropped);
        }
        
        public static Item CreateInstance(Item reference, Vector3 where, bool physicsEnabled=true, bool canBePickedUpInstantly=false)
        {
            GameObject itemGO = PhotonNetwork.Instantiate(reference.name, where, Quaternion.identity);
            Item item = itemGO.GetComponent<Item>();
            item.SetPhysicsEnabled(physicsEnabled);
            if(canBePickedUpInstantly)
                item.droppedTime = double.NegativeInfinity;
            return item;
        }

        /// <summary>
        /// Tries to transfer items from one stack to another.
        /// If the destination is null, then it will be assigned to the source.
        /// </summary>
        /// <returns>The amount of items transferred</returns>
        public static int Transfer(Item from, ref Item to)
        {
            
            int amt = GetTransferableAmount(from, to);
            if (amt > 0)
            {
                if (to == null)
                {
                    to = from;
                }
                else
                {
                    from.amount -= amt;
                    to.amount += amt;
                }
            }
            return amt;
        }

        /// <summary>
        /// Returns how many items could be transferred from one stack to another
        /// </summary>
        /// <returns>The amount of items that could be transferred</returns>
        public static int GetTransferableAmount(Item from, Item to)
        {
            if (to == null) //destination is null:
            {
                if (from == null)
                    return 0;
                else
                    return from.amount;
            }
            
            //otherwise, destination is not null:
            if (from == null || from.name != to.name) //empty source or type mismatch on non-empty destination
                return 0;
            
            return Mathf.Min(from.amount, to.maxStackAmount - to.amount);
        }

        public override string ToString()
        {
            return "(" + typeName + " x" + amount + ")";
        }

        private void OnCollisionEnter(Collision other)
        {
            if(PhotonNetwork.Time - droppedTime < 1.0f) return; //can't be picked up yet
            if(other.rigidbody == null) return;
            
            Inventory inv = other.rigidbody.GetComponentInChildren<CharacterInventory>();
            if (inv && inv.photonView.IsMine)
            {
                PickUp(inv);
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(dropped);
                stream.SendNext((float)droppedTime);
                stream.SendNext((float)despawnTime);
            }

            if (stream.IsReading)
            {
                bool nowDropped = stream.ReceiveNext<bool>();
                if (dropped != nowDropped)
                {
                    SetPhysicsEnabled(nowDropped);
                }
                droppedTime = stream.ReceiveNext<float>();
                despawnTime = stream.ReceiveNext<float>();
            }
        }
    }
}