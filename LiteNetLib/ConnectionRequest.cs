using System.Net;
using System.Threading;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    public enum ConnectionRequestType
    {
        Incoming,
        PeerToPeer
    }

    internal enum ConnectionRequestResult
    {
        None,
        Accept,
        Reject,
        RejectForce
    }

    public class ConnectionRequest
    {
        private readonly NetManager _listener;
        private int _used;

        public IPEndPoint RemoteEndPoint { get { return Peer.EndPoint; } }
        public readonly NetDataReader Data;
        public ConnectionRequestType Type { get; private set; }

        internal ConnectionRequestResult Result { get; private set; }
        internal readonly long ConnectionId;
        internal readonly byte ConnectionNumber;
        internal readonly NetPeer Peer;

        private bool TryActivate()
        {
            return Interlocked.CompareExchange(ref _used, 1, 0) == 0;
        }

        internal ConnectionRequest(
            long connectionId,
            byte connectionNumber,
            ConnectionRequestType type,
            NetDataReader netDataReader,
            NetPeer peer,
            NetManager listener)
        {
            ConnectionId = connectionId;
            ConnectionNumber = connectionNumber;
            Type = type;
            Peer = peer;
            Data = netDataReader;
            _listener = listener;
        }

        public NetPeer AcceptIfKey(string key)
        {
            if (!TryActivate())
                return null;
            try
            {
                if (Data.GetString() == key)
                {
                    Result = ConnectionRequestResult.Accept;
                    _listener.OnConnectionSolved(this, null, 0, 0);
                    return Peer;
                }
            }
            catch
            {
                NetDebug.WriteError("[AC] Invalid incoming data");
            }
            Result = ConnectionRequestResult.Reject;
            _listener.OnConnectionSolved(this, null, 0, 0);
            return null;
        }

        /// <summary>
        /// Accept connection and get new NetPeer as result
        /// </summary>
        /// <returns>Connected NetPeer</returns>
        public NetPeer Accept()
        {
            if (!TryActivate())
                return null;
            Result = ConnectionRequestResult.Accept;
            _listener.OnConnectionSolved(this, null, 0, 0);
            return Peer;
        }
        
        public void Reject(byte[] rejectData, int start, int length, bool force)
        {
            if (!TryActivate())
                return;
            Result = force ? ConnectionRequestResult.RejectForce : ConnectionRequestResult.Reject;
            _listener.OnConnectionSolved(this, rejectData, start, length);
        }

        public void Reject(byte[] rejectData, int start, int length)
        {
            Reject(rejectData, start, length, false);
        }


        public void RejectForce(byte[] rejectData, int start, int length)
        {
            Reject(rejectData, start, length, true);
        }

        public void RejectForce()
        {
            Reject(null, 0, 0, true);
        }

        public void RejectForce(byte[] rejectData)
        {
            Reject(rejectData, 0, rejectData.Length, true);
        }

        public void RejectForce(NetDataWriter rejectData)
        {
            Reject(rejectData.Data, 0, rejectData.Length, true);
        }

        public void Reject()
        {
            Reject(null, 0, 0, false);
        }

        public void Reject(byte[] rejectData)
        {
            Reject(rejectData, 0, rejectData.Length, false);
        }

        public void Reject(NetDataWriter rejectData)
        {
            Reject(rejectData.Data, 0, rejectData.Length, false);
        }
    }
}
