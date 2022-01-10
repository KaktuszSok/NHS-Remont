using System;
using System.Collections.Generic;
using System.Linq;
using C5;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NHSRemont.Networking
{
    public class BandwidthLimiter : MonoBehaviour
    {
        public static BandwidthLimiter instance;
        
        public enum BandwidthBudgetCategory
        {
            GRAPHS
        }
        private static readonly Dictionary<BandwidthBudgetCategory, int> bandwidthBudgets = new()
        {
            {BandwidthBudgetCategory.GRAPHS, 10_000}
        };
        /// <summary>
        /// If the bandwidth used this second (as a fraction of the budget) exceeds the first value, the chance to allow more to be used is 1/the second value.
        /// Bandwidth is calculated per-category.
        /// </summary>
        private static readonly TreeDictionary<float, float> bandwidthBudgetPenalties = new()
        {
            {float.NegativeInfinity, 1f},
            {0.5f, 2f}, //exceeding 50% of the budget makes it half as likely for more bandwidth to be approved
            {0.75f, 3f},
            {1f, 4f}
        };
        private Dictionary<BandwidthBudgetCategory, int> bandwidthUsed;
        private BandwidthBudgetCategory[] bandwidthUsedKeys;
        private float nextBandwidthResetTime = 0f;

        public static Action<Dictionary<BandwidthBudgetCategory, int>> onBandwidthUsageMeasured;

        private void Awake()
        {
            instance = this;
            
            bandwidthUsed = new();
            foreach (var keyValuePair in bandwidthBudgets)
            {
                bandwidthUsed.Add(keyValuePair.Key, 0);
            }
            bandwidthUsedKeys = bandwidthUsed.Keys.ToArray();
        }
        
        private void FixedUpdate()
        {
            if (Time.time > nextBandwidthResetTime)
            {
                nextBandwidthResetTime = Time.time + 1f;
                onBandwidthUsageMeasured?.Invoke(new Dictionary<BandwidthBudgetCategory, int>(bandwidthUsed));
                foreach (BandwidthBudgetCategory key in bandwidthUsedKeys)
                {
                    bandwidthUsed[key] = 0;
                }
            }
        }

        /// <summary>
        /// Seeks approval to use more bandwidth in a category.
        /// Returns true if the usage of more bandwidth gets approved.
        /// </summary>
        public bool CanUseMoreBandwidth(BandwidthBudgetCategory category)
        {
            float fraction = bandwidthUsed[category] / (float)bandwidthBudgets[category];
            float chance = 1f / bandwidthBudgetPenalties.Predecessor(fraction).Value;
            if (Random.value <= chance)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tells the controller that a certain amount of bandwidth in a category was used.
        /// </summary>
        public void UseBandwidth(BandwidthBudgetCategory category, int amount)
        {
            bandwidthUsed[category] += amount;
        }
    }
}