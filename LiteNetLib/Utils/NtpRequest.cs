using System.Net;
using System.Net.Sockets;

namespace LiteNetLib.Utils
{
    /// <summary>
    /// Represents an active NTP (Network Time Protocol) query to a remote time server.
    /// Handles retransmission and lifetime management of the request.
    /// </summary>
    internal sealed class NtpRequest
    {
        private const int ResendTimer = 1000;
        private const int KillTimer = 10000;
        /// <summary>
        /// Standard UDP port used by NTP servers.
        /// </summary>
        public const int DefaultPort = 123;
        private readonly IPEndPoint _ntpEndPoint;
        private float _resendTime = ResendTimer;
        private float _killTime = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="NtpRequest"/> class for a specific server.
        /// </summary>
        /// <param name="endPoint">The IP endpoint of the NTP server.</param>
        public NtpRequest(IPEndPoint endPoint)
        {
            _ntpEndPoint = endPoint;
        }

        /// <summary>
        /// Gets a value indicating whether the request has exceeded its maximum lifetime and should be removed.
        /// </summary>
        public bool NeedToKill => _killTime >= KillTimer;

        /// <summary>
        /// Attempts to send an NTP query packet to the remote endpoint.
        /// </summary>
        /// <remarks>
        /// The packet is only sent if the internal retransmission timer has elapsed. 
        /// Updates internal timers for both retransmission and total lifetime.
        /// </remarks>
        /// <param name="socket">The underlying socket used to send the datagram.</param>
        /// <param name="time">The amount of time elapsed since the last update/call, in seconds.</param>
        /// <returns><see langword="true"/> if the packet was successfully transmitted; otherwise, <see langword="false"/>.</returns>
        public bool Send(Socket socket, float time)
        {
            _resendTime += time;
            _killTime += time;
            if (_resendTime < ResendTimer)
            {
                return false;
            }
            var packet = new NtpPacket();
            try
            {
                int sendCount = socket.SendTo(packet.Bytes, 0, packet.Bytes.Length, SocketFlags.None, _ntpEndPoint);
                return sendCount == packet.Bytes.Length;
            }
            catch
            {
                return false;
            }
        }
    }
}
