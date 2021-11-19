using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace NHSRemont.Gameplay
{
    public class PhysicsManager : MonoBehaviour
    {
        public static PhysicsManager instance;

        public enum PhysObjectType
        {
            NORMAL,
            DEBRIS_SMALL,
            DEBRIS_MEDIUM,
            DEBRIS_LARGE
        }
        private readonly Dictionary<PhysObjectType, int> maxObjectsAmount = new Dictionary<PhysObjectType, int>()
        {
            {PhysObjectType.NORMAL, -1},
            {PhysObjectType.DEBRIS_SMALL, 150},
            {PhysObjectType.DEBRIS_MEDIUM, 300},
            {PhysObjectType.DEBRIS_LARGE, 750}
        };
        private readonly Dictionary<PhysObjectType, LinkedList<Rigidbody>> rigidbodies = new Dictionary<PhysObjectType, LinkedList<Rigidbody>>();
        private readonly Dictionary<Rigidbody, float> rigidbodyCrossAreasCache = new Dictionary<Rigidbody, float>();

        public Action<ExplosionInfo> onExplosion;

        [SerializeField]
        private VisualEffect explosionVFX;

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

        private void Update()
        {
            //TODO remove test
            if (Input.GetMouseButtonDown(0))
            {
                Transform camTransform = Camera.main.transform;
                if (Physics.Raycast(camTransform.position, camTransform.forward, out RaycastHit hit, 4000f))
                {
                    float yield = 0.052f;
                    if (Input.GetKey(KeyCode.LeftShift)) yield = 1f;
                    else if (Input.GetKey(KeyCode.Tab)) yield = 6f;
                    else if (Input.GetKey(KeyCode.X)) yield = 500f;
                    else if (Input.GetKey(KeyCode.Delete)) yield = 27_000f;

                    Vector3 point = hit.point;
                    point -= camTransform.forward * 0.05f;
                    
                    CreateExplosion(new ExplosionInfo(point, yield, 0.2f));
                }
            }
        }

        public void RegisterRigidbody(Rigidbody rb, PhysObjectType classification)
        {
            var rbCollection = rigidbodies[classification];
            int maxCount = maxObjectsAmount[classification];
            if (maxCount != -1)
            {
                //limit amount of objects of each type
                while (rbCollection.Count > maxCount)
                {
                    var node = rbCollection.First;
                    Destroy(node.Value.gameObject);
                    rbCollection.Remove(node);
                    RemoveRigidbodyFromCaches(node.Value);
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
            float blastRadiusSqr = explosionInfo.blastRadius * explosionInfo.blastRadius;
            foreach (Rigidbody rb in GetAllRigidbodies())
            {
                if ((rb.position - explosionInfo.position).sqrMagnitude <= blastRadiusSqr)
                {
                    if(rb.tag.Equals("Player")) continue; //TODO remove once explosion testing is gone
                    explosionInfo.ApplyToRigidbody(rb);
                }
            }
            onExplosion?.Invoke(explosionInfo);
            GameObject explosionEffect = Instantiate(explosionVFX.gameObject);
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
    }
}