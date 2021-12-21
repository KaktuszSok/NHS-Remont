using NHSRemont.Environment.Fractures;
using UnityEngine;

namespace NHSRemont.Environment
{
    public class NHSWall : MonoBehaviour
    {
        public WallMaterial material;

        public void CopyTo(GameObject target)
        {
            NHSWall copy = target.GetOrAddComponent<NHSWall>();
            copy.material = material;
        }
    }
}