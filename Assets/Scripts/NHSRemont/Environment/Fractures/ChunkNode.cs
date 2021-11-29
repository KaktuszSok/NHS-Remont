using System;
using System.Collections.Generic;
using System.Linq;
using NHSRemont.Gameplay;
using NHSRemont.Networking;
using NHSRemont.Utility;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Environment.Fractures
{
    public class ChunkNode : MonoBehaviour
    {
        private const float unfreezeImpulseDampening = 0.0f;
        private const float unfreezeObjectScaling = 0.925f;
        private const float destroyImpulseFactor = 5f; //impulse required to destroy = maxImpulse*this

        public new MeshCollider collider { get; private set; }
        public SyncedRigidbody syncedRb;
        
        [SerializeField]
        private SerialisableMesh savedMesh;
        public float mass = 10f;
        [SerializeField, HideInInspector]
        private float maxImpulse = 3f;
        public bool isAnchor = false;
        [SerializeField, HideInInspector]
        private PhysicsManager.PhysObjectType category;
        [SerializeField, HideInInspector] private ChunkNode[] savedNeighbours; //because unity can't serialise Serializables...
       
        private HashSet<ChunkNode> neighbours = new HashSet<ChunkNode>();
        public bool frozen = true;
        public Color Color { get; set; } = Color.black; //for debug

        /// <summary>
        /// Called when this chunk is broken off or destroyed
        /// </summary>
        public Action<ChunkNode> breakOffCallback;

        private void Awake()
        {
            if (collider == null)
                collider = GetComponent<MeshCollider>();

            if (savedMesh != null)
            {
                var mesh = savedMesh.CreateMesh();
                collider.sharedMesh = mesh;
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            if (savedNeighbours != null)
            {
                neighbours = new HashSet<ChunkNode>(savedNeighbours);
            }
        }

        public void SetCollider(MeshCollider collider)
        {
            this.collider = collider;
            this.savedMesh = new SerialisableMesh(collider.sharedMesh);
        }

        public void SetPhysicsDetails(float maxImpulse, PhysicsManager.PhysObjectType category)
        {
            this.maxImpulse = maxImpulse;
            this.category = category;
        }

        public void SaveNeighbours()
        {
            savedNeighbours = neighbours.ToArray();
        }

        public void AddNeighbour(ChunkNode neighbour)
        {
            neighbours.Add(neighbour);
        }

        public void RemoveNeighbour(ChunkNode neighbour)
        {
            neighbours.Remove(neighbour);
        }

        public IEnumerable<ChunkNode> GetAllNeighbours()
        {
            return neighbours;
        }

        private void OnDestroy()
        {
            DetachFromNeighbours();
        }

        public void OnCollisionEnter(Collision collision)
        {
            if(!PhotonNetwork.IsMasterClient) return;
            
            ApplyImpulseAtPoint(collision.impulse, collision.GetContact(0).point);
        }

        public void OnExplosion(ExplosionInfo explosionInfo)
        {
            if(!PhotonNetwork.IsMasterClient || !explosionInfo.IsPointWithinBlastRadius(transform.position))
                return;

            (Vector3 impulse, Vector3 point) = explosionInfo.CalculateImpulseAndPoint(transform, collider.bounds, mass);
            ApplyImpulseAtPoint(impulse, point);
        }

        public void ApplyImpulseAtPoint(Vector3 impulse, Vector3 point)
        {
            if(!PhotonNetwork.IsMasterClient) return;
            
            float impulseToDestroy = maxImpulse * destroyImpulseFactor;
            if (impulse.sqrMagnitude >= impulseToDestroy * impulseToDestroy)
            {
                Destroy(gameObject);
                return;
            }
            
            if(!frozen) return;

            if (impulse.sqrMagnitude > maxImpulse * maxImpulse)
            {
                Unfreeze().AddForceAtPosition(impulse*(1-unfreezeImpulseDampening), point, ForceMode.Impulse);
            }
        }

        public Rigidbody Unfreeze()
        {
            if(!frozen)
                return GetComponent<Rigidbody>();

            frozen = false;
            
            DetachFromNeighbours();
            transform.localScale *= unfreezeObjectScaling;
            
            syncedRb = gameObject.AddComponent<SyncedRigidbody>();
            syncedRb.rb.mass = mass;
            PhysicsManager.instance.RegisterRigidbody(syncedRb.rb, category);

            transform.SetParent(PhysicsManager.instance.transform);

            return syncedRb.rb;
        }

        private void DetachFromNeighbours()
        {
            foreach (ChunkNode neighbour in neighbours)
            {
                neighbour.RemoveNeighbour(this);
            }
            neighbours.Clear();
            breakOffCallback?.Invoke(this);
            breakOffCallback = null;
        }

        private void OnDrawGizmos()
        {
            if (isAnchor)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.position, 0.05f);
            }
            else
            {
                Gizmos.color = Color.SetAlpha(0.5f);
                Gizmos.DrawSphere(transform.position, 0.1f);
            }
            
            foreach (var neighbour in neighbours)
            {
                var from = transform.position;
                var to = neighbour.transform.position;
                Gizmos.color = Color;
                Gizmos.DrawLine(from, to);
            }
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var node in neighbours)
            {
                var mesh = node.GetComponent<MeshFilter>().mesh;
                Gizmos.color = Color.yellow.SetAlpha(.2f);
                Gizmos.DrawMesh(mesh, node.transform.position, node.transform.rotation);
            }
        }
    }
}