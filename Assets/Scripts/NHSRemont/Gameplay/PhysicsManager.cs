using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.VFX;

namespace NHSRemont.Gameplay
{
    public class PhysicsManager : MonoBehaviourPun
    {
        public static PhysicsManager instance;

        public enum PhysObjectType
        {
            NORMAL,
            DEBRIS_SMALL,
            DEBRIS_MEDIUM,
            DEBRIS_LARGE
        }
        private readonly Dictionary<PhysObjectType, int> maxObjectsAmount = new()
        {
            {PhysObjectType.NORMAL, -1},
            {PhysObjectType.DEBRIS_SMALL, 150},
            {PhysObjectType.DEBRIS_MEDIUM, 300},
            {PhysObjectType.DEBRIS_LARGE, 750}
        };
        private readonly Dictionary<PhysObjectType, LinkedList<Rigidbody>> rigidbodies = new();
        private readonly Dictionary<Rigidbody, float> rigidbodyCrossAreasCache = new();

        public Action<ExplosionInfo> onExplosion;

        [SerializeField]
        private GameObject explosionVFX;

        private void Awake()
        {
            instance = this;
            rigidbodies[PhysObjectType.NORMAL] = new LinkedList<Rigidbody>();
            rigidbodies[PhysObjectType.DEBRIS_SMALL] = new LinkedList<Rigidbody>();
            rigidbodies[PhysObjectType.DEBRIS_MEDIUM] = new LinkedList<Rigidbody>();
            rigidbodies[PhysObjectType.DEBRIS_LARGE] = new LinkedList<Rigidbody>();
        }

        private void Start()
        {
            var existingRBs = FindObjectsOfType<Rigidbody>();
            foreach (Rigidbody rb in existingRBs)
            {
                if (!rb.isKinematic)
                {
                    RegisterRigidbody(rb, PhysObjectType.NORMAL);
                }
            }
        }

        public void RegisterRigidbody(Rigidbody rb, PhysObjectType classification)
        {
            var rbCollection = rigidbodies[classification];
            if (PhotonNetwork.IsMasterClient)
            {
                int maxCount = maxObjectsAmount[classification];
                if (maxCount != -1)
                {
                    //limit amount of objects of each type
                    while (rbCollection.Count > maxCount)
                    {
                        var node = rbCollection.First;
                        if (node.Value != null)
                        {
                            Destroy(node.Value.gameObject);
                        }

                        rbCollection.Remove(node);
                        RemoveRigidbodyFromCaches(node.Value);
                    }
                }
            }

            rbCollection.AddLast(rb);
        }

        public void RegisterRigidbodies(IEnumerable<Rigidbody> rbs, PhysObjectType classification)
        {
            foreach (Rigidbody rb in rbs)
            {
                RegisterRigidbody(rb, classification);
            }
        }

        /// <summary>
        /// Gets all rigidbodies which have not been destroyed
        /// </summary>
        public List<Rigidbody> GetAllRigidbodies()
        {
            List<Rigidbody> output = new List<Rigidbody>();
            foreach (var rbCollection in rigidbodies.Values)
            {
                var node = rbCollection.First;
                while (node != null)
                {
                    var next = node.Next;
                    Rigidbody rb = node.Value;
                    if (rb == null) //destroyed
                    {
                        rbCollection.Remove(node);
                        RemoveRigidbodyFromCaches(rb);
                    }
                    else
                        output.Add(rb);
                    
                    node = next;
                }
            }

            return output;
        }

        /// <summary>
        /// Gets all rigidbodies which have not been destroyed and are not asleep
        /// </summary>
        public List<Rigidbody> GetAwakeRigidbodies()
        {
            List<Rigidbody> output = new List<Rigidbody>();
            foreach (var rbCollection in rigidbodies.Values)
            {
                var node = rbCollection.First;
                while (node != null)
                {
                    var next = node.Next;
                    Rigidbody rb = node.Value;
                    if (rb == null) //destroyed
                    {
                        rbCollection.Remove(node);
                        RemoveRigidbodyFromCaches(rb);
                    }
                    else if(!rb.IsSleeping())
                        output.Add(rb);
                    
                    node = next;
                }
            }

            return output;
        }

        /// <summary>
        /// Creates an explosion in the world (does not create sound or particles)
        /// </summary>
        public void CreateExplosion(ExplosionInfo explosionInfo)
        {
            photonView.RPC(nameof(CreateExplosionRpc), RpcTarget.All,
                explosionInfo.position, explosionInfo.blastRadius, explosionInfo.power_tnt, explosionInfo.energyFalloffExponent, explosionInfo.upwardsModifier);
        }

        [PunRPC]
        private void CreateExplosionRpc(Vector3 pos, float blastRadius, float power_tnt, float falloff, float upwardsModifier)
        {
            ExplosionInfo explosionInfo = new ExplosionInfo((pos, blastRadius, (double)power_tnt*ExplosionInfo.joulesPerKgTnt, falloff, upwardsModifier));
            
            float blastRadiusSqr = explosionInfo.blastRadius * explosionInfo.blastRadius;
            foreach (Rigidbody rb in GetAllRigidbodies())
            {
                if ((rb.position - explosionInfo.position).sqrMagnitude <= blastRadiusSqr)
                {
                    //if(rb.tag.Equals("Player")) continue;
                    explosionInfo.ApplyToRigidbody(rb);
                }
            }
            onExplosion?.Invoke(explosionInfo);
            GameObject explosionEffect = Instantiate(explosionVFX);
            explosionEffect.transform.position = explosionInfo.position;
            float scale = explosionInfo.blastRadius / 320f;
            explosionEffect.transform.localScale = Vector3.one * scale;
            explosionEffect.GetComponent<VisualEffect>().Play();
        }

        private void RemoveRigidbodyFromCaches(Rigidbody rb)
        {
            rigidbodyCrossAreasCache.Remove(rb);
        }

        /// <summary>
        /// Estimates a cross-sectional surface area for this rigidbody.
        /// </summary>
        public float EstimateCrossSection(Rigidbody rb)
        {
            if (rigidbodyCrossAreasCache.ContainsKey(rb))
                return rigidbodyCrossAreasCache[rb];
            
            float maxX = rb.ClosestPointOnBounds(rb.position + rb.transform.right * 1000f).x;
            float minX = rb.ClosestPointOnBounds(rb.position - rb.transform.right * 1000f).x;
            float maxY = rb.ClosestPointOnBounds(rb.position + rb.transform.up * 1000f).y;
            float minY = rb.ClosestPointOnBounds(rb.position - rb.transform.up * 1000f).y;
            float maxZ = rb.ClosestPointOnBounds(rb.position + rb.transform.forward * 1000f).z;
            float minZ = rb.ClosestPointOnBounds(rb.position - rb.transform.forward * 1000f).z;
            
            float longestSide = Mathf.Max(Mathf.Abs(maxX - minX), Mathf.Abs(maxY - minY), Mathf.Abs(maxZ - minZ));
            float area = longestSide; //let's just assume that the area is equal to the longest side. For human: about 1.8m^2, for 20m tall tree: 20m^2, etc.

            rigidbodyCrossAreasCache[rb] = area;
            return area;
        }
        
        /// <summary>
        /// Estimates a cross-sectional surface area for this body given its bounding box.
        /// </summary>
        public float EstimateCrossSection(Transform body, Bounds boundingBox)
        {
            Vector3 centre = boundingBox.center;
            float maxX = boundingBox.ClosestPoint(centre + body.right * 1000f).x;
            float minX = boundingBox.ClosestPoint(centre - body.right * 1000f).x;
            float maxY = boundingBox.ClosestPoint(centre + body.up * 1000f).y;
            float minY = boundingBox.ClosestPoint(centre - body.up * 1000f).y;
            float maxZ = boundingBox.ClosestPoint(centre + body.forward * 1000f).z;
            float minZ = boundingBox.ClosestPoint(centre - body.forward * 1000f).z;
            
            float longestSide = Mathf.Max(Mathf.Abs(maxX - minX), Mathf.Abs(maxY - minY), Mathf.Abs(maxZ - minZ));
            float area = longestSide; //let's just assume that the area is equal to the longest side. For human: about 1.8m^2, for 20m tall tree: 20m^2, etc.

            return area;
        }

        /// <summary>
        /// Estimates a cross-sectional surface area for this body given its size (for simple shapes).
        /// </summary>
        public float EstimateCrossSection(Vector3 size)
        {
            return Mathf.Max(size.x, size.y, size.z); //same assumption as in the function above
        }
    }
}