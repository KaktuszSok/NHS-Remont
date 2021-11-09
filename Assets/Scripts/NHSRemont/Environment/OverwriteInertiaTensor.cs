using UnityEngine;

namespace NHSRemont.Environment
{
    /// <summary>
    /// Allows you to change the inertia tensor of the attached rigidbody.
    /// </summary>
    public class OverwriteInertiaTensor : MonoBehaviour
    {
        [SerializeField] private Vector3 tensor = Vector3.one;
        [SerializeField] private Vector3 tensorRotation = Vector3.zero;

        [Tooltip("If true, the magnitude of the inertia tensor will be preserved from the auto-calculated tensor.")]
        [SerializeField] private bool keepMagnitude = true;

        [SerializeField] private bool keepRotation = true;

        // Start is called before the first frame update
        void Start()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (keepMagnitude)
            {
                float magnitude = rb.inertiaTensor.magnitude;
                rb.inertiaTensor = tensor.normalized * magnitude;
            }
            else
            {
                rb.inertiaTensor = tensor;
            }

            if (!keepRotation)
                rb.inertiaTensorRotation = Quaternion.Euler(tensorRotation);
        }
    }
}
