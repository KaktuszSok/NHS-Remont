using UnityEngine;

namespace NHSRemont.Utility
{
    public static class ArrayUtils
    {
        public static T ChooseRandom<T>(this T[] arr)
        {
            return arr[Random.Range(0, arr.Length)];
        }
    }
}