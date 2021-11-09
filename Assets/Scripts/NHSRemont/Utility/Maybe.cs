namespace NHSRemont.Utility
{
    /// <summary>
    /// Same as Nullable but works for already nullable types T
    /// </summary>
    public readonly struct Maybe<T>
    {
        public readonly bool hasValue;
        internal readonly T value;

        public Maybe(T value)
        {
            hasValue = value != null;
            this.value = value;
        }

        public T GetValueOrDefault()
        {
            return hasValue ? value : default;
        }
    }
}