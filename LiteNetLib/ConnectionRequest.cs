using System.Net;
using System.Threading;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    internal enum ConnectionRequestResult
    {
        None,
        Accept,
        Reject,
        RejectForce
    }

    public class LiteConnectionRequest
    {
        private readonly LiteNetManager _listener;
        private int _used;

        public NetDataReader Data => InternalPacket.Data;

        internal ConnectionRequestResult Result { get; private set; }
        internal NetConnectRequestPacket InternalPacket;

        public readonly IPEndPoint RemoteEndPoint;

        internal void UpdateRequest(NetConnectRequestPacket connectRequest)
        {
            //old request
            if (connectRequest.ConnectionTime < InternalPacket.ConnectionTime)
                return;

            if (connectRequest.ConnectionTime == InternalPacket.ConnectionTime &&
                connectRequest.ConnectionNumber == InternalPacket.ConnectionNumber)
                return;

            InternalPacket = connectRequest;
        }

        private bool TryActivate() =>
            Interlocked.CompareExchange(ref _used, 1, 0) == 0;

        internal LiteConnectionRequest(IPEndPoint remoteEndPoint, NetConnectRequestPacket requestPacket, LiteNetManager listener)
        {
            InternalPacket = requestPacket;
            RemoteEndPoint = remoteEndPoint;
            _listener = listener;
        }

        public LiteNetPeer AcceptIfKey(string key)
        {
            if (!TryActivate())
                return null;
            try
            {
                if (Data.GetString() == key)
                    Result = ConnectionRequestResult.Accept;
            }
            catch
            {
                NetDebug.WriteError("[AC] Invalid incoming data");
            }
            if (Result == ConnectionRequestResult.Accept)
                return _listener.OnConnectionSolved(this, null, 0, 0);

            Result = ConnectionRequestResult.Reject;
            _listener.OnConnectionSolved(this, null, 0, 0);
            return null;
        }

        /// <summary>
        /// Accept connection and get new NetPeer as result
        /// </summary>
        /// <returns>Connected NetPeer</returns>
        public LiteNetPeer Accept()
        {
            if (!TryActivate())
                return null;
            Result = ConnectionRequestResult.Accept;
            return _listener.OnConnectionSolved(this, null, 0, 0);
        }

        /// <summary>
        /// Rejects the connection request.
        /// </summary>
        /// <param name="rejectData">Optional user data to send along with the rejection packet.</param>
        /// <param name="start">Offset in the <paramref name="rejectData"/> array.</param>
        /// <param name="length">Length of the data to be sent from the <paramref name="rejectData"/> array.</param>
        /// <param name="force">
        /// If <see langword="true"/>, performs a "fire-and-forget" rejection without creating an internal peer.
        /// If <see langword="false"/>, creates a temporary peer to ensure the rejection is delivered reliably.
        /// </param>
        public void Reject(byte[] rejectData, int start, int length, bool force)
        {
            if (!TryActivate())
                return;
            Result = force ? ConnectionRequestResult.RejectForce : ConnectionRequestResult.Reject;
            _listener.OnConnectionSolved(this, rejectData, start, length);
        }

        /// <summary>
        /// Rejects the connection reliably. Creates a temporary peer to handle packet loss.
        /// </summary>
        /// <param name="rejectData">Data to send with the rejection.</param>
        /// <param name="start">Offset in the <paramref name="rejectData"/> array.</param>
        /// <param name="length">Length of the data to be sent.</param>
        public void Reject(byte[] rejectData, int start, int length) =>
            Reject(rejectData, start, length, false);

        /// <summary>
        /// Rejects the connection immediately without reliability. 
        /// Minimizes resource usage by not creating an internal peer.
        /// </summary>
        /// <param name="rejectData">Data to send with the rejection.</param>
        /// <param name="start">Offset in the <paramref name="rejectData"/> array.</param>
        /// <param name="length">Length of the data to be sent.</param>
        public void RejectForce(byte[] rejectData, int start, int length) =>
            Reject(rejectData, start, length, true);

        /// <summary>
        /// Rejects the connection immediately without reliability and without additional data.
        /// </summary>
        public void RejectForce() =>
            Reject(null, 0, 0, true);

        /// <summary>
        /// Rejects the connection immediately without reliability.
        /// </summary>
        /// <param name="rejectData">Data to send with the rejection.</param>
        public void RejectForce(byte[] rejectData) =>
            Reject(rejectData, 0, rejectData.Length, true);

        /// <summary>
        /// Rejects the connection immediately without reliability using data from a <see cref="NetDataWriter"/>.
        /// </summary>
        /// <param name="rejectData">Writer containing the data to send.</param>
        public void RejectForce(NetDataWriter rejectData) =>
            Reject(rejectData.Data, 0, rejectData.Length, true);

        /// <summary>
        /// Rejects the connection reliably without additional data.
        /// </summary>
        public void Reject() =>
            Reject(null, 0, 0, false);

        /// <summary>
        /// Rejects the connection reliably.
        /// </summary>
        /// <param name="rejectData">Data to send with the rejection.</param>
        public void Reject(byte[] rejectData) =>
            Reject(rejectData, 0, rejectData.Length, false);

        /// <summary>
        /// Rejects the connection reliably using data from a <see cref="NetDataWriter"/>.
        /// </summary>
        /// <param name="rejectData">Writer containing the data to send.</param>
        public void Reject(NetDataWriter rejectData) =>
            Reject(rejectData.Data, 0, rejectData.Length, false);
    }

    public class ConnectionRequest : LiteConnectionRequest
    {
        internal ConnectionRequest(IPEndPoint remoteEndPoint, NetConnectRequestPacket requestPacket, LiteNetManager listener) : base(remoteEndPoint, requestPacket, listener)
        {
        }

        public new NetPeer AcceptIfKey(string key) => (NetPeer)base.AcceptIfKey(key);

        public new NetPeer Accept() => (NetPeer)base.Accept();
    }
}
