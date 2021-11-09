using UnityEngine;

namespace NHSRemont.Utility
{
    public static class RectUtils
    {
        /// <summary>
        /// Alternative implementation of Contains which does not cast values to doubles
        /// </summary>
        /// <param name="rect">The rect which we check against</param>
        /// <param name="point">The point which we want to know if it is inside the rect</param>
        /// <returns></returns>
        public static bool FastContains(this Rect rect, Vector2 point)
        {
            return point.x >= rect.xMin && point.x < rect.xMax && point.y >= rect.yMin && point.y < rect.yMax;
        }
    }
}