using System;

public sealed class FastBitConverter
{
    private readonly ulong[] _ulongHelper = new ulong[1];
    private readonly int[] _intHelper = new int[1];
    private readonly float[] _floatHelper = new float[1];
    private readonly double[] _doubleHelper = new double[1];

    public static void WriteLittleEndian(byte[] buffer, int offset, ulong data)
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
        buffer[offset    ] = (byte)(data);
        buffer[offset + 1] = (byte)(data >> 8);
        buffer[offset + 2] = (byte)(data >> 16);
        buffer[offset + 3] = (byte)(data >> 24);
        buffer[offset + 4] = (byte)(data >> 32);
        buffer[offset + 5] = (byte)(data >> 40);
        buffer[offset + 6] = (byte)(data >> 48);
        buffer[offset + 7] = (byte)(data >> 56);
#endif
    }

    public static void WriteLittleEndian(byte[] buffer, int offset, int data)
    {
#if BIGENDIAN
        buffer[offset + 3] = (byte)(data);
        buffer[offset + 2] = (byte)(data >> 8);
        buffer[offset + 1] = (byte)(data >> 16);
        buffer[offset    ] = (byte)(data >> 24);
#else
        buffer[offset    ] = (byte)(data);
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
        buffer[offset    ] = (byte)(data);
        buffer[offset + 1] = (byte)(data >> 8);
#endif
    }

    public int GetBytes(byte[] bytes, int startIndex, double value)
    {
        _doubleHelper[0] = value;
        Buffer.BlockCopy(_doubleHelper, 0, _ulongHelper, 0, 8);
        WriteLittleEndian(bytes, startIndex, _ulongHelper[0]);
        return 8;
    }

    public int GetBytes(byte[] bytes, int startIndex, float value)
    {
        _floatHelper[0] = value;
        Buffer.BlockCopy(_floatHelper, 0, _intHelper, 0, 4);
        WriteLittleEndian(bytes, startIndex, _intHelper[0]);
        return 4;
    }

    public static int GetBytes(byte[] bytes, int startIndex, short value)
    {
        WriteLittleEndian(bytes, startIndex, value);
        return 2;
    }

    public static int GetBytes(byte[] bytes, int startIndex, ushort value)
    {
        WriteLittleEndian(bytes, startIndex, (short)value);
        return 2;
    }

    public static int GetBytes(byte[] bytes, int startIndex, int value)
    {
        WriteLittleEndian(bytes, startIndex, value);
        return 4;
    }

    public static int GetBytes(byte[] bytes, int startIndex, uint value)
    {
        WriteLittleEndian(bytes, startIndex, (int)value);
        return 4;
    }

    public static int GetBytes(byte[] bytes, int startIndex, long value)
    {
        WriteLittleEndian(bytes, startIndex, (ulong)value);
        return 8;
    }

    public static int GetBytes(byte[] bytes, int startIndex, ulong value)
    {
        WriteLittleEndian(bytes, startIndex, value);
        return 8;
    }
}
