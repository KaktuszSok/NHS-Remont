using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NHSRemont.Utility
{
    public static class ComponentUtils
    {
        /// <summary>
        /// Gets components of type T in children, grandchildren, etc recursively, but stops a path if it finds a component of type U.
        /// </summary>
        /// <param name="gameObject">The gameobject to search for components</param>
        /// <param name="allowTerminateOnRoot">If an object of type U is present on the root gameobject, should the function terminate?</param>
        /// <typeparam name="T">The type of component to search for</typeparam>
        /// <typeparam name="U">The type of component to terminate a path if found</typeparam>
        /// <returns>
        /// All components of type T attached to descendants of the given gameobject (including the gameobject itself),
        /// minus those who have a component of type U in the hierarchical path between the given gameobject and itself.
        /// </returns>
        public static IEnumerable<T> GetComponentsInChildrenTerminating<T, U>(this GameObject gameObject, bool allowTerminateOnRoot = true)
        {
            if(allowTerminateOnRoot && gameObject.GetComponent<U>() != null)
                return Enumerable.Empty<T>();

            IEnumerable<T> result = gameObject.GetComponents<T>();
            foreach (Transform child in gameObject.transform)
            {
                result = result.Concat(child.gameObject.GetComponentsInChildrenTerminating<T, U>(true)); //recursively call function on children and add to the result
            }

            return result;
        }
    }
}