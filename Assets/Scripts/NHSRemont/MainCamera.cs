using UnityEngine;

namespace NHSRemont
{
    public class MainCamera : MonoBehaviour
    {
        public static MainCamera instance;
        public static Transform target;
        
        void Awake()
        {
            instance = this;
        }

        void LateUpdate()
        {
            if (target != null)
            {
                transform.position = target.position;
                transform.rotation = target.rotation;
            }
        }
    }
}
