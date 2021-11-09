using UnityEngine;

namespace NHSRemont.Utility
{
    public class Autodestroy : MonoBehaviour
    {

        public float destroyTimer = -6969f;

        private void Update()
        {
            if (destroyTimer > 0 && destroyTimer != -6969f)
            {
                destroyTimer -= Time.deltaTime;
                if (destroyTimer <= 0)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}