using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHSRemont
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
        private readonly Dictionary<PhysObjectType, LinkedList<Rigidbody>> rigidbodies = new Dictionary<PhysObjectType, LinkedList<Rigidbody>>();
        private readonly Dictionary<PhysObjectType, int> maxObjectsAmount = new Dictionary<PhysObjectType, int>()
        {
            {PhysObjectType.NORMAL, -1},
            {PhysObjectType.DEBRIS_SMALL, 150},
            {PhysObjectType.DEBRIS_MEDIUM, 300},
            {PhysObjectType.DEBRIS_LARGE, 1500}
        };

        public Action<ExplosionInfo> onExplosion;

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
                    float blastRadius = Input.GetKey(KeyCode.LeftShift) ? 100f : 20f;
                    float blastForce = Input.GetKey(KeyCode.LeftShift) ? 60000f : 12000f;
                    
                    CreateExplosion(new ExplosionInfo(hit.point, blastRadius, blastForce, 4.3f));
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
                        rbCollection.Remove(node);
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
                        rbCollection.Remove(node);
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
                    explosionInfo.ApplyToRigidbody(rb);
                }
            }
            onExplosion?.Invoke(explosionInfo);
        }
    }
}