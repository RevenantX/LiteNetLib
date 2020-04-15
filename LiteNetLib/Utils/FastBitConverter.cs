using System;
using System.Runtime.InteropServices;

namespace LiteNetLib.Utils
{
    public static class FastBitConverter
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct ConverterHelperDouble
        {
            [FieldOffset(0)]
            public ulong Along;

            [FieldOffset(0)]
            public double Adouble;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct ConverterHelperFloat
        {
            [FieldOffset(0)]
            public int Aint;

            [FieldOffset(0)]
            public float Afloat;
        }

        private static void WriteLittleEndian(byte[] buffer, int offset, ulong data)
        {
#if BIGENDIAN
            buffer[offset + 7] = (byte)(data);
            buffer[offset + 6] = (byte)(data >> 8);
            buffer[offset + 5] = (byte)(data >> 16);
            buffer[offset + 4] = (byte)(data >> 24);
            buffer[offset + 3] = (byte)(data >> 32);
            buffer[offset + 2] = (byte)(data >> 40);
            buffer[offset + 1] = (byte)(data >> 48);
            buffer[offset    ] = (byte)(data >> 56);
#else
            buffer[offset] = (byte)(data);
            buffer[offset + 1] = (byte)(data >> 8);
            buffer[offset + 2] = (byte)(data >> 16);
            buffer[offset + 3] = (byte)(data >> 24);
            buffer[offset + 4] = (byte)(data >> 32);
            buffer[offset + 5] = (byte)(data >> 40);
            buffer[offset + 6] = (byte)(data >> 48);
            buffer[offset + 7] = (byte)(data >> 56);
#endif
        }

        private static void WriteLittleEndian(byte[] buffer, int offset, int data)
        {
#if BIGENDIAN
            buffer[offset + 3] = (byte)(data);
            buffer[offset + 2] = (byte)(data >> 8);
            buffer[offset + 1] = (byte)(data >> 16);
            buffer[offset    ] = (byte)(data >> 24);
#else
            buffer[offset] = (byte)(data);
            buffer[offset + 1] = (byte)(data >> 8);
            buffer[offset + 2] = (byte)(data >> 16);
            buffer[offset + 3] = (byte)(data >> 24);
#endif
        }

        public static void WriteLittleEndian(byte[] buffer, int offset, short data)
        {
#if BIGENDIAN
            buffer[offset + 1] = (byte)(data);
            buffer[offset    ] = (byte)(data >> 8);
#else
            buffer[offset] = (byte)(data);
            buffer[offset + 1] = (byte)(data >> 8);
#endif
        }

        public static void GetBytes(byte[] bytes, int startIndex, double value)
        {
            ConverterHelperDouble ch = new ConverterHelperDouble { Adouble = value };
            WriteLittleEndian(bytes, startIndex, ch.Along);
        }

        public static void GetBytes(byte[] bytes, int startIndex, float value)
        {
            ConverterHelperFloat ch = new ConverterHelperFloat { Afloat = value };
            WriteLittleEndian(bytes, startIndex, ch.Aint);
        }

        public static void GetBytes(byte[] bytes, int startIndex, short value)
        {
            WriteLittleEndian(bytes, startIndex, value);
        }

        public static void GetBytes(byte[] bytes, int startIndex, ushort value)
        {
            WriteLittleEndian(bytes, startIndex, (short)value);
        }

        public static void GetBytes(byte[] bytes, int startIndex, int value)
        {
            WriteLittleEndian(bytes, startIndex, value);
        }

        public static void GetBytes(byte[] bytes, int startIndex, uint value)
        {
            WriteLittleEndian(bytes, startIndex, (int)value);
        }

        public static void GetBytes(byte[] bytes, int startIndex, long value)
        {
            WriteLittleEndian(bytes, startIndex, (ulong)value);
        }

        public static void GetBytes(byte[] bytes, int startIndex, ulong value)
        {
            WriteLittleEndian(bytes, startIndex, value);
        }

#if NETCOREAPP2_1 || NETCOREAPP3_0 || NETSTANDARD2_1
        private static void WriteLittleEndian(Span<byte> bytes, ulong data)
        {
#if BIGENDIAN
            bytes[7] = (byte)(data);
            bytes[6] = (byte)(data >> 8);
            bytes[5] = (byte)(data >> 16);
            bytes[4] = (byte)(data >> 24);
            bytes[3] = (byte)(data >> 32);
            bytes[2] = (byte)(data >> 40);
            bytes[1] = (byte)(data >> 48);
            bytes[0] = (byte)(data >> 56);
#else
            bytes[0] = (byte)(data);
            bytes[1] = (byte)(data >> 8);
            bytes[2] = (byte)(data >> 16);
            bytes[3] = (byte)(data >> 24);
            bytes[4] = (byte)(data >> 32);
            bytes[5] = (byte)(data >> 40);
            bytes[6] = (byte)(data >> 48);
            bytes[7] = (byte)(data >> 56);
#endif
        }

        private static void WriteLittleEndian(Span<byte> bytes, int data)
        {
#if BIGENDIAN
            bytes[3] = (byte)(data);
            bytes[2] = (byte)(data >> 8);
            bytes[1] = (byte)(data >> 16);
            bytes[0] = (byte)(data >> 24);
#else
            bytes[0] = (byte)(data);
            bytes[1] = (byte)(data >> 8);
            bytes[2] = (byte)(data >> 16);
            bytes[3] = (byte)(data >> 24);
#endif
        }

        public static void WriteLittleEndian(Span<byte> bytes, short data)
        {
#if BIGENDIAN
            bytes[1] = (byte)(data);
            bytes[0] = (byte)(data >> 8);
#else
            bytes[0] = (byte)(data);
            bytes[1] = (byte)(data >> 8);
#endif
        }

        public static void GetBytes(Span<byte> bytes, double value)
        {
            ConverterHelperDouble ch = new ConverterHelperDouble { Adouble = value };
            WriteLittleEndian(bytes, ch.Along);
        }

        public static void GetBytes(Span<byte> bytes, float value)
        {
            ConverterHelperFloat ch = new ConverterHelperFloat { Afloat = value };
            WriteLittleEndian(bytes, ch.Aint);
        }

        public static void GetBytes(Span<byte> bytes, short value)
        {
            WriteLittleEndian(bytes, value);
        }

        public static void GetBytes(Span<byte> bytes, ushort value)
        {
            WriteLittleEndian(bytes, (short)value);
        }

        public static void GetBytes(Span<byte> bytes, int value)
        {
            WriteLittleEndian(bytes, value);
        }

        public static void GetBytes(Span<byte> bytes, uint value)
        {
            WriteLittleEndian(bytes, (int)value);
        }

        public static void GetBytes(Span<byte> bytes, long value)
        {
            WriteLittleEndian(bytes, (ulong)value);
        }

        public static void GetBytes(Span<byte> bytes, ulong value)
        {
            WriteLittleEndian(bytes, value);
        }
#endif
    }
}
