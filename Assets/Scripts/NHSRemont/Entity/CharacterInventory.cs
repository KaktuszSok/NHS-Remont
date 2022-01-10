using System;
using System.Collections.Generic;
using NHSRemont.Environment;
using NHSRemont.Gameplay;
using NHSRemont.Gameplay.ItemSystem;
using NHSRemont.Networking;
using Photon.Pun;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NHSRemont.Entity
{
    public class CharacterInventory : Inventory, IPunObservable
    {
        private const int slotsCount = 9;
        private const double droppedItemsLifetime = 150;
        private const float emptyHandPunchPower = 2500f;

        [Tooltip("The \"head\" of this character, used for punching raycasts etc.")]
        public Transform lookingTransform;

        public int hotbarSlot { get; private set; }
        private Item heldItem;

        public Action<int> OnHotbarSlotSelected;

        protected override void Awake()
        {
            _size = slotsCount;
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
            SelectSlot(0);
            onSlotContentsChanged += (item, idx) =>
            {
                if (idx == hotbarSlot)
                {
                    UpdateHeldItem(hotbarSlot, hotbarSlot);
                }
            };
        }

        private void Update()
        {
            if (heldItem != null)
            {
                heldItem.WhileHeld(this);
            }
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
            Item oldItem = slots[oldSlot];
            Item newItem = slots[newSlot];

            if(oldSlot != newSlot && oldItem == newItem) return;
            
            if(oldItem != null)
                oldItem.gameObject.SetActive(false);
            
            if(newItem != null)
                newItem.gameObject.SetActive(true);

            heldItem = newItem;
        }

        public override ICollection<Item> DropAllItems(bool individually = true)
        {
            var drops = base.DropAllItems(individually);
            foreach (Item drop in drops)
            {
                drop.transform.position += Random.insideUnitSphere * 0.025f;
            }

            return drops;
        }

        public override Item DropItemsInSlot(int slot, int amount = -1)
        {
            Item drop = base.DropItemsInSlot(slot, amount);
            if (drop)
            {
                drop.despawnTime = PhotonNetwork.Time + droppedItemsLifetime;
            }
            return drop;
        }

        /// <summary>
        /// Tries to throw a punch using the currently held item (or empty hand)
        /// </summary>
        public void Punch()
        {
            float punchPower = emptyHandPunchPower;
            if (heldItem != null)
            {
                punchPower = heldItem.punchPower;
                if(punchPower == 0f) //Item does not allow punching
                    return;
            }
            
            photonView.RPC(nameof(PunchRPC), RpcTarget.All, lookingTransform.position, lookingTransform.forward, punchPower, 3f);
        }

        [PunRPC]
        public void PunchRPC(Vector3 headPosition, Vector3 headForward, float punchPower, float punchDistance)
        {
            if (Physics.Raycast(headPosition, headForward, out RaycastHit hit, punchDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                GameObject target = hit.transform.gameObject;
                if(target == null)
                    return;

                Vector3 impulse = headForward * punchPower;
                target.ApplyImpulseAtPoint(impulse, hit.point);

                if (heldItem != null)
                    heldItem.DoPunchAnimAndSFX(hit.point);
                else
                {
                    GameManager.instance.gameplayReferences
                        .emptyHandPunchSFX.PlayRandomSoundAtPosition(hit.point);
                }
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(hotbarSlot);
            }
            if (stream.IsReading)
            {
                int oldSlot = hotbarSlot;
                hotbarSlot = stream.ReceiveNext<int>();
                UpdateHeldItem(oldSlot, hotbarSlot);
            }
        }
    }
}