namespace TypeSystem
{
    class Box<T> where T: struct
    {
        public readonly object _value;
        public T Value => (T) _value;

        public Box(T t)
        {
            _value = t;
        }
    }
}