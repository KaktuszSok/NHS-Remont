using System;
using System.IO;
using Photon.Pun;
using Unity.Mathematics;
using UnityEngine;

namespace NHSRemont.Networking
{
    [Serializable]
    public struct NetworkedPhysicsState
    {
        /// <summary>
        /// Maximum difference in position between two states before they are considered different
        /// </summary>
        public const float maxPosError = 0.1f;
        public const float maxPosErrorSqr = maxPosError * maxPosError;
        /// <summary>
        /// Maximum velocity of a state before it is considered as moving
        /// </summary>
        public const float velocityStillnessThreshold = 0.05f;
        public const float velocityStillnessThresholdSqr = velocityStillnessThreshold * velocityStillnessThreshold;
        /// <summary>
        /// Maximum difference in rotation between two states before they are considered different (in degrees)
        /// </summary>
        public const float maxRotError = 2.5f;
        public const float angularVelocityStillnessThresholdSqr =
            (maxRotError * Mathf.Deg2Rad) * (maxRotError * Mathf.Deg2Rad);

        public enum Precision
        {
            /// <summary>
            /// Position, velocity and angular velocity are sent as 3 half-precision floats.
            /// Rotation is sent as 3 single-byte values (accurate to 1.4 degrees).
            /// Total weight: 3*(3*2) + 1*(3*1) = 21 bytes
            /// </summary>
            LOW,
            /// <summary>
            /// Position, rotation, velocity and angular velocity are all sent as 3 single-precision floats (Vector3).
            /// Total weight: 4*(3*4) = 48 bytes
            /// </summary>
            HIGH
        }

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

        public void Send(BinaryWriter writer, Precision precision = Precision.LOW)
        {
            switch (precision)
            {
                case Precision.LOW:
                    SendHalfVector(position);
                    SendByteVector(rotation);
                    SendHalfVector(velocity);
                    SendHalfVector(angularVelocity);
                    break;
                case Precision.HIGH:
                    writer.Write(position);
                    writer.Write(rotation);
                    writer.Write(velocity);
                    writer.Write(angularVelocity);
                    break;
            }

            void SendHalfVector(Vector3 vector)
            {
                writer.WriteHalf((half)vector.x);
                writer.WriteHalf((half)vector.y);
                writer.WriteHalf((half)vector.z);
            }

            void SendByteVector(Vector3 vector)
            {
                writer.Write(RotationAsByte(vector.x));
                writer.Write(RotationAsByte(vector.y));
                writer.Write(RotationAsByte(vector.z));

                byte RotationAsByte(float rotation)
                {
                    return (byte)((rotation / 360f)*256f);
                }
            }
        }

        public void Receive(BinaryReader reader, Precision precision = Precision.LOW)
        {
            switch (precision)
            {
                case Precision.LOW:
                    ReceiveHalfVector(ref position);
                    ReceiveByteVector(ref rotation);
                    ReceiveHalfVector(ref velocity);
                    ReceiveHalfVector(ref angularVelocity);
                    break;
                case Precision.HIGH:
                    position = reader.ReadVector3();
                    rotation = reader.ReadVector3();
                    velocity = reader.ReadVector3();
                    angularVelocity = reader.ReadVector3();
                    break;
            }
            
            void ReceiveHalfVector(ref Vector3 vector)
            {
                vector.x = reader.ReadHalf();
                vector.y = reader.ReadHalf();
                vector.z = reader.ReadHalf();
            }

            void ReceiveByteVector(ref Vector3 vector)
            {
                vector.x = RotationFromByte(reader.ReadByte());
                vector.y = RotationFromByte(reader.ReadByte());
                vector.z = RotationFromByte(reader.ReadByte());

                float RotationFromByte(byte rotation)
                {
                    return (rotation / 256f)*360f;
                }
            }
        }

        public override string ToString()
        {
            return "(pos=" + position + ", rot=" + rotation + ", vel=" + velocity + ", angVel=" + angularVelocity + ")";
        }

        /// <summary>
        /// Is this physics state considered "still"? (velocity/angular velocity are below a threshold)
        /// </summary>
        public bool IsStill()
        {
            return velocity.sqrMagnitude < velocityStillnessThresholdSqr && angularVelocity.sqrMagnitude < angularVelocityStillnessThresholdSqr;
        }
        
        /// <summary>
        /// Checks whether the new state has changed from the previous state
        /// </summary>
        public static bool ChangedFromPrevious(NetworkedPhysicsState newState, NetworkedPhysicsState prevState)
        {
            if (!newState.IsStill()) return true; //true if new state is not still
            
            return !prevState.IsStill() //true if new state is still but old state was not still
                || (newState.position - prevState.position).sqrMagnitude >= maxPosErrorSqr //true if both are still but position is different
                || Quaternion.Angle(
                    Quaternion.Euler(newState.rotation), 
                    Quaternion.Euler(prevState.rotation)
                    ) >= maxRotError; //true if both are still but rotation is different
        }
    }
}