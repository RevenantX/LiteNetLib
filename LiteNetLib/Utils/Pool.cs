using System;
using System.Collections.Generic;

namespace LiteNetLib.Utils
{
    public sealed class PoolArray<T>
    {
        private readonly Dictionary<int, Stack<T[]>> _pool;

        public PoolArray()
        {
            _pool = new Dictionary<int, Stack<T[]>>();
        }

        public T[] Get(int arrayLength)
        {
            T[] array = null;
            lock (_pool)
            {
                Stack<T[]> stack;
                if (_pool.TryGetValue(arrayLength, out stack))
                {
                    if (stack.Count > 0)
                    {
                        array = stack.Pop();
                    }
                }
            }
            if (array == null)
            {
                array = new T[arrayLength];
            }
            return array;
        }

        public void Recycle(T[] element)
        {
            Array.Clear(element, 0, element.Length);
            lock (_pool)
            {
                Stack<T[]> stack;
                if (!_pool.TryGetValue(element.Length, out stack))
                {
                    stack = new Stack<T[]>();
                    _pool.Add(element.Length, stack);
                }
                stack.Push(element);
            }
        }
    }
}
