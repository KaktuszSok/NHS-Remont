using System.Collections.Generic;
using NHSRemont.Environment.Fractures;
using UnityEngine;

namespace NHSRemont.Utility
{
    public static class BoundsUtils
    {
        //http://answers.unity.com/answers/872449/view.html
        /// <summary>
        /// Transforms the given bounds from local space to world space
        /// </summary>
        public static Bounds TransformBounds(this Transform self, Bounds bounds)
        {
            var center = self.TransformPoint(bounds.center);
            var points = bounds.GetCorners();
 
            var result = new Bounds(center, Vector3.zero);
            foreach (var point in points)
                result.Encapsulate(self.TransformPoint(point));
            return result;
        }
        
        /// <summary>
        /// Transforms the given bounds from world space to local space
        /// </summary>
        public static Bounds InverseTransformBounds(this Transform self, Bounds bounds)
        {
            var center = self.InverseTransformPoint(bounds.center);
            var points = bounds.GetCorners();
 
            var result = new Bounds(center, Vector3.zero);
            foreach (var point in points)
                result.Encapsulate(self.InverseTransformPoint(point));
            return result;
        }
 
        // bounds
        public static List<Vector3> GetCorners(this Bounds obj, bool includePosition = true)
        {
            var result = new List<Vector3>();
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                result.Add((includePosition ? obj.center : Vector3.zero) + (obj.size / 2).Multiply(new Vector3(x, y, z)));
            return result;
        }
    }
}