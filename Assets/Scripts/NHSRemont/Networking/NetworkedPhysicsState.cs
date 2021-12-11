using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Networking
{
    public struct NetworkedPhysicsState
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;

        public NetworkedPhysicsState From(Rigidbody rb)
        {
            position = rb.position;
            rotation = rb.transform.eulerAngles;

            if (!rb.IsSleeping())
            {
                velocity = rb.velocity;
                angularVelocity = rb.angularVelocity;
            }
            else
            {
                velocity = angularVelocity = Vector3.zero;
            }

            return this;
        }

        public void To(Rigidbody rb, float lag = 0f)
        {
            (Vector3 predictedPosition, Vector3 predictedRotation) = GetPredictedTransformState(lag);
            
            rb.position = rb.transform.position = predictedPosition;
            rb.transform.eulerAngles = predictedRotation;
            rb.velocity = rb.useGravity ? velocity + Physics.gravity*lag : velocity;
            rb.angularVelocity = angularVelocity;
        }

        public (Vector3 predictedPosition, Vector3 predictedRotation) GetPredictedTransformState(float lag)
        {
            return (position + velocity * lag, rotation + angularVelocity * lag);
        }

        public void Send(PhotonStream stream)
        {
            stream.SendNext(position);
            stream.SendNext(rotation);
            stream.SendNext(velocity);
            stream.SendNext(angularVelocity);
        }

        public void Receive(PhotonStream stream)
        {
            position = stream.ReceiveNext<Vector3>();
            rotation = stream.ReceiveNext<Vector3>();
            velocity = stream.ReceiveNext<Vector3>();
            angularVelocity = stream.ReceiveNext<Vector3>();
        }

        public override string ToString()
        {
            return "(pos=" + position + ", rot=" + rotation + ", vel=" + velocity + ", angVel=" + angularVelocity + ")";
        }
    }
}