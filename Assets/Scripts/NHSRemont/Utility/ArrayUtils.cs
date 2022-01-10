using System.Collections.Generic;
using UnityEngine;

namespace NHSRemont.Utility
{
    public static class ArrayUtils
    {
        public static T ChooseRandom<T>(this T[] arr)
        {
            return arr[Random.Range(0, arr.Length)];
        }

        public static string ToStringDetailed<T>(this ICollection<T> arr)
        {
            return $"[#={arr.Count} | {string.Join(", ", arr)}]";
        }
    }
}