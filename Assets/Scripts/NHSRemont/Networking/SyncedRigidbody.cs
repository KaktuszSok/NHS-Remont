using NHSRemont.Entity;
using NHSRemont.Environment.Fractures;
using NHSRemont.Utility;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Networking
{
    [RequireComponent(typeof(Rigidbody))]
    public class SyncedRigidbody : MonoBehaviour
    {
        public Rigidbody rb { get; private set; }

        private bool _isSimulatedLocally = false;
        public bool isSimulatedLocally
        {
            get => _isSimulatedLocally;
            set
            {
                _isSimulatedLocally = value;
                if(_isSimulatedLocally)
                    rb.isKinematic = false;
            }
        }

        private bool hadLastState = false;
        private NetworkedPhysicsState lastReceivedState;
        private float lag;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private float lastReceivedStateSpeed;
        private float lastReceivedStateAngularSpeed;
        private bool lastStateWasStill = true;
        private bool lerping = false;

        private Mesh mesh;

        private void Awake()
        {
            this.rb = this.GetOrAddComponent<Rigidbody>();
            isSimulatedLocally = PhotonNetwork.IsMasterClient;
        }

        private void Start()
        {
            mesh = GetComponent<MeshFilter>().sharedMesh;
        }

        private void Update()
        {
            if (!isSimulatedLocally)
            {
                if (lerping)
                {
                    float posErrorManhattan = VectorUtils.ManhattanDistance(transform.position, targetPosition);
                    Vector3 nowPosition = Vector3.MoveTowards(transform.position, targetPosition,
                        Mathf.Max(lastReceivedStateSpeed, posErrorManhattan*10f)*Time.deltaTime);
                    transform.position = nowPosition;
                    float posErrorSqr = (nowPosition - targetPosition).sqrMagnitude;

                    float rotError = Quaternion.Angle(transform.rotation, targetRotation);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 
                        Mathf.Max(lastReceivedStateAngularSpeed, rotError*10f)*Time.deltaTime);
                    rotError = Quaternion.Angle(transform.rotation, targetRotation);
                
                    if (posErrorSqr < NetworkedPhysicsState.maxPosErrorSqr && rotError < NetworkedPhysicsState.maxRotError)
                    {
                        lerping = false;
                        if(!lastStateWasStill) //simulate locally
                        {
                            rb.isKinematic = false;
                            lastReceivedState.To(rb, lag);
                        }
                    }
                }
            }
        }

        public void ReceivePhysicsState(NetworkedPhysicsState state, float lag)
        {
            lastReceivedState = state;
            this.lag = lag;
            if (!hadLastState)
            {
                hadLastState = true;
                state.To(rb, lag);
            }

            lastStateWasStill = state.IsStill();
            Vector3 targetEuler;
            (targetPosition, targetEuler) = state.GetPredictedTransformState(lag);
            targetRotation = Quaternion.Euler(targetEuler);
            lastReceivedStateSpeed = state.velocity.magnitude;
            lastReceivedStateAngularSpeed = state.angularVelocity.magnitude*Mathf.Rad2Deg;
            lerping = true;
            rb.isKinematic = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.rigidbody != null)
            {
                Health health = collision.rigidbody.GetComponent<Health>();
                if (health != null && PhotonView.Get(health).IsMine)
                {
                    health.TakeImpactDamage(collision.impulse.magnitude, collision.GetContact(0).point);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if(lerping)
                Gizmos.color = Color.green; //lerping
            else
            {
                if(rb.isKinematic)
                    Gizmos.color = Color.red; //frozen
                else
                    Gizmos.color = Color.white; //simulated locally
            }
        
            Gizmos.DrawWireMesh(mesh, transform.position, transform.rotation);
        }
    }
}
