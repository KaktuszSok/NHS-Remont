using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C5;
using NHSRemont.Environment;
using NHSRemont.Environment.Fractures;
using NHSRemont.Utility;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NHSRemont.Gameplay.Projectiles
{
    [RequireComponent(typeof(Rigidbody))]
    public class BulletProjectile : MonoBehaviour, IProjectile
    {
        private record MediumTransition
        {
            /// <summary>
            /// Raycast hit info about this transition
            /// </summary>
            public RaycastHit hit;
            /// <summary>
            /// Did we enter or exit this medium?
            /// </summary>
            public bool isEntry;
            /// <summary>
            /// Distance from the bullet's position at the start of this physics tick
            /// </summary>
            public float distance;

            public override string ToString()
            {
                return $"(isEntry={isEntry}, distance={distance}, object={hit.collider.name})";
            }
        }

        private const int raycastHitsBufferSize = 99;
        private static readonly IComparer<MediumTransition> distCompare = new DelegateComparer<MediumTransition>(
            (r1, r2) => r1.distance.CompareTo(r2.distance));

        [Header("Bullet Settings")]
        [Tooltip("Penetration of this bullet, in mm of a wall material with a 1.0x penetration multiplier (think steel).")]
        public float penetration = 10f;
        public AnimationCurve damageByDistance = new AnimationCurve(new Keyframe[]
        {
            new(0, 40f),
            new(200f, 20f)
        });
        [Tooltip("Calibre of this bullet, in millimeters.")]
        public float calibre = 7.62f;
        public float holeSizeMult = 1f;
        
        //RUNTIME:
        private float muzzleVelocity;
        private float startPen;
        private Rigidbody rb;
        private TrailRenderer[] trails;
        private RaycastHit[] hitsBuffer = new RaycastHit[raycastHitsBufferSize];

        private Vector3 truePosition;
        private NHSWall currentWall;
        private float distTravelled = 0f;
        private bool destroyed = false;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            trails = GetComponentsInChildren<TrailRenderer>();
        }

        public void OnLaunched(float velocity)
        {
            truePosition = rb.position;
            muzzleVelocity = velocity;
            startPen = penetration;
            Debug.DrawRay(rb.position, Vector3.up*2f, Color.white, 15f);
        }

        public void SetOwned(bool ownedByLocalClient) { }

        private void FixedUpdate()
        {
            if(destroyed)
                return;
            
            IPriorityQueue<MediumTransition> transitions = new IntervalHeap<MediumTransition>(distCompare);

            float vel = rb.velocity.magnitude;
            Vector3 fwd = rb.velocity.normalized;
            float rayDist = vel * Time.fixedDeltaTime;

            //forward raycast
            Ray collisionRay = new Ray(rb.position, fwd);
            Debug.DrawRay(rb.position, fwd*rayDist, ColourUtils.RandomColour(), 15f);
            int hitsCount = Physics.RaycastNonAlloc(collisionRay, hitsBuffer, rayDist, GameManager.instance.gameplayReferences.bulletCollisionLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitsCount; i++)
            {
                transitions.Add(new MediumTransition()
                {
                    hit = hitsBuffer[i],
                    isEntry = true,
                    distance = hitsBuffer[i].distance
                });
            }
            
            //backward raycast
            collisionRay = new Ray(rb.position + rb.velocity*Time.fixedDeltaTime, -fwd);
            hitsCount = Physics.RaycastNonAlloc(collisionRay, hitsBuffer, rayDist, GameManager.instance.gameplayReferences.bulletCollisionLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitsCount; i++)
            {
                transitions.Add(new MediumTransition()
                {
                    hit = hitsBuffer[i],
                    isEntry = false,
                    distance = rayDist - hitsBuffer[i].distance
                });
            }

            float travelled = 0f;
            truePosition = rb.position;
            while (!transitions.IsEmpty)
            {
                MediumTransition transition = transitions.DeleteMin();
                Debug.Log("Processing transition: " + transition);
                TravelThroughCurrentMedium(transition.distance - travelled);
                if(destroyed)
                    return;
                
                NHSWall wall = transition.hit.collider.GetComponent<NHSWall>();
                if (wall)
                {
                    DoWallVisualsAndSFX(wall, transition);
                }
                
                if (transition.isEntry)
                {
                    truePosition = transition.hit.point;

                    float speedFraction = vel / muzzleVelocity;
                    float damage = damageByDistance.EvaluateClamped(distTravelled)*speedFraction;
                    foreach (IDamageListener damageListener in transition.hit.transform.GetComponents<IDamageListener>())
                    {
                        damageListener.OnBulletDamage(transition.hit, damage);
                    }
                    
                    if (wall != null)
                    {
                        currentWall = wall;
                    }
                    else
                    {
                        DestroyFromHittingCollider();
                        return;
                    }
                }
                else
                {
                    truePosition = transition.hit.point;
                    
                    currentWall = null;
                }

                travelled = transition.distance;
            }
            TravelThroughCurrentMedium(rayDist - travelled);
            
            void TravelThroughCurrentMedium(float distanceToTravel)
            {
                float trulyTravelled = distanceToTravel;
                if (currentWall != null)
                {
                    //lose penetration
                    float penLost = (distanceToTravel*1000f) * currentWall.material.penetrationMultiplier;
                    Debug.Log("lost " + penLost + "mm pen to wall " + currentWall.name + " over " + (distanceToTravel*1000f) + "mm distance");
                    float trulyLost = Mathf.Min(penLost, penetration);
                    penetration -= trulyLost;
                    
                    //advance true position
                    float fractionOfTravelCompleted = trulyLost / penLost;
                    trulyTravelled = distanceToTravel * fractionOfTravelCompleted;
                    truePosition += fwd * trulyTravelled;
                
                    //lose velocity
                    float lostFraction = trulyLost / startPen;
                    float velLost = muzzleVelocity * lostFraction;
                    float newVel = Mathf.Max(vel - velLost, 0f);
                    rb.velocity = fwd * newVel;

                    //check if destroyed by medium
                    if (penetration <= 0f)
                    {
                        DestroyFromPenetrationLoss();
                    }
                }
            
                distTravelled += trulyTravelled;
            }
        }

        /// <summary>
        /// Destroy this bullet due to it not being able to fully penetrate a wall
        /// </summary>
        public void DestroyFromPenetrationLoss()
        {
            DestroySelf();
        }

        /// <summary>
        /// Destroy this bullet due to it hitting a collider which does not support penetration
        /// </summary>
        public void DestroyFromHittingCollider()
        {
            DestroySelf();
        }

        public void DestroySelf()
        {
            if(destroyed)
                return;
            destroyed = true;

            rb.position = transform.position = truePosition;

            //don't destroy trail immediately
            foreach (TrailRenderer trail in trails)
            {
                var autoDestroy= trail.gameObject.AddComponent<Autodestroy>();
                autoDestroy.destroyTimer = 3f;
                autoDestroy.StartCoroutine(FixTrailRendererBug(trail));
                trail.transform.SetParent(null);
            }

            Destroy(gameObject);

            IEnumerator FixTrailRendererBug(TrailRenderer trail)
            {
                yield return new WaitForFixedUpdate();
                trail.transform.position += trail.transform.forward * -0.01f;
            }
        }

        /// <summary>
        /// Creates visual and sound effects for when the bullet enters or exits a wall
        /// </summary>
        private void DoWallVisualsAndSFX(NHSWall wall, MediumTransition transition)
        {
            Vector3 hitPoint = transition.hit.point;
            Vector3 outDirection = (transition.isEntry ? -rb.velocity : rb.velocity).normalized;
            Quaternion visualsRotation = Quaternion.LookRotation(outDirection);
            
            //SFX/VFX
            float volume = transition.isEntry ? 0.55f : 0.25f;
            wall.material.PlayImpactVFXAndSFX(hitPoint, visualsRotation, volume, 1.3f);
            
            //Bullet hole
            //TODO pooling, limit amount of holes
            GameObject bulletHole = Instantiate(
                wall.material.bulletHolePrefab, 
                hitPoint + outDirection*0.0015f, 
                Quaternion.LookRotation(transition.hit.normal), 
                transition.hit.collider.transform);
            bulletHole.transform.Rotate(0f, 0f, Random.value*360f, Space.Self); //random rotation
            
            Vector3 holeScale = wall.material.bulletHolePrefab.transform.localScale;
            bulletHole.transform.localScale = holeScale * holeSizeMult;
            DecalProjector decalProjector = bulletHole.GetComponent<DecalProjector>();
            if (decalProjector)
            {
                Vector3 decalSize = holeScale * holeSizeMult;
                decalSize.z = 0.005f;
                decalProjector.size = decalSize;
            }
            
            //make bullets stick to the correct(ish) chunk if the wall is fractured
            Fracturable fracturable = transition.hit.collider.GetComponent<Fracturable>();
            if (fracturable != null)
            {
                const float threshold = 1f;
                var possibleParents = fracturable.allChunks.Where(IsChunkPossibleParent).ToArray();

                ChunkNode trueParent = null;
                if (possibleParents.Length == 1)
                {
                    trueParent = possibleParents[0];
                }
                else
                {
                    float closest = float.PositiveInfinity;
                    foreach (ChunkNode possibleParent in possibleParents)
                    {
                        float sqDist = (hitPoint - possibleParent.meshCollider.ClosestPoint(hitPoint)).sqrMagnitude;
                        Debug.Log(possibleParent + " sqDist is " + sqDist, possibleParent);
                        if (sqDist < closest)
                        {
                            closest = sqDist;
                            trueParent = possibleParent;
                        }
                    }
                }

                if (trueParent != null)
                {
                    trueParent.breakOffCallbackLate += _ =>
                    {
                        bulletHole.transform.SetParent(trueParent.transform);
                    };
                }
                else
                {
                    Debug.LogWarning("Could not find true parent for bullet hole! (possible parents: " + possibleParents.Length + ")", bulletHole);
                }

                bool IsChunkPossibleParent(ChunkNode chunk)
                {
                    if (chunk == null || !chunk.frozen) return false;
                    
                    Bounds bounds = PhysicsManager.GetColliderBounds(chunk.meshCollider);
                    bounds.Expand(threshold);
                    Debug.Log(bounds);
                    return bounds.Contains(hitPoint);
                }
            }
        }
    }
}