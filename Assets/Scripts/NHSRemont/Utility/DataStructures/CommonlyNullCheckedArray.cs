using System.Collections;
using System.Collections.Generic;

namespace NHSRemont.Utility
{
    /// <summary>
    /// An array which we expect to commonly check if some of its elements are null
    /// </summary>
    /// <typeparam name="T">The type of element to store in the array</typeparam>
    public class CommonlyNullCheckedArray1D<T> : IEnumerable<T>
    {
        private readonly T[] array;
        private readonly bool[] hasElementAt;

        public T this[int i]
        {
            get => array[i];
            set
            {
                array[i] = value;
                hasElementAt[i] = value != null;
            }
        }

        public CommonlyNullCheckedArray1D(int size)
        {
            array = new T[size];
            hasElementAt = new bool[size];
        }
        
        public bool NotNull(int i)
        {
            return hasElementAt[i];
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)array).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    
    /// <summary>
    /// A 3-dimensional array which we expect to commonly check if some of its elements are null
    /// </summary>
    /// <typeparam name="T">The type of element to store in the array</typeparam>
    public class CommonlyNullCheckedArray3D<T> : IEnumerable<T>
    {
        private readonly T[,,] array;
        private readonly bool[,,] hasElementAt;

        public T this[int i, int j, int k]
        {
            get => array[i,j,k];
            set
            {
                array[i,j,k] = value;
                hasElementAt[i,j,k] = value != null;
            }
        }

        public CommonlyNullCheckedArray3D(int size1, int size2, int size3)
        {
            array = new T[size1, size2, size3];
            hasElementAt = new bool[size1, size2, size3];
        }
        
        public bool NotNull(int i, int j, int k)
        {
            return hasElementAt[i,j,k];
        }
        
        private IEnumerable<T> arrayEnumerable
        {
            get
            {
                foreach (T e in array)
                    yield return e;
            }
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            return arrayEnumerable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}