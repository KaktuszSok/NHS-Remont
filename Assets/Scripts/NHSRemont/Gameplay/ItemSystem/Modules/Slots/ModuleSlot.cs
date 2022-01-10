using System;
using JetBrains.Annotations;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Gameplay.ItemSystem.Modules.Slots
{
    /// <summary>
    /// A slot that allows this item to be fitted with modules of type T
    /// </summary>
    /// <typeparam name="T">The type of module this slot supports</typeparam>
    [RequireComponent(typeof(Item))]
    public class ModuleSlot<T> : Inventory where T : ItemModule
    {
        [Tooltip("What is the maximum amount of ")]
        public int maxModuleStackAmount = 1;

        /// <summary>
        /// The module that is currently fitted on this slot (may be null)
        /// </summary>
        public T currentModule => (T)slots[0];

        public Action<T> moduleFittedCallback;
        public Action<T> moduleUnfittedCallback;

        protected override void Awake()
        {
            _size = 1;
            base.Awake();
        }

        public override void OnPreNetDestroy(PhotonView rootView)
        {
            //don't drop modules when destroyed - destroy them too
        }

        protected override void OnItemAddedToInventory(Item item)
        {
            base.OnItemAddedToInventory(item);
            item.gameObject.SetActive(true); //display fitted module
        }

        /// <summary>
        /// Fits the given module to this slot and unfits the previously fitted module.
        /// </summary>
        /// <param name="module">The new module to fit (may be null)</param>
        /// <param name="dropInventory">The inventory to drop the old module into (if any)</param>
        public void FitModule(T module, [CanBeNull] Inventory dropInventory)
        {
            if(!photonView.IsMine)
                return;

            UnfitModule(dropInventory);

            if (module != null && IsModuleAccepted(module))
            {
                int amt = AddItemStack(module);
                if (amt > 0)
                {
                    currentModule.OnFitted();
                    moduleFittedCallback?.Invoke(currentModule);
                }
            }
        }

        public void UnfitModule(Inventory dropInventory, bool dropOnGround=false)
        {
            if(!photonView.IsMine)
                return;
            
            if (currentModule != null)
            {
                currentModule.OnUnfitted();
                moduleUnfittedCallback?.Invoke(currentModule);
                if(!dropOnGround)
                    TransferSlot(0, dropInventory);
                DropItemsInSlot(0); //any leftover items in this slot get dropped
            }
        }

        /// <summary>
        /// Checks if the given module is accepted into this slot
        /// </summary>
        public virtual bool IsModuleAccepted(T module)
        {
            return true;
        }

        /// <summary>
        /// Tries to find a module of the appropriate type among the children of the given transform and fits it if found.
        /// </summary>
        public void TryFindModuleToFit(Transform modulesParent)
        {
            if(!photonView.IsMine)
                return;

            T[] foundModules = modulesParent.GetComponentsInChildren<T>();
            foreach (T foundModule in foundModules)
            {
                if (IsModuleAccepted(foundModule))
                {
                    FitModule(foundModule, null);
                    break;
                }
            }
        }
    }
}