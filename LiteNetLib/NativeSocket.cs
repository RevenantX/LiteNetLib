using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace LiteNetLib
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct TimeValue
    {
        public int Seconds;
        public int Microseconds;
    }

    internal static
#if LITENETLIB_UNSAFE
        unsafe
#endif
        class WinSock
    {
        private const string LibName = "ws2_32.dll";
        
        [DllImport(LibName, SetLastError = true)]
        public static extern int recvfrom(
            IntPtr socketHandle,
#if LITENETLIB_UNSAFE
            byte* pinnedBuffer,
#else
            [In, Out] byte[] pinnedBuffer,
#endif
            [In] int len,
            [In] SocketFlags socketFlags,
#if LITENETLIB_UNSAFE
            byte* socketAddress,
#else
            [Out] byte[] socketAddress,
#endif
            [In, Out] ref int socketAddressSize);
        
        [DllImport(LibName, SetLastError = true)]
        internal static extern int sendto(
            IntPtr socketHandle,
#if LITENETLIB_UNSAFE
            byte* pinnedBuffer,
#else
            [In] byte[] pinnedBuffer,
#endif
            [In] int len,
            [In] SocketFlags socketFlags,
#if LITENETLIB_UNSAFE
            byte* socketAddress,
#else
            [In] byte[] socketAddress,
#endif
            [In] int socketAddressSize);

        [DllImport(LibName, SetLastError = true)]
        internal static extern int select(
            [In] int ignoredParameter,
#if LITENETLIB_UNSAFE
            IntPtr* readfds,
            IntPtr* writefds,
            IntPtr* exceptfds,
#else
            [In, Out] IntPtr[] readfds,
            [In, Out] IntPtr[] writefds,
            [In, Out] IntPtr[] exceptfds,
#endif
            [In] ref TimeValue timeout);
    }

    internal static
#if LITENETLIB_UNSAFE
        unsafe
#endif
        class UnixSock
    {
        private const string LibName = "libc";
        
        [DllImport(LibName)]
        public static extern int recvfrom(
            IntPtr socketHandle,
#if LITENETLIB_UNSAFE
            byte* pinnedBuffer,
#else
            [In, Out] byte[] pinnedBuffer,
#endif
            [In] int len,
            [In] SocketFlags socketFlags,
#if LITENETLIB_UNSAFE
            byte* socketAddress,
#else
            [Out] byte[] socketAddress,
#endif
            [In, Out] ref int socketAddressSize);
        
        [DllImport(LibName)]
        internal static extern int sendto(
            IntPtr socketHandle,
#if LITENETLIB_UNSAFE
            byte* pinnedBuffer,
#else
            [In] byte[] pinnedBuffer,
#endif
            [In] int len,
            [In] SocketFlags socketFlags,
#if LITENETLIB_UNSAFE
            byte* socketAddress,
#else
            [In] byte[] socketAddress,
#endif
            [In] int socketAddressSize);

        [DllImport(LibName, SetLastError = true)]
        internal static extern int select(
            [In] int ignoredParameter,
#if LITENETLIB_UNSAFE
            IntPtr* readfds,
            IntPtr* writefds,
            IntPtr* exceptfds,
#else
            [In, Out] IntPtr[] readfds,
            [In, Out] IntPtr[] writefds,
            [In, Out] IntPtr[] exceptfds,
#endif
            [In] ref TimeValue timeout);
    }

    internal static class NativeSocket
    {
        public static readonly bool IsSupported;
        private static readonly bool UnixMode;

#if !LITENETLIB_UNSAFE
        [ThreadStatic] private static byte[] SendToBuffer;
        [ThreadStatic] private static IntPtr[] PollHandle;
#endif

        [ThreadStatic] private static byte[] EndPointBuffer;
        [ThreadStatic] private static byte[] AddrBuffer;

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
            { UnixSocketError.ENXIO, SocketError.HostNotFound },
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

        static
#if LITENETLIB_UNSAFE
            unsafe
#endif
            NativeSocket()
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

        public static IPAddress GetIPAddress(byte[] saddr)
        {
            short family = BitConverter.ToInt16(saddr, 0);
            if ((UnixMode && family == AF_INET6) || (!UnixMode && (AddressFamily)family == AddressFamily.InterNetworkV6))
            {
                if(AddrBuffer == null)
                    AddrBuffer = new byte[16];

                Buffer.BlockCopy(saddr, 8, AddrBuffer, 0, 16);
                uint scope = unchecked((uint)(
                    (saddr[27] << 24) +
                    (saddr[26] << 16) +
                    (saddr[25] << 8) +
                    (saddr[24])));
                return new IPAddress(AddrBuffer, scope);
            }
            long ipv4Addr = unchecked((uint)((saddr[4] & 0x000000FF) |
                                             (saddr[5] << 8 & 0x0000FF00) |
                                             (saddr[6] << 16 & 0x00FF0000) |
                                             (saddr[7] << 24)));
            return new IPAddress(ipv4Addr & 0x0FFFFFFFF);
        }

        public static byte[] GetNativeEndPoint(IPEndPoint endPoint)
        {
            if (EndPointBuffer == null)
                EndPointBuffer = new byte[MaxAddrSize];
            bool ipv4 = endPoint.AddressFamily == AddressFamily.InterNetwork;
            short addressFamily = UnixMode ? (short)(ipv4 ? AF_INET : AF_INET6) : (short)endPoint.AddressFamily;
            EndPointBuffer[0] = (byte)(addressFamily);
            EndPointBuffer[1] = (byte)(addressFamily >> 8);
            EndPointBuffer[2] = (byte)(endPoint.Port >> 8);
            EndPointBuffer[3] = (byte)(endPoint.Port);

            if (ipv4)
            {
#pragma warning disable 618
                long addr = endPoint.Address.Address;
#pragma warning restore 618
                EndPointBuffer[4] = (byte)(addr);
                EndPointBuffer[5] = (byte)(addr >> 8);
                EndPointBuffer[6] = (byte)(addr >> 16);
                EndPointBuffer[7] = (byte)(addr >> 24);
            }
            else
            {
                byte[] addrBytes = endPoint.Address.GetAddressBytes();
                Buffer.BlockCopy(addrBytes, 0, EndPointBuffer, 8, 16);
            }

            return EndPointBuffer;
        }

        private static SocketError GetSocketError()
        {
            int error = Marshal.GetLastWin32Error();
            if (UnixMode)
            {
                SocketError err;
                return NativeErrorToSocketError.TryGetValue((UnixSocketError)error, out err) ? err : SocketError.SocketError;
            }
            return (SocketError)error;
        }

        public static
#if LITENETLIB_UNSAFE
            unsafe
#endif
            bool Poll(IntPtr socketHandle, int microSeconds)
        {
            TimeValue timeValue = new TimeValue
            {
                Seconds = (int) (microSeconds / 1000000L), 
                Microseconds = (int) (microSeconds % 1000000L)
            };
#if LITENETLIB_UNSAFE
#if !NET35
            IntPtr* pollHandle = stackalloc IntPtr[2] {(IntPtr)1, socketHandle};
#else
            IntPtr* pollHandle = stackalloc IntPtr[2];
            pollHandle[0] = (IntPtr)1;
            pollHandle[1] = socketHandle;
#endif
            int num = UnixMode
                ? UnixSock.select(0, pollHandle, null, null, ref timeValue)
                : WinSock.select(0, pollHandle, null, null, ref timeValue);
            if (num == -1)
                throw new SocketException((int)GetSocketError());
            return (int)pollHandle[0] != 0 && pollHandle[1] == socketHandle;
#else
            if (PollHandle == null)
                PollHandle = new IntPtr[2];
            PollHandle[0] = (IntPtr)1;
            PollHandle[1] = socketHandle;
            int num = UnixMode 
                ? UnixSock.select(0, PollHandle, null, null, ref timeValue)
                : WinSock.select(0, PollHandle, null, null, ref timeValue);
            if (num == -1)
                throw new SocketException((int)GetSocketError());
            return (int)PollHandle[0] != 0 && PollHandle[1] == socketHandle;
#endif
        }

        public static
#if LITENETLIB_UNSAFE
            unsafe
#endif
            int ReceiveFrom(IntPtr socketHandle, byte[] buffer, int size, byte[] socketAddress)
        {
            int bytesReceived;
            int addressLength = socketAddress.Length;
#if LITENETLIB_UNSAFE
            fixed (byte* data = buffer)
            {
                fixed (byte* addr = socketAddress)
                {
                    bytesReceived = UnixMode
                        ? UnixSock.recvfrom(socketHandle, data, size, 0, addr, ref addressLength)
                        : WinSock.recvfrom(socketHandle, data, size, 0, addr, ref addressLength);
                }
            }
#else
            bytesReceived = UnixMode
                ? UnixSock.recvfrom(socketHandle, buffer, size, 0, socketAddress, ref addressLength)
                : WinSock.recvfrom(socketHandle, buffer, size, 0, socketAddress, ref addressLength);
#endif
            if (bytesReceived == -1)
                throw new SocketException((int)GetSocketError());
            return bytesReceived;
        }

        public static
#if LITENETLIB_UNSAFE
            unsafe
#endif
            int SendTo(Socket s, byte[] buffer, int start, int size, byte[] socketAddress)
        {
            int bytesSent;
#if LITENETLIB_UNSAFE
            fixed (byte* data = &buffer[start])
            {
                fixed (byte* addr = socketAddress)
                {
                    bytesSent = UnixMode
                        ? UnixSock.sendto(s.Handle, data, size, 0, addr, socketAddress.Length)
                        : WinSock.sendto(s.Handle, data, size, 0, addr, socketAddress.Length);
                }
            }
#else
            if (start > 0)
            {
                if (SendToBuffer == null)
                    SendToBuffer = new byte[NetConstants.MaxPacketSize];
                Buffer.BlockCopy(buffer, start, SendToBuffer, 0, size);
                buffer = SendToBuffer;
            }
            bytesSent = UnixMode
                ? UnixSock.sendto(s.Handle, buffer, size, 0, socketAddress, socketAddress.Length)
                : WinSock.sendto(s.Handle, buffer, size, 0, socketAddress, socketAddress.Length);
#endif
            if (bytesSent == -1)
                throw new SocketException((int)GetSocketError());
            return bytesSent;
        }
    }
}
