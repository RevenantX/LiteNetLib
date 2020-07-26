using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    internal static class WinSock
    {
        private const string LibName = "ws2_32.dll";
        
        [DllImport(LibName, SetLastError = true)]
        public static extern int recvfrom(
            IntPtr socketHandle,
            [In, Out] byte[] pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);
        
        [DllImport(LibName, SetLastError = true)]
        internal static extern int sendto(
            IntPtr socketHandle,
            [In] byte[] pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags,
            [In] byte[] socketAddress,
            [In] int socketAddressSize);
    }

    internal static class UnixSock
    {
        private const string LibName = "libc";
        
        [DllImport(LibName)]
        public static extern int recvfrom(
            IntPtr socketHandle,
            [In, Out] byte[] pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);
        
        [DllImport(LibName)]
        internal static extern int sendto(
            IntPtr socketHandle,
            [In] byte[] pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags,
            [In] byte[] socketAddress,
            [In] int socketAddressSize);
    }
    internal static class NativeSocket
    {
        public static readonly bool IsSupported;
        private static readonly bool UnixMode;

        public const int MaxAddrSize = 28;
        
        private const int AF_INET = 2;
        private const int AF_INET6 = 10;
        internal enum UnixSocketError
        {
            SUCCESS          = 0,
            EACCES           = 0x10002,
            EADDRINUSE       = 0x10003,
            EADDRNOTAVAIL    = 0x10004,
            EAFNOSUPPORT     = 0x10005,
            EAGAIN           = 0x10006,
            EALREADY         = 0x10007,
            EBADF            = 0x10008,
            ECANCELED        = 0x1000B,
            ECONNABORTED     = 0x1000D,
            ECONNREFUSED     = 0x1000E,
            ECONNRESET       = 0x1000F,
            EDESTADDRREQ     = 0x10011,
            EFAULT           = 0x10015,
            EHOSTUNREACH     = 0x10017,
            EINPROGRESS      = 0x1001A,
            EINTR            = 0x1001B,
            EINVAL           = 0x1001C,
            EISCONN          = 0x1001E,
            EMFILE           = 0x10021,
            EMSGSIZE         = 0x10023,
            ENETDOWN         = 0x10026,
            ENETRESET        = 0x10027,
            ENETUNREACH      = 0x10028,
            ENFILE           = 0x10029,
            ENOBUFS          = 0x1002A,
            ENOENT           = 0x1002D,
            ENOPROTOOPT      = 0x10033,
            ENOTCONN         = 0x10038,
            ENOTSOCK         = 0x1003C,
            ENOTSUP          = 0x1003D,
            ENXIO            = 0x1003F,
            EPERM            = 0x10042,
            EPIPE            = 0x10043,
            EPROTONOSUPPORT  = 0x10045,
            EPROTOTYPE       = 0x10046,
            ETIMEDOUT        = 0x1004D,
            ESOCKTNOSUPPORT  = 0x1005E,
            EPFNOSUPPORT     = 0x10060,
            ESHUTDOWN        = 0x1006C,
            EHOSTDOWN        = 0x10070,
            ENODATA          = 0x10071
        }
        
        private static readonly Dictionary<UnixSocketError, SocketError> NativeErrorToSocketError = new Dictionary<UnixSocketError, SocketError>(42)
        {
            { UnixSocketError.EACCES, SocketError.AccessDenied },
            { UnixSocketError.EADDRINUSE, SocketError.AddressAlreadyInUse },
            { UnixSocketError.EADDRNOTAVAIL, SocketError.AddressNotAvailable },
            { UnixSocketError.EAFNOSUPPORT, SocketError.AddressFamilyNotSupported },
            { UnixSocketError.EAGAIN, SocketError.WouldBlock },
            { UnixSocketError.EALREADY, SocketError.AlreadyInProgress },
            { UnixSocketError.EBADF, SocketError.OperationAborted },
            { UnixSocketError.ECANCELED, SocketError.OperationAborted },
            { UnixSocketError.ECONNABORTED, SocketError.ConnectionAborted },
            { UnixSocketError.ECONNREFUSED, SocketError.ConnectionRefused },
            { UnixSocketError.ECONNRESET, SocketError.ConnectionReset },
            { UnixSocketError.EDESTADDRREQ, SocketError.DestinationAddressRequired },
            { UnixSocketError.EFAULT, SocketError.Fault },
            { UnixSocketError.EHOSTDOWN, SocketError.HostDown },
            { UnixSocketError.ENXIO, SocketError.HostNotFound }, // not perfect, but closest match available
            { UnixSocketError.EHOSTUNREACH, SocketError.HostUnreachable },
            { UnixSocketError.EINPROGRESS, SocketError.InProgress },
            { UnixSocketError.EINTR, SocketError.Interrupted },
            { UnixSocketError.EINVAL, SocketError.InvalidArgument },
            { UnixSocketError.EISCONN, SocketError.IsConnected },
            { UnixSocketError.EMFILE, SocketError.TooManyOpenSockets },
            { UnixSocketError.EMSGSIZE, SocketError.MessageSize },
            { UnixSocketError.ENETDOWN, SocketError.NetworkDown },
            { UnixSocketError.ENETRESET, SocketError.NetworkReset },
            { UnixSocketError.ENETUNREACH, SocketError.NetworkUnreachable },
            { UnixSocketError.ENFILE, SocketError.TooManyOpenSockets },
            { UnixSocketError.ENOBUFS, SocketError.NoBufferSpaceAvailable },
            { UnixSocketError.ENODATA, SocketError.NoData },
            { UnixSocketError.ENOENT, SocketError.AddressNotAvailable },
            { UnixSocketError.ENOPROTOOPT, SocketError.ProtocolOption },
            { UnixSocketError.ENOTCONN, SocketError.NotConnected },
            { UnixSocketError.ENOTSOCK, SocketError.NotSocket },
            { UnixSocketError.ENOTSUP, SocketError.OperationNotSupported },
            { UnixSocketError.EPERM, SocketError.AccessDenied },
            { UnixSocketError.EPIPE, SocketError.Shutdown },
            { UnixSocketError.EPFNOSUPPORT, SocketError.ProtocolFamilyNotSupported },
            { UnixSocketError.EPROTONOSUPPORT, SocketError.ProtocolNotSupported },
            { UnixSocketError.EPROTOTYPE, SocketError.ProtocolType },
            { UnixSocketError.ESOCKTNOSUPPORT, SocketError.SocketNotSupported },
            { UnixSocketError.ESHUTDOWN, SocketError.Disconnecting },
            { UnixSocketError.SUCCESS, SocketError.Success },
            { UnixSocketError.ETIMEDOUT, SocketError.TimedOut },
        };

        static NativeSocket()
        {
            int temp = 0;
            IntPtr p = IntPtr.Zero;
            
            try
            {
                WinSock.recvfrom(p, null, 0, 0, null, ref temp);
                IsSupported = true;
            }
            catch
            {
                UnixMode = true;
            }

            if (UnixMode)
            {
                try
                {
                    UnixSock.recvfrom(p, null, 0, 0, null, ref temp);
                    IsSupported = true;
                }
                catch
                {
                    //do nothing
                }
            }
        }

        public static IPAddress GetIPAddress(byte[] buffer, byte[] localCache)
        {
            short family = BitConverter.ToInt16(buffer, 0);
            if ((UnixMode && family == AF_INET6) || (!UnixMode && (AddressFamily)family == AddressFamily.InterNetworkV6))
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

        private static readonly ThreadLocal<byte[]> TemporaryEpBuffer = new ThreadLocal<byte[]>(() => new byte[MaxAddrSize]);
        public static byte[] GetNativeEndPoint(IPEndPoint endPoint)
        {
            byte[] target = TemporaryEpBuffer.Value;
            bool ipv4 = endPoint.AddressFamily == AddressFamily.InterNetwork;
            if(UnixMode)
                FastBitConverter.GetBytes(target, 0, (short)(ipv4 ? AF_INET : AF_INET6));
            else
                FastBitConverter.GetBytes(target, 0, (short)endPoint.AddressFamily);
            target[2] = (byte)(0xff & (endPoint.Port >> 8));
            target[3] = (byte)(0xff & (endPoint.Port));

            if (ipv4)
            {
                long addr = endPoint.Address.Address;
                target[4] = (byte)(addr);
                target[5] = (byte)(addr >> 8);
                target[6] = (byte)(addr >> 16);
                target[7] = (byte)(addr >> 24);
            }
            else
            {
                byte[] addrBytes = endPoint.Address.GetAddressBytes();
                Buffer.BlockCopy(addrBytes, 0, target, 8, 16);
            }

            return target;
        }

        private static SocketError GetSocketError(int data)
        {
            if (UnixMode)
            {
                SocketError err;
                return !NativeErrorToSocketError.TryGetValue((UnixSocketError) data, out err) ? SocketError.SocketError : err;
            }
            return (SocketError) Marshal.GetLastWin32Error();
        }

        public static int ReceiveFrom(Socket s, byte[] buffer, int size, byte[] socketAddress)
        {
            int addressLength = socketAddress.Length;
            int bytesReceived = UnixMode
                ? UnixSock.recvfrom(s.Handle, buffer, size, 0, socketAddress, ref addressLength)
                : WinSock.recvfrom(s.Handle, buffer, size, 0, socketAddress, ref addressLength);
            if (bytesReceived == -1)
                throw new SocketException((int)GetSocketError(bytesReceived));
            return bytesReceived;
        }

        private static readonly ThreadLocal<byte[]> TemporaryBuffer = new ThreadLocal<byte[]>(() => new byte[NetConstants.MaxPacketSize]);
        public static int SendTo(Socket s, byte[] buffer, int start, int size, byte[] socketAddress)
        {
            if (start > 0)
            {
                Buffer.BlockCopy(buffer, start, TemporaryBuffer.Value, 0, size);
                buffer = TemporaryBuffer.Value;
            }
            int bytesSent = UnixMode
                ? UnixSock.sendto(s.Handle, buffer, size, 0, socketAddress, socketAddress.Length)
                : WinSock.sendto(s.Handle, buffer, size, 0, socketAddress, socketAddress.Length);
            if (bytesSent == -1)
                throw new SocketException((int)GetSocketError(bytesSent));
            return bytesSent;
        }
    }
}
