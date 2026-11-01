using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Net.Sockets;

namespace LiteNetLib
{
    internal readonly struct NativeEndpointKey : IEquatable<NativeEndpointKey>
    {
        private readonly AddressFamily _addressFamily;
        private readonly uint _address0;
        private readonly uint _address1;
        private readonly uint _address2;
        private readonly uint _address3;
        private readonly uint _scope;
        private readonly ushort _port;

        private NativeEndpointKey(AddressFamily addressFamily, uint address0, uint address1, uint address2, uint address3, uint scope, ushort port)
        {
            _addressFamily = addressFamily;
            _address0 = address0;
            _address1 = address1;
            _address2 = address2;
            _address3 = address3;
            _scope = scope;
            _port = port;
        }

        internal static NativeEndpointKey FromNativeAddress(byte[] address)
        {
            short family = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? (short)((address[1] << 8) | address[0])
                : BitConverter.ToInt16(address, 0);
            ushort port = (ushort)((address[2] << 8) | address[3]);
            bool isIPv6 = NativeSocket.UnixMode
                ? family == NativeSocket.AF_INET6
                : (AddressFamily)family == AddressFamily.InterNetworkV6;
            return isIPv6
                ? new NativeEndpointKey(
                    AddressFamily.InterNetworkV6,
                    ReadUInt32LE(address, 8),
                    ReadUInt32LE(address, 12),
                    ReadUInt32LE(address, 16),
                    ReadUInt32LE(address, 20),
                    BitConverter.ToUInt32(address, 24),
                    port)
                : new NativeEndpointKey(AddressFamily.InterNetwork, ReadUInt32LE(address, 4), 0, 0, 0, 0, port);
        }

        internal static NativeEndpointKey FromSocketAddress(SocketAddress socketAddress)
        {
            ushort port = (ushort)((socketAddress[2] << 8) | socketAddress[3]);
            if (socketAddress.Family == AddressFamily.InterNetworkV6)
            {
                return new NativeEndpointKey(
                    AddressFamily.InterNetworkV6,
                    ReadUInt32LE(socketAddress, 8),
                    ReadUInt32LE(socketAddress, 12),
                    ReadUInt32LE(socketAddress, 16),
                    ReadUInt32LE(socketAddress, 20),
                    ReadUInt32NativeEndian(socketAddress, 24),
                    port);
            }
            else if (socketAddress.Family != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Socket address must be IPv4 or IPv6.", nameof(socketAddress));
            }

            return new NativeEndpointKey(AddressFamily.InterNetwork, ReadUInt32LE(socketAddress, 4), 0, 0, 0, 0, port);
        }

        internal static NativeEndpointKey FromEndPoint(IPEndPoint endPoint)
        {
            Span<byte> address = stackalloc byte[16];
            endPoint.Address.TryWriteBytes(address, out _);
            var af = endPoint.Address.AddressFamily;
            return new NativeEndpointKey(
                af,
                ReadUInt32LE(address, 0),
                af == AddressFamily.InterNetworkV6 ? ReadUInt32LE(address, 4) : 0,
                af == AddressFamily.InterNetworkV6 ? ReadUInt32LE(address, 8) : 0,
                af == AddressFamily.InterNetworkV6 ? ReadUInt32LE(address, 12) : 0,
                af == AddressFamily.InterNetworkV6 ? (uint)endPoint.Address.ScopeId : 0,
                (ushort)endPoint.Port);
        }

        internal IPEndPoint ToEndPoint()
        {
            Span<byte> address = stackalloc byte[16];
            WriteUInt32LE(address, 0, _address0);
            if (_addressFamily == AddressFamily.InterNetworkV6)
            {
                WriteUInt32LE(address, 4, _address1);
                WriteUInt32LE(address, 8, _address2);
                WriteUInt32LE(address, 12, _address3);
                return new IPEndPoint(new IPAddress(address, _scope), _port);
            }

            return new IPEndPoint(new IPAddress(address[..4]), _port);
        }

        public bool Equals(NativeEndpointKey other) =>
            _addressFamily == other._addressFamily &&
            _address0 == other._address0 &&
            _address1 == other._address1 &&
            _address2 == other._address2 &&
            _address3 == other._address3 &&
            _scope == other._scope &&
            _port == other._port;

        public override int GetHashCode() =>
            HashCode.Combine(_addressFamily, _address0, _address1, _address2, _address3, _scope, _port);

        private static uint ReadUInt32LE(ReadOnlySpan<byte> bytes, int offset) =>
            (uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);

        private static uint ReadUInt32LE(SocketAddress address, int offset) =>
            (uint)(address[offset] | address[offset + 1] << 8 | address[offset + 2] << 16 | address[offset + 3] << 24);

        private static uint ReadUInt32NativeEndian(SocketAddress address, int offset) => BitConverter.IsLittleEndian
            ? ReadUInt32LE(address, offset)
            : (uint)(address[offset] << 24 | address[offset + 1] << 16 | address[offset + 2] << 8 | address[offset + 3]);

        private static void WriteUInt32LE(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
