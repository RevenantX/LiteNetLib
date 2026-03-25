using System;
using System.Runtime.CompilerServices;
#if UNITY_ANDROID
using Unity.Collections.LowLevel.Unsafe;
#endif

namespace LiteNetLib.Utils
{
    public static class FastBitConverter
    {
        /// <summary>
        /// Converts a value of type <typeparamref name="T"/> into a byte array starting at the specified index.
        /// </summary>
        /// <typeparam name="T">The type of the value to convert. Must be an unmanaged/blittable type.</typeparam>
        /// <param name="bytes">The destination byte array.</param>
        /// <param name="startIndex">The zero-based index in <paramref name="bytes"/> at which to begin writing.</param>
        /// <param name="value">The value to be converted and written.</param>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown when the <paramref name="bytes"/> array is too small to contain the value at the given <paramref name="startIndex"/>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void GetBytes<T>(byte[] bytes, int startIndex, T value) where T : unmanaged
        {
            int size = sizeof(T);
            if (bytes.Length < startIndex + size)
                ThrowIndexOutOfRangeException();
#if NET8_0_OR_GREATER
            Unsafe.WriteUnaligned(ref bytes[startIndex], value);
#else
            fixed (byte* ptr = &bytes[startIndex])
            {
    #if UNITY_ANDROID
                // On some android systems, assigning *(T*)ptr throws a NRE if
                // the ptr isn't aligned (i.e. if Position is 1,2,3,5, etc.).
                // Here we have to use memcpy.
                //
                // => we can't get a pointer of a struct in C# without
                //    marshalling allocations
                // => instead, we stack allocate an array of type T and use that
                // => stackalloc avoids GC and is very fast. it only works for
                //    value types, but all blittable types are anyway.
                T* valueBuffer = stackalloc T[1] { value };
                UnsafeUtility.MemCpy(ptr, valueBuffer, size);
    #else
                *(T*)ptr = value;
    #endif
            }
#endif
        }

        private static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();
    }
}
