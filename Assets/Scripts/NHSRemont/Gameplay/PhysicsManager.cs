using System;
using System.Collections.Generic;
using NHSRemont.Environment.Fractures;
using NHSRemont.Utility;
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
        private static PhysicMaterial _droppedItemMaterial;

        public static PhysicMaterial droppedItemMaterial
        {
            get
            {
                if (_droppedItemMaterial == null)
                {
                    _droppedItemMaterial = new PhysicMaterial
                    {
                        name = "Dropped Item",
                        dynamicFriction = 0.75f,
                        staticFriction = 0.75f,
                        bounciness = 0.3f
                    };
                }

                return _droppedItemMaterial;
            }
        }

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
            => GetRigidbodiesFiltered(_ => true);

        /// <summary>
        /// Gets all rigidbodies near some point which have not been destroyed
        /// </summary>
        public List<Rigidbody> GetRigidbodiesNear(Vector3 pos, float maxDist)
        {
            float maxDistSqr = maxDist * maxDist;
            return GetRigidbodiesFiltered(rb => (rb.position - pos).sqrMagnitude < maxDistSqr);
        }

        /// <summary>
        /// Gets all rigidbodies which have not been destroyed and are not asleep
        /// </summary>
        public List<Rigidbody> GetAwakeRigidbodies()
            => GetRigidbodiesFiltered(rb => !rb.IsSleeping());

        /// <summary>
        /// Gets all rigidbodies that haven't been destroyed and for which the filter predicate returns true.
        /// </summary>
        public List<Rigidbody> GetRigidbodiesFiltered(Predicate<Rigidbody> filter)
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
                    else if(filter.Invoke(rb))
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
            Debug.Log(explosionInfo + " overpressure: " +
                      "\n0m:" + explosionInfo.GetOverpressureAt(0f*0f) +
                      "\n3m:" + explosionInfo.GetOverpressureAt(3f*3f) +
                      "\n5m:" + explosionInfo.GetOverpressureAt(5f*5f) +
                      "\n10m:" + explosionInfo.GetOverpressureAt(10f*10f) +
                      "\n20m:" + explosionInfo.GetOverpressureAt(20f*20f) +
                      "\n30m:" + explosionInfo.GetOverpressureAt(30f*30f) +
                      "\n50m:" + explosionInfo.GetOverpressureAt(50f*50f));
            Debug.Log(explosionInfo + " intensity: " +
                      "\n0m:" + explosionInfo.GetEnergyCaughtBySurfaceAt(0f*0f, 1f) +
                      "\n3m:" + explosionInfo.GetEnergyCaughtBySurfaceAt(3f*3f, 1f) +
                      "\n5m:" + explosionInfo.GetEnergyCaughtBySurfaceAt(5f*5f, 1f) +
                      "\n10m:" + explosionInfo.GetEnergyCaughtBySurfaceAt(10f*10f, 1f) +
                      "\n20m:" + explosionInfo.GetEnergyCaughtBySurfaceAt(20f*20f, 1f) +
                      "\n30m:" + explosionInfo.GetEnergyCaughtBySurfaceAt(30f*30f, 1f) +
                      "\n50m:" + explosionInfo.GetEnergyCaughtBySurfaceAt(50f*50f, 1f));
            photonView.RPC(nameof(CreateExplosionRpc), RpcTarget.All,
                explosionInfo.position, explosionInfo.blastRadius, explosionInfo.power_tnt, explosionInfo.energyFalloffExponent, explosionInfo.upwardsModifier);
        }

        [PunRPC]
        private void CreateExplosionRpc(Vector3 pos, float blastRadius, float power_tnt, float falloff, float upwardsModifier)
        {
            ExplosionInfo explosionInfo = new ExplosionInfo((pos, blastRadius, (double)power_tnt*ExplosionInfo.joulesPerKgTnt, falloff, upwardsModifier));
            
            float blastRadiusSqr = explosionInfo.blastRadius * explosionInfo.blastRadius;
            foreach (Rigidbody rb in GetRigidbodiesNear(pos, blastRadiusSqr))
            {
                explosionInfo.ApplyToRigidbody(rb);
            }
            onExplosion?.Invoke(explosionInfo);
            GameObject explosionEffect = Instantiate(GameManager.instance.gameplayReferences.explosionVFX);
            explosionEffect.transform.position = explosionInfo.position;
            float scale = explosionInfo.blastRadius / 245f;
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
        
        // /// <summary>
        // /// Estimates a cross-sectional surface area for this body given its bounding box.
        // /// </summary>
        // public float EstimateCrossSection(Transform body, Bounds boundingBox)
        // {
        //     Vector3 centre = boundingBox.center;
        //     float maxX = boundingBox.ClosestPoint(centre + body.right * 1000f).x;
        //     float minX = boundingBox.ClosestPoint(centre - body.right * 1000f).x;
        //     float maxY = boundingBox.ClosestPoint(centre + body.up * 1000f).y;
        //     float minY = boundingBox.ClosestPoint(centre - body.up * 1000f).y;
        //     float maxZ = boundingBox.ClosestPoint(centre + body.forward * 1000f).z;
        //     float minZ = boundingBox.ClosestPoint(centre - body.forward * 1000f).z;
        //     
        //     float longestSide = Mathf.Max(Mathf.Abs(maxX - minX), Mathf.Abs(maxY - minY), Mathf.Abs(maxZ - minZ));
        //     float area = longestSide; //let's just assume that the area is equal to the longest side. For human: about 1.8m^2, for 20m tall tree: 20m^2, etc.
        //
        //     Debug.Log("cross section of " + body + " = " + area, body);
        //     
        //     return area;
        // }
        
        /// <summary>
        /// Estimates a cross-sectional surface area for this body given its collider.
        /// </summary>
        public float EstimateCrossSection(Collider collider)
        {
            return collider switch
            {
                BoxCollider col => EstimateByLongestSide(col.size), //estimate area as longest side
                SphereCollider col => MaxScale()*2*Mathf.PI*col.radius*col.radius, //cross-sectional area always 2pi*r^2 no matter the angle
                CapsuleCollider col => Mathf.Max(col.height*col.transform.lossyScale.y, MaxScaleXZ()*col.radius*2), //estimate area as the height (guaranteed to be the longest side)
                WheelCollider col => 2*col.radius*col.transform.lossyScale.y, //estimate area as height (to account for less area when at an angle)
                MeshCollider col => EstimateFromLocalBounds(col.sharedMesh.bounds),
                _ => FallbackEstimate()
            };

            float MaxScale()
            {
                
                Vector3 lossyScale = collider.transform.lossyScale;
                return Mathf.Max(lossyScale.x, lossyScale.y, lossyScale.z);
            }

            float MaxScaleXZ()
            {
                Vector3 lossyScale = collider.transform.lossyScale;
                return Mathf.Max(lossyScale.x, lossyScale.z);
            }

            float EstimateFromLocalBounds(Bounds localBounds)
            {
                Transform colliderTransform = collider.transform;
                Vector3 size = localBounds.size;
                return Mathf.Max(size.x * colliderTransform.lossyScale.x, size.y * colliderTransform.lossyScale.y,
                    size.z * colliderTransform.lossyScale.z);
            }

            float EstimateByLongestSide(Vector3 localSize)
            {
                Vector3 lossyScale = collider.transform.lossyScale;
                localSize.Scale(lossyScale);
                return Mathf.Max(localSize.x, localSize.y, localSize.z);
            }

            float FallbackEstimate()
            {
                Vector3 size = collider.bounds.size;
                if (size == Vector3.zero)
                    size = collider.transform.lossyScale;
                return Mathf.Max(size.x, size.y, size.z);
            }
        }

        /// <summary>
        /// Estimates a cross-sectional surface area for this body given its size (for simple shapes).
        /// </summary>
        public float EstimateCrossSection(Vector3 size)
        {
            return Mathf.Max(size.x, size.y, size.z); //same assumption as in the first estimate function
        }

        /// <summary>
        /// Gets the world-space bounds of a collider, even if it is disabled
        /// </summary>
        public Bounds GetColliderBounds(Collider collider)
        {
            Bounds bounds = collider.bounds;
            if (bounds.extents == Vector3.zero)
            {
                Bounds localBounds = collider switch
                {
                    BoxCollider col => new Bounds(col.center, col.size),
                    SphereCollider col => new Bounds(col.center, Vector3.one*col.radius*2),
                    CapsuleCollider col => new Bounds(col.center, new Vector3(col.radius*2, Mathf.Max(col.height, col.radius*2), col.radius*2)),
                    WheelCollider col => new Bounds(col.center, new Vector3(col.radius*0.2f, col.radius*2, col.radius*2)),
                    MeshCollider col => col.sharedMesh.bounds,
                    _ => new Bounds(Vector3.zero, Vector3.one)
                };

                bounds = collider.transform.TransformBounds(localBounds);
            }

            return bounds;
        }
    }
}