using Unity.Netcode;
using UnityEngine;

namespace NHSRemont.Entity
{
    public class Player : NetworkBehaviour
    {
        public Transform cameraTarget;
        
        private void Start()
        {
            if (IsLocalPlayer)
            {
                MainCamera.target = cameraTarget;
            }
        }

        public void Respawn(Transform spawnPoint)
        {
            transform.position = spawnPoint.position;
            var move = GetComponent<PlayerMovement>();
            move.SetCameraAngle(spawnPoint.eulerAngles);
            move.StopVelocity();
        }
    }
}
