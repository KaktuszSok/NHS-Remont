using UnityEngine;

namespace NHSRemont.Environment
{
    [RequireComponent(typeof(Collider))]
    public class TreeCollisionHandler : MonoBehaviour
    {
        /// <summary>
        /// if impulse.magnitude divided by treeMass is greater than this, the tree will be knocked down
        /// </summary>
        public const float maxWithstoodVelocityChange = 3.5f;
        private TreeOptimiser owner;
        private Transform collisionObjectTransform;
        /// <summary>
        /// The expected mass of this tree (should match mass of rigidbody that gets spawned by the TreeOptimiser)
        /// </summary>
        public float treeMass;

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 impulse = collision.impulse;
            float ratioSqr = (impulse / treeMass).sqrMagnitude;
            if (ratioSqr > maxWithstoodVelocityChange * maxWithstoodVelocityChange)
            {
                var fallingTrees = owner.DestroyTreesNear(collisionObjectTransform.position, 0.1f, true); //should be just 1 object
                foreach (Rigidbody fallingTree in fallingTrees)
                {
                    ContactPoint[] contacts = new ContactPoint[collision.contactCount];
                    collision.GetContacts(contacts);
                    for (int i = 0; i < contacts.Length; i++)
                    {
                        Vector3 totalMomentum = Vector3.Dot(collision.relativeVelocity, contacts[i].normal) * contacts[i].normal * collision.rigidbody.mass;
                        float combinedMass = treeMass + collision.rigidbody.mass;
                        Vector3 deltaVelocity = totalMomentum / combinedMass; //our velocity delta - we go from velocity of 0 to velocity after such a collision. Preservation of momentum says that totalMomentum = newVelocity*combinedMass. deltaVelocity = newVelocity - oldVelocity, where oldVelocity is zero, so deltaVelocity = newVelocity
                        fallingTree.AddForceAtPosition(deltaVelocity, contacts[i].point, ForceMode.VelocityChange);
                    }
                }
            }
        }

        /// <summary>
        /// Checks whether the given explosion should knock down the given tree
        /// </summary>
        public static bool DoesExplosionFellTree(ExplosionInfo explosionInfo, Vector3 treePosition, float treeHeight, float treeMass)
        {
            float sqrDist = (treePosition - explosionInfo.position).sqrMagnitude;
            if (sqrDist > explosionInfo.blastRadius*explosionInfo.blastRadius || explosionInfo.blastRadius < treeHeight) //tree must be within explosion radius, and the explosion radius must be at least the tree's height
                return false;

            float impulse = explosionInfo.CalculateImpulse(sqrDist, treeHeight, treeMass);
            return impulse/treeMass >= maxWithstoodVelocityChange;
        }

        /// <summary>
        /// Initialises this component for a tree collision object, making it fall down if impacted too hard.
        /// The component must be already added on the object beforehand! (To improve efficiency - component should be added to all children when creating the collision template)
        /// </summary>
        /// <param name="treeCollisionObject">The gameobject which holds all of the tree's colliders</param>
        /// <param name="treeOwner">The TreeOptimiser that the tree is a part of</param>
        /// <param name="treeMass">The mass of the whole tree</param>
        public static void MakeTreeFellableByImpact(Transform treeCollisionObject, TreeOptimiser treeOwner, float treeMass)
        {
            foreach (Transform child in treeCollisionObject)
            {
                TreeCollisionHandler component = child.GetComponent<TreeCollisionHandler>();
                component.owner = treeOwner;
                component.collisionObjectTransform = treeCollisionObject;
                component.treeMass = treeMass;
            }
        }

        /// <summary>
        /// Sets the mass for all collision handler components that are children of the given tree collision object.
        /// </summary>
        /// <param name="treeCollisionObject">The gameobject which holds all of the tree's colliders</param>
        /// <param name="treeMass">The mass of the whole tree</param>
        public static void SetTreeMass(Transform treeCollisionObject, float treeMass)
        {
            foreach (Transform child in treeCollisionObject)
            {
                child.GetComponent<TreeCollisionHandler>().treeMass = treeMass;
            }
        }
    }
}
