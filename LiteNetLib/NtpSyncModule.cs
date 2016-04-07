using System;

namespace LiteNetLib
{
    public class NtpSyncModule
    {
        public DateTime? SyncedTime { get; private set; }
        private readonly NetSocket _socket;
        private readonly NetEndPoint _ntpEndPoint;

        public NtpSyncModule(string ntpServer)
        {
            _socket = new NetSocket(ConnectionAddressType.IPv4);
            NetEndPoint ourEndPoint = new NetEndPoint(ConnectionAddressType.IPv4, 0);
            _socket.Bind(ref ourEndPoint);
            _socket.ReceiveTimeout = 3000;
            _ntpEndPoint = new NetEndPoint(ntpServer, 123);
            SyncedTime = null;
        }

        public void GetNetworkTime()
        {
            if (SyncedTime != null)
                return;

            var ntpData = new byte[48];
            //LeapIndicator = 0 (no warning)
            //VersionNum = 3
            //Mode = 3 (Client Mode)
            ntpData[0] = 0x1B;

            //send
            _socket.SendTo(ntpData, _ntpEndPoint);

            //receive
            NetEndPoint endPoint = new NetEndPoint(ConnectionAddressType.IPv4, 0);
            int errorCode = 0;
            if (_socket.ReceiveFrom(ref ntpData, ref endPoint, ref errorCode) > 0 && endPoint.Equals(_ntpEndPoint))
            {
                ulong intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | (ulong)ntpData[43];
                ulong fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | (ulong)ntpData[47];

                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                SyncedTime = (new DateTime(1900, 1, 1)).AddMilliseconds((long)milliseconds);
            }
        }
    }
}
