using UnityEngine;

namespace NHSRemont.Utility
{
    /// <summary>
    /// Utility class for vector maths
    /// </summary>
    public static class VectorUtils
    {
        /// <summary>
        /// Returns a vector which is perpendicular to the given vector a.
        /// </summary>
        /// https://stackoverflow.com/a/43454629
        public static Vector3 Perpendicular(Vector3 a)
        {
            bool b0 = (a.x <  a.y) && (a.x <  a.z);
            bool b1 = (a.y <= a.x) && (a.y <  a.z);
            bool b2 = (a.z <= a.x) && (a.z <= a.y);

            return Vector3.Cross(a, new Vector3(b0 ? 0:1, b1 ? 0:1, b2 ? 0:1));
        }

        /// <summary>
        /// Returns the manhattan distance between two positions
        /// </summary>
        public static float ManhattanDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
        }

        /// <summary>
        /// Returns a vector with each component being the respective component of a divided by the respective component of b
        /// </summary>
        public static Vector3 Divide(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        }
    }
}