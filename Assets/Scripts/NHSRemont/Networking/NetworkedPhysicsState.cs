using UnityEngine;

namespace NHSRemont.Networking
{
    public struct NetworkedPhysicsState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;

        public NetworkedPhysicsState From(Rigidbody rb)
        {
            position = rb.position;
            rotation = rb.rotation;
            velocity = rb.velocity;
            angularVelocity = rb.angularVelocity;
            
            return this;
        }

        public void To(Rigidbody rb)
        {
            rb.position = rb.transform.position = position;
            rb.rotation = rb.transform.rotation = rotation;
            rb.velocity = velocity;
            rb.angularVelocity = angularVelocity;
        }
    }
}