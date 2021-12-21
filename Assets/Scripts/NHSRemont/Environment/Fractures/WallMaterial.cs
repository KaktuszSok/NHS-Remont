using NHSRemont.Utility;
using UnityEngine;

namespace NHSRemont.Environment.Fractures
{
    [CreateAssetMenu(fileName = "New Wall Material", menuName = "NHS/Wall Material")]
    public class WallMaterial : ScriptableObject
    {
        public float density = 2400;
        [Tooltip("How much impulse per unit density can chunks withstand before being broken off?")]
        public float internalStrength = 5f;

        public Material outsideMaterial;
        public Material insideMaterial;
        public SFXCollection breakOffSound;
        public SFXCollection hardImpulseSound;
        public SFXCollection destroySound;

        /// <summary>
        /// Automatically sets the outside and inside materials if they are null
        /// </summary>
        /// <param name="gameObject">The gameobject to source the materials from, using its renderer</param>
        public void AutoDetectMaterials(GameObject gameObject)
        {
            if (outsideMaterial == null)
            {
                outsideMaterial = gameObject.GetComponent<Renderer>()?.sharedMaterial;
            }

            if (insideMaterial == null)
            {
                Renderer rend = gameObject.GetComponent<Renderer>();
                if (!rend)
                {
                    insideMaterial = outsideMaterial;
                }
                else
                {
                    var mats = rend.sharedMaterials;
                    insideMaterial = mats.Length > 1 ? mats[1] : mats[0];
                }
            }
        }
    }
}