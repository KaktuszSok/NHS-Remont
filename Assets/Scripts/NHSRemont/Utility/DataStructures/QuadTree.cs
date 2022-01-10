using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using UnityEngine;

namespace NHSRemont.Utility
{
    /// <summary>
    /// A quadtree with equally sized rectangular quadrants
    /// </summary>
    /// <typeparam name="T">Type of data to store</typeparam>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class QuadTree<T> : IQuadTree<T>
    {
        private bool isEmpty => isEndNode && !hasElement;
        private bool isEndNode;
        private bool hasElement;
        public readonly Rect bounds;
        private (T contents, Vector2 pos) element;
        [CanBeNull] private QuadTree<T> NW;
        [CanBeNull] private QuadTree<T> NE;
        [CanBeNull] private QuadTree<T> SW;
        [CanBeNull] private QuadTree<T> SE;
        
        public QuadTree(Rect bounds)
        {
            this.bounds = bounds;
            isEndNode = true;
            hasElement = false;
        }

        /// <summary>
        /// Add an element to the quadtree at some position
        /// </summary>
        public bool Add(T element, Vector2 position)
        {
            if (!bounds.FastContains(position))
            {
                Debug.LogError(new ArgumentOutOfRangeException(nameof(position),
                    "Argument is out of bounds! (literally)").ToString());
                return false;
            }

            Add((element, position));
            return true;
        }
        
        /// <summary>
        /// A recursive add method which adds the element to the quadtree, subdividing if needed.
        /// If an element already exists at this position, it will be replaced.
        /// </summary>
        private void Add((T contents, Vector2 pos) element)
        {
            if (isEndNode)
            {
                if (hasElement)
                {
                    if (this.element.pos == element.pos)
                    {
                        this.element = element; //replace old element with new
                        return;
                    }
                    Subdivide();
                    GetAppropriateSubtreeForPosition(element.pos).Add(element);
                }
                else
                {
                    this.element = element;
                    hasElement = true;
                }
            }
            else
            {
                GetAppropriateSubtreeForPosition(element.pos).Add(element);
            }
        }

        public void Clear()
        {
            element = default;
            hasElement = false;
            isEndNode = true;
            NW = NE = SW = SE = null; //orphan subtrees, GC will take care of them
        }
        
        
        public Maybe<T> GetAtPosition(Vector2 pos)
        {
            QuadTree<T> sub = GetAppropriateSubtreeForPosition(pos);
            return sub?.GetAtPosition(pos) ?? new Maybe<T>(element.contents); //if sub is null, return our element (may be null), otherwise call recursively
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public Maybe<T> RemoveAtPosition(Vector2 pos)
        {
            QuadTree<T> sub = GetAppropriateSubtreeForPosition(pos);
            if (sub == null) //we are the leaf node containing the element to remove
            {
                T removedElement = element.contents;
                element = default;
                hasElement = false;
                return new Maybe<T>(removedElement);
            }
            // ReSharper disable once RedundantIfElseBlock
            else //we are not a leaf node
            {
                Maybe<T> returnValue = sub.RemoveAtPosition(pos); //recursive call
                if (NW.isEmpty && NE.isEmpty && SW.isEmpty && SE.isEmpty) //are all our subtrees empty?
                {
                    //if so,
                    NW = NE = SW = SE = null; //orphan child nodes
                    isEndNode = true; //we are now a leaf node (and since we were not a leaf, so have no element, we are also now empty)
                }

                return returnValue;
            }
        }
        
        public List<(T element, Vector2 position)> GetElementsWithinBounds(Rect bounds)
        {
            List<(T element, Vector2 position)> elements = new List<(T element, Vector2 position)>();
            CollectElementsInBoundsRecursively(bounds, elements);
            return elements;
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        private void CollectElementsInBoundsRecursively(Rect bounds, ICollection<(T element, Vector2 position)> collection)
        {
            if (hasElement) //leaf node with element
            {
                if(bounds.FastContains(element.pos))
                    collection.Add(element);
            }
            else if(!isEndNode) //non-leaf node
            {
                if(bounds.Overlaps(NW.bounds))
                    NW.CollectElementsInBoundsRecursively(bounds, collection);
                if(bounds.Overlaps(NE.bounds))
                    NE.CollectElementsInBoundsRecursively(bounds, collection);
                if(bounds.Overlaps(SW.bounds))
                    SW.CollectElementsInBoundsRecursively(bounds, collection);
                if(bounds.Overlaps(SE.bounds))
                    SE.CollectElementsInBoundsRecursively(bounds, collection);
            }
        }

        public List<T> GetAllElements()
        {
            var elements = new List<T>();
            CollectElementsRecursively(elements);
            return elements;
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        private void CollectElementsRecursively(ICollection<T> collection)
        {
            if (hasElement) //leaf node with element
            {
                collection.Add(element.contents);
            }
            else if (!isEndNode) //non-leaf node
            {
                NW.CollectElementsRecursively(collection);
                NE.CollectElementsRecursively(collection);
                SW.CollectElementsRecursively(collection);
                SE.CollectElementsRecursively(collection);
            }
        }

        private void Subdivide() //note: Never subdivide if hasElement is false!!!
        {
            Vector2 centre = bounds.center;
            NW = new QuadTree<T>(Rect.MinMaxRect(bounds.xMin, centre.y, centre.x, bounds.yMax));
            NE = new QuadTree<T>(Rect.MinMaxRect(centre.x, centre.y, bounds.xMax, bounds.yMax));
            SW = new QuadTree<T>(Rect.MinMaxRect(bounds.xMin, bounds.yMin, centre.x, centre.y));
            SE = new QuadTree<T>(Rect.MinMaxRect(centre.x, bounds.yMin, bounds.xMax, centre.y));
            
            //move element from self to appropriate subtree
            GetAppropriateSubtreeForPosition(element.pos).Add(element);
            element = default;
            hasElement = false;
            isEndNode = false;
        }

        [SuppressMessage("ReSharper", "RedundantIfElseBlock")]
        private QuadTree<T> GetAppropriateSubtreeForPosition(Vector2 pos)
        {
            if (pos.y < bounds.center.y)
            {
                return pos.x < bounds.center.x ? SW : SE;
            }
            else
            {
                return pos.x < bounds.center.x ? NW : NE;
            }
        }
    }
}