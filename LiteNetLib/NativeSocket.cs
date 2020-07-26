using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace LiteNetLib
{
    internal static class NativeSocket
    {
        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern int recvfrom(
            IntPtr socketHandle,
            [In, Out] byte[] pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);

        //[DllImport("libSystem.Native", EntryPoint = "SystemNative_ReceiveMessage")]
        //private static extern Error ReceiveMessage(SafeHandle socket, MessageHeader* messageHeader, SocketFlags flags, long* received);

        private static SocketError GetLastSocketError()
        {
            int win32Error = Marshal.GetLastWin32Error();
            return (SocketError)win32Error;
        }

        public static IPAddress GetIPAddress(byte[] buffer, byte[] localCache)
        {
            AddressFamily family = (AddressFamily)BitConverter.ToInt16(buffer, 0);
            if (family == AddressFamily.InterNetworkV6)
            {
                for (int i = 0; i < localCache.Length; i++)
                {
                    localCache[i] = buffer[8 + i];
                }
                uint scope = unchecked((uint)(
                    (buffer[27] << 24) +
                    (buffer[26] << 16) +
                    (buffer[25] << 8) +
                    (buffer[24])));
                return new IPAddress(localCache, scope);
            }
            long ipv4Addr = unchecked((uint)((buffer[4] & 0x000000FF) |
                                             (buffer[5] << 8 & 0x0000FF00) |
                                             (buffer[6] << 16 & 0x00FF0000) |
                                             (buffer[7] << 24)));
            return new IPAddress(ipv4Addr & 0x0FFFFFFFF);
        }

        public static void SetAddressFamily(byte[] buffer, AddressFamily family)
        {
#if BIGENDIAN
            buffer[0] = unchecked((byte)((int)family >> 8));
            buffer[1] = unchecked((byte)((int)family));
#else
            buffer[0] = unchecked((byte)((int)family));
            buffer[1] = unchecked((byte)((int)family >> 8));
#endif
        }

        public static SocketError ReceiveFrom(IntPtr handle, byte[] buffer, int size, SocketFlags socketFlags, byte[] socketAddress, ref int addressLength, out int bytesTransferred)
        {
            int bytesReceived = recvfrom(handle, buffer, size, socketFlags, socketAddress, ref addressLength);
            if (bytesReceived == (int)SocketError.SocketError)
            {
                bytesTransferred = 0;
                return GetLastSocketError();
            }
            bytesTransferred = bytesReceived;
            return SocketError.Success;
        }
    }
}
