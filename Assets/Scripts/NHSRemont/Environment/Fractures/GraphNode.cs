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
    /// <summary>
    /// A node representing an object which can be broken off from a structure
    /// </summary>
    public abstract class GraphNode : MonoBehaviour, IDamageListener
    {
        private const float unfreezeImpulseDampening = 0.0f;
        public const float destroyImpulseFactor = 4f; //impulse required to destroy while frozen = breakOffImpulse*this
        
        //settings
        public new Collider collider { get; protected set; }
        [SerializeField, HideInInspector] private GraphNode[] savedNeighbours; //because unity can't serialise Serializables...
        public bool isAnchor;
        public float mass = 10f;
        /// <summary>
        /// Impulse required to break off this node.
        /// For explosions, the surface area is ignored (assumed as 1m^2)
        /// </summary>
        [ReadOnlyInEditor]
        public float breakOffImpulse = 3f;
        public bool indestructible = false;

        //state
        public readonly ISet<GraphNode> neighbours = new HashSet<GraphNode>();
        public bool frozen = true;
        public bool destroyed { get; private set; } = false;
        private float accumulatedImpulse = 0f;
        
        /// <summary>
        /// Called when this chunk is broken off or destroyed, right before breakOffCallbackLate
        /// </summary>
        public Action<GraphNode> breakOffCallbackEarly;
        /// <summary>
        /// Called when this chunk is broken off or destroyed, right after breakOffCallbackEarly
        /// </summary>
        public Action<GraphNode> breakOffCallbackLate;
        /// <summary>
        /// Called when the node is violently destroyed (i.e. not by unloading scene etc).
        /// The vector represents the velocity this chunk has at the moment of destruction (this includes the effect of the impulse that causes the destruction)
        /// </summary>
        public Action<GraphNode, Vector3> destroyedCallback;

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

        protected virtual void FixedUpdate()
        {
            //accumulate damage only if not frozen (otherwise, only accumulate over the period of one physics tick)
            if(frozen)
                accumulatedImpulse = 0f;
        }
        
        public void DestroySelf()
        {
            DestroySelf(GetVelocity());
        }

        /// <param name="expectedVelocity">The velocity this chunks expects to have at the moment of its destruction</param>
        public void DestroySelf(Vector3 expectedVelocity)
        {
            if(destroyed)
                return;
            
            destroyed = true;
            destroyedCallback?.Invoke(this, expectedVelocity);
            DetachFromNeighbours();
            Destroy(gameObject);
        }

        /// <summary>
        /// Destroys this node non-violently (does not fire destroyed callback)
        /// </summary>
        public void RemoveSelf()
        {
            if(destroyed)
                return;

            destroyed = true;
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
            
            OnImpulseAtPoint(collision.impulse, collision.GetContact(0).point);
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
            
            OnImpulseAtPoint(impulse, point);
        }

        public virtual void OnImpulseAtPoint(Vector3 impulse, Vector3 point)
        {
            if(indestructible || !PhotonNetwork.IsMasterClient) return;
            
            float impulseToDestroy = breakOffImpulse * destroyImpulseFactor;
            float impulseMag = impulse.magnitude;
            if(impulseMag < breakOffImpulse*0.15f)
                return; //impulse too weak to be significant - ignore it (and by extension don't accumulate)
            
            accumulatedImpulse += impulseMag;
            if (accumulatedImpulse >= impulseToDestroy)
            {
                Vector3 newVel = GetVelocity() + (impulse / mass);
                DestroySelf(newVel);
                return;
            }

            if (frozen && accumulatedImpulse > breakOffImpulse)
            {
                Unfreeze().AddForceAtPosition(impulse*(1-unfreezeImpulseDampening), point, ForceMode.Impulse);
            }
        }

        public void OnBulletDamage(RaycastHit hit, float damage)
        {
            //TODO
        }

        public virtual Rigidbody Unfreeze()
        {
            if(!frozen)
                return GetComponent<Rigidbody>();

            frozen = false;
            
            DetachFromNeighbours();
            
            return null;
        }
        
        public virtual Vector3 GetVelocity()
        {
            return Vector3.zero;
        }

        public virtual void WritePhysicsState(ref NetworkedPhysicsState state)
        {
            state.position = transform.position;
            state.rotation = transform.eulerAngles;
            state.velocity = GetVelocity();
            state.angularVelocity = Vector3.zero;
        }

        public virtual void ApplyPhysicsState(NetworkedPhysicsState state, float lag = 0f)
        {
            if (frozen)
                Unfreeze();

            (Vector3 predictedPosition, Vector3 predictedRotation) = state.GetPredictedTransformState(lag);
            transform.position = predictedPosition;
            transform.eulerAngles = predictedRotation;
        }
    }
}