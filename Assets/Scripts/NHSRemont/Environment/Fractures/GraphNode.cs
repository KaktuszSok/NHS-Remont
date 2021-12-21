using System;
using System.Collections.Generic;
using System.Linq;
using NHSRemont.Gameplay;
using NHSRemont.Utility;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Environment.Fractures
{
    /// <summary>
    /// A node representing an object which can be broken off from a structure
    /// </summary>
    public abstract class GraphNode : MonoBehaviour, IDamageListener
    {
        private const float unfreezeImpulseDampening = 0.0f;
        public const float destroyImpulseFactor = 3f; //impulse required to destroy = breakOffImpulse*this
        
        //settings
        public new Collider collider { get; protected set; }
        [SerializeField, HideInInspector] private GraphNode[] savedNeighbours; //because unity can't serialise Serializables...
        public bool isAnchor;
        public float mass = 10f;
        /// <summary>
        /// Impulse required to break off this node.
        /// For explosions, the surface area is ignored (assumed as 1m^2)
        /// </summary>
        [ReadOnly]
        public float breakOffImpulse = 3f;
        public bool indestructible = false;

        //state
        public readonly ISet<GraphNode> neighbours = new HashSet<GraphNode>();
        public bool frozen = true;
        public bool destroyed { get; private set; } = false;

        /// <summary>
        /// Called when this chunk is broken off or destroyed, right before breakOffCallbackLate
        /// </summary>
        public Action<GraphNode> breakOffCallbackEarly;
        /// <summary>
        /// Called when this chunk is broken off or destroyed, right after breakOffCallbackEarly
        /// </summary>
        public Action<GraphNode> breakOffCallbackLate;
        /// <summary>
        /// Called when the node is violently destroyed (i.e. not by unloading scene etc)
        /// </summary>
        public Action<GraphNode> destroyedCallback;
        /// <summary>
        /// Lets us re-use an explosion's effects on this object without having to calculate it again.
        /// Not called if the node was frozen at the time of the explosion.
        /// </summary>
        public Action<GraphNode, Vector3, Vector3, float> explosionImpulseAndPointAndSqrDistForwarding;

        protected virtual void Awake()
        {
            if (collider == null)
                collider = GetComponent<Collider>();

            if (savedNeighbours != null)
            {
                neighbours.Clear();
                foreach (GraphNode savedNeighbour in savedNeighbours)
                    neighbours.Add(savedNeighbour);
            }
        }

        public void AddNeighbour(GraphNode neighbour)
        {
            neighbours.Add(neighbour);
        }

        public void RemoveNeighbour(GraphNode neighbour)
        {
            neighbours.Remove(neighbour);
        }
        
        public void SaveNeighbours()
        {
            savedNeighbours = neighbours.ToArray();
        }

        public void DestroySelf()
        {
            if(destroyed)
                return;
            
            destroyed = true;
            destroyedCallback?.Invoke(this);
            DetachFromNeighbours();
            Destroy(gameObject);
        }

        private void DetachFromNeighbours()
        {
            foreach (GraphNode neighbour in neighbours)
            {
                neighbour.RemoveNeighbour(this);
            }
            neighbours.Clear();
            breakOffCallbackEarly?.Invoke(this);
            breakOffCallbackEarly = null;
            breakOffCallbackLate?.Invoke(this);
            breakOffCallbackLate = null;
        }
        
        public virtual void OnCollisionEnter(Collision collision)
        {
            if(indestructible || !PhotonNetwork.IsMasterClient) return;
            
            ApplyImpulseAtPoint(collision.impulse, collision.GetContact(0).point);
        }

        public virtual void OnExplosion(ExplosionInfo explosionInfo)
        {
            if(indestructible || !PhotonNetwork.IsMasterClient || !explosionInfo.IsPointWithinBlastRadius(transform.position))
                return;

            (Vector3 impulse, Vector3 point, float sqrDist) = explosionInfo.CalculateImpulseAndPoint(transform, collider, mass);
            if (frozen)
            {
                float overpressure = explosionInfo.GetOverpressureAt(sqrDist);
                if (overpressure > breakOffImpulse / 1000f)
                {
                    Unfreeze().AddForceAtPosition(impulse*(1-unfreezeImpulseDampening), point, ForceMode.Impulse); //unfreeze from overpressure and apply physics
                }
                else
                {
                    return;
                }
            }
            else
            {
                explosionImpulseAndPointAndSqrDistForwarding?.Invoke(this, impulse, point, sqrDist);
            }
            
            ApplyImpulseAtPoint(impulse, point);
        }

        public virtual void ApplyImpulseAtPoint(Vector3 impulse, Vector3 point)
        {
            if(indestructible || !PhotonNetwork.IsMasterClient) return;
            
            float impulseToDestroy = breakOffImpulse * destroyImpulseFactor;
            if (impulse.sqrMagnitude >= impulseToDestroy * impulseToDestroy)
            {
                DestroySelf();
                return;
            }

            if (impulse.sqrMagnitude > breakOffImpulse * breakOffImpulse)
            {
                Unfreeze().AddForceAtPosition(impulse*(1-unfreezeImpulseDampening), point, ForceMode.Impulse);
            }
        }
        
        public virtual Rigidbody Unfreeze()
        {
            if(!frozen)
                return GetComponent<Rigidbody>();

            frozen = false;
            
            DetachFromNeighbours();
            
            return null;
        }    
    }
}