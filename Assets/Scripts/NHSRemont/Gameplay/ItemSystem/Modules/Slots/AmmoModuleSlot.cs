using UnityEngine;
using System.Collections.Generic;

namespace NHSRemont.Gameplay.ItemSystem.Modules.Slots
{
    public class AmmoModuleSlot : ModuleSlot<AmmoModule>
    {
        private string preferredAmmoType = null;

        [Header("Ammo Module Slot")]
        [SerializeField] private AmmoModule[] acceptedAmmoTypes;
        private ISet<string> acceptedAmmoTypesSet; //runtime 

        protected override void Awake()
        {
            base.Awake();
            acceptedAmmoTypesSet = new HashSet<string>(acceptedAmmoTypes.Length);
            foreach (AmmoModule acceptedAmmoType in acceptedAmmoTypes)
            {
                acceptedAmmoTypesSet.Add(acceptedAmmoType.typeName);
            }
            
            moduleFittedCallback += module =>
            {
                preferredAmmoType = module.typeName;
            };
        }

        public override bool IsModuleAccepted(AmmoModule module)
        {
            return acceptedAmmoTypesSet.Contains(module.typeName);
        }

        public bool CanShoot()
        {
            if (currentModule == null)
                return false;

            return currentModule.ammoCount > 0;
        }

        /// <summary>
        /// Deplete the ammo by one shot
        /// </summary>
        public void Deplete()
        {
            if(currentModule == null)
                return;

            currentModule.Deplete();
        }

        public void Reload(Inventory ammoSource)
        {
            AmmoModule unloadedAmmoModule = currentModule;
            UnfitModule(ammoSource, true);
            
            //find best ammo to load
            AmmoModule bestCandidate = null;
            bool foundPreferredType = false;
            for (int i = 0; i < ammoSource.size; i++)
            {
                Item invItem = ammoSource.GetSlot(i);
                if(invItem == null || invItem == unloadedAmmoModule)
                    continue;

                if(foundPreferredType && invItem.typeName != preferredAmmoType)
                    continue; //always favour ammo of the preferred type over other types of ammo
                
                if(invItem is not AmmoModule ammoItem) continue; //ignore non-ammo items
                if (!IsModuleAccepted(ammoItem)) continue; //ignore non-accepted ammo items

                if (!foundPreferredType && ammoItem.typeName == preferredAmmoType)
                {
                    foundPreferredType = true;
                    bestCandidate = null; //clear previous best candidate as its type was not the preferred type
                }

                //favour ammo with more rounds available
                if (bestCandidate == null || ammoItem.ammoCount > bestCandidate.ammoCount)
                {
                    bestCandidate = ammoItem;
                }
            }
            
            if(bestCandidate != null)
                FitModule(bestCandidate, ammoSource);
        }
    }
}