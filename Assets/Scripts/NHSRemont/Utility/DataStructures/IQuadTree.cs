using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace NHSRemont.Utility
{
    public interface IQuadTree<T>
    {
        bool Add(T element, Vector2 position);

        void Clear();

        /// <summary>
        /// Retrieves the element at this position (may return null or a nearby element if there is no element exactly at the specified position)
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        Maybe<T> GetAtPosition(Vector2 pos);

        /// <summary>
        /// Attempts to remove the element at this position.
        /// If no element exists exactly at this position, nothing may get removed or a nearby element may be removed.
        /// </summary>
        /// <param name="pos">The position of the element to remove</param>
        /// <returns>The element that has been removed (may be null)</returns>
        Maybe<T> RemoveAtPosition(Vector2 pos);

        /// <summary>
        /// Gets all elements inside the given bounds, as well as their positions
        /// </summary>
        List<(T element, Vector2 position)> GetElementsWithinBounds(Rect bounds);

        /// <summary>
        /// Gets all elements in the quad tree
        /// </summary>
        List<T> GetAllElements();
    }
}