using System;
using System.Collections;
using JetBrains.Annotations;
using NHSRemont.Entity;
using NHSRemont.Environment.Fractures;
using NHSRemont.Networking;
using NHSRemont.Utility;
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
        [Header("Settings")]
        public string typeName = null;
        [Tooltip("Maximum amount of this item that can be stored in one slot." +
                 "\nNOTE: Only simple items (no extra data) should be stackable! Also, stackable items must be present in the resources folder so they can be instantiated.")]
        public int maxStackAmount = 1;
        public float mass = 1f;
        public virtual float punchPower => 1;
        //public float punchCooldown = 0.25f; //TODO (also animations)
        public SFXCollection punchSFX;

        [Header("Runtime")]
        [SerializeField]
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
        /// <summary>
        /// Called when this item stack gets transferred to another, leaving nothing behind.
        /// Also called when the item stack's amount reaches zero.
        /// </summary>
        public Action<Item> onTransferredFullyOrDepleted;

        protected virtual void Awake()
        {
            if (gameObject.name.Contains("(Clone)"))
                gameObject.name = gameObject.name.Replace("(Clone)", "");
            if (string.IsNullOrEmpty(typeName))
                typeName = gameObject.name;
         
            photonView.AddCallbackTarget(this);
            photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
            
            Rigidbody rb = transform.GetOrAddComponent<Rigidbody>();
            rb.mass = mass;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            PhysicsManager.instance.RegisterRigidbody(rb, PhysicsManager.PhysObjectType.NORMAL);
            transform.GetComponent<Collider>().sharedMaterial = PhysicsManager.droppedItemMaterial;
            SetPhysicsEnabled(false);
            droppedTime = double.NegativeInfinity;
        }

        private void FixedUpdate()
        {
            if (photonView.IsMine && dropped)
            {
                if(transform.position.y < -1000)
                    RemoveSelf();
                
                if(despawnTime == 0) return;
                if (PhotonNetwork.Time >= despawnTime)
                {
                    despawnTime = 0;
                    RemoveSelf();
                }
            }
        }

        /// <summary>
        /// Called every frame while the item is held (only called on owner's client)
        /// </summary>
        /// <param name="holderInventory">The entity holding this item</param>
        public virtual void WhileHeld([CanBeNull] CharacterInventory holderInventory)
        {
            
        }

        public void DoPunchAnimAndSFX(Vector3 hitPos)
        {
            if(punchSFX)
                punchSFX.PlayRandomSoundAtPosition(hitPos);
        }

        private void OnDestroy()
        {
            photonView.RemoveCallbackTarget(this);
        }

        public void RemoveSelf()
        {
            if(!photonView.IsMine)
                return;
            
            InvokeTransferredFullyOrDepletedCallback();
            PhotonNetwork.Destroy(gameObject);
        }

        public void SetPhysicsEnabled(bool physicsEnabled)
        {
            if(dropped == physicsEnabled)
                return;
            
            var colliders = gameObject.GetComponentsInChildrenTerminating<Collider, Item>(false);
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
        
        public static Item CreateInstance(
            Item reference, Vector3 where, Quaternion rotation,
            bool ownedByRoom=false,
            bool canBePickedUpInstantly=false,
            bool physicsEnabled=true)
        {
            GameObject itemGO = ownedByRoom ?
                PhotonNetwork.InstantiateRoomObject(reference.name, where, rotation) : 
                PhotonNetwork.Instantiate(reference.name, where, rotation);
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
        public static int Transfer(Item from, ref Item to, int maxAmount = -1)
        {
            int amt = GetTransferableAmount(from, to);
            if (maxAmount != -1) //-1 means no max amount
            {
                amt = Mathf.Min(maxAmount, amt);
            }
            
            if (amt > 0)
            {
                if (to == null)
                {
                    from.InvokeTransferredFullyOrDepletedCallback();
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

        public void InvokeTransferredFullyOrDepletedCallback()
        {
            Debug.Log(name + " transferred fully or depleted");
            onTransferredFullyOrDepleted?.Invoke(this);
            onTransferredFullyOrDepleted = null;
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
            if (from == null || !from.CanCombineStacks(to)) //empty source or stack mismatch on non-empty destination
                return 0;
            
            return Mathf.Min(from.amount, to.maxStackAmount - to.amount);
        }

        /// <summary>
        /// Checks whether this item and the other item are similar enough that they may be combined into one stack.
        /// Does not take into account maximum stack size, just the item data.
        /// </summary>
        /// <param name="other">The item stack to compare against (may not be null)</param>
        /// <returns>True if the items are similar enough to be combinable</returns>
        public virtual bool CanCombineStacks(Item other)
        {
            return this.name == other.name;
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

        public virtual void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            try
            {
                if (stream.IsWriting)
                {
                    stream.SendNext((short) _amount);
                    stream.SendNext(dropped);
                    stream.SendNext((float) droppedTime);
                    stream.SendNext((float) despawnTime);
                }

                if (stream.IsReading)
                {
                    _amount = stream.ReceiveNext<short>();
                    
                    bool nowDropped = stream.ReceiveNext<bool>();
                    if (dropped != nowDropped)
                    {
                        SetPhysicsEnabled(nowDropped);
                    }

                    droppedTime = stream.ReceiveNext<float>();
                    despawnTime = stream.ReceiveNext<float>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e, this);
                
            }
        }
    }
}