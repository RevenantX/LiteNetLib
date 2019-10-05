using System;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLib.Utils
{
    /// <summary>
    /// Make NTP request.
    /// <para>
    /// 1. Create the object by <see cref="Create(IPEndPoint,Action&lt;NtpPacket&gt;)"/> method. 
    /// </para>
    /// <para>
    /// 2. Use <see cref="Send"/> method to send requests. 3. Call <see cref="Close"/> to release the socket
    /// AFTER you have received the response or some timeout. If you close the socket too early, you may miss the response.
    /// </para>
    /// <para>
    /// 3. Call <see cref="Close"/> to release the socket AFTER you have received the response or some timeout.
    /// If you close the socket too early, you may miss the response.
    /// </para>
    /// </summary>
    public sealed class NtpRequest : INetSocketListener
    {
        public const int DefaultPort = 123;

        private readonly NetSocket _socket;
        private readonly Action<NtpPacket> _onRequestComplete;
        private readonly IPEndPoint _ntpEndPoint;

        /// <summary>
        /// Initialize object, open socket.
        /// </summary>
        /// <param name="endPoint">NTP Server endpoint</param>
        /// <param name="onRequestComplete">callback (called from other thread!)</param>
        private NtpRequest(IPEndPoint endPoint, Action<NtpPacket> onRequestComplete)
        {
            _ntpEndPoint = endPoint;
            _onRequestComplete = onRequestComplete;

            // Create and start socket
            _socket = new NetSocket(this);
            _socket.Bind(IPAddress.Any, IPAddress.IPv6Any, 0, false, endPoint.AddressFamily == AddressFamily.InterNetworkV6);
        }

        /// <summary>
        /// Create the requests for NTP server, open socket.
        /// </summary>
        /// <param name="endPoint">NTP Server address.</param>
        /// <param name="onRequestComplete">callback (called from other thread!)</param>
        public static NtpRequest Create(IPEndPoint endPoint, Action<NtpPacket> onRequestComplete)
        {
            return new NtpRequest(endPoint, onRequestComplete);
        }

        /// <summary>
        /// Create the requests for NTP server (default port), open socket.
        /// </summary>
        /// <param name="ipAddress">NTP Server address.</param>
        /// <param name="onRequestComplete">callback (called from other thread!)</param>
        public static NtpRequest Create(IPAddress ipAddress, Action<NtpPacket> onRequestComplete)
        {
            IPEndPoint endPoint = new IPEndPoint(ipAddress, DefaultPort);
            return Create(endPoint, onRequestComplete);
        }

        /// <summary>
        /// Create the requests for NTP server, open socket.
        /// </summary>
        /// <param name="ntpServerAddress">NTP Server address.</param>
        /// <param name="port">port</param>
        /// <param name="onRequestComplete">callback (called from other thread!)</param>
        public static NtpRequest Create(string ntpServerAddress, int port, Action<NtpPacket> onRequestComplete)
        {
            IPEndPoint endPoint = NetUtils.MakeEndPoint(ntpServerAddress, port);
            return Create(endPoint, onRequestComplete);
        }

        /// <summary>
        /// Create the requests for NTP server (default port), open socket.
        /// </summary>
        /// <param name="ntpServerAddress">NTP Server address.</param>
        /// <param name="onRequestComplete">callback (called from other thread!)</param>
        public static NtpRequest Create(string ntpServerAddress, Action<NtpPacket> onRequestComplete)
        {
            IPEndPoint endPoint = NetUtils.MakeEndPoint(ntpServerAddress, DefaultPort);
            return Create(endPoint, onRequestComplete);
        }

        /// <summary>
        /// Send request to the NTP server calls callback (if success).
        /// In case of error the callbacke is called with null param.
        /// </summary>
        public void Send()
        {
            SocketError errorCode = 0;
            var packet = new NtpPacket();
            packet.ValidateRequest(); // not necessary
            byte[] sendData = packet.Bytes;
            var sendCount = _socket.SendTo(sendData, 0, sendData.Length, _ntpEndPoint, ref errorCode);
            if (errorCode != 0 || sendCount != sendData.Length)
            {
                _onRequestComplete(null);
            }
        }

        /// <summary>
        /// Close socket.
        /// </summary>
        public void Close()
        {
            _socket.Close();
        }

        /// <summary>
        /// Handle received data: transform bytes to NtpPacket, close socket and call the callback.
        /// </summary>
        void INetSocketListener.OnMessageReceived(byte[] data, int length, SocketError errorCode, IPEndPoint remoteEndPoint)
        {
            DateTime destinationTimestamp = DateTime.UtcNow;
            if (!remoteEndPoint.Equals(_ntpEndPoint))
                return;

            if (length < 48)
            {
                NetDebug.Write(NetLogLevel.Trace, "NTP response too short: {}", length);
                _onRequestComplete(null);
                return;
            }

            NtpPacket packet = NtpPacket.FromServerResponse(data, destinationTimestamp);
            try
            {
                packet.ValidateReply();
            }
            catch (InvalidOperationException ex)
            {
                NetDebug.Write(NetLogLevel.Trace, "NTP response error: {}", ex.Message);
                packet = null;
            }
            _onRequestComplete(packet);
        }
    }
}