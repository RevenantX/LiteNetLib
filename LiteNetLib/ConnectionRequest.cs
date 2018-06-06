using System;
using System.Net;
using System.Threading;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    public enum ConnectionRequestResult
    {
        None,
        Accept,
        Reject
    }

    public enum ConnectionRequestType
    {
        Incoming,
        PeerToPeer
    }

    public class ConnectionRequest
    {
        private readonly Action<ConnectionRequest> _onUserAction;
        private int _used;

        public IPEndPoint RemoteEndPoint { get { return Peer.EndPoint; } }
        public readonly NetDataReader Data;
        public ConnectionRequestResult Result { get; private set; }
        public ConnectionRequestType Type { get; private set; }

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
            Action<ConnectionRequest> onUserAction)
        {
            ConnectionId = connectionId;
            ConnectionNumber = connectionNumber;
            Type = type;
            Peer = peer;
            Data = netDataReader;
            _onUserAction = onUserAction;
        }

        public NetPeer AcceptIfKey(string key)
        {
            if (!TryActivate())
                return null;
            try
            {
                string dataKey = Data.GetString(key.Length);
                if (dataKey == key)
                {
                    Result = ConnectionRequestResult.Accept;
                    _onUserAction(this);
                    return Peer;
                }
            }
            catch
            {
                NetUtils.DebugWriteError("[AC] Invalid incoming data");
            }
            Result = ConnectionRequestResult.Reject;
            _onUserAction(this);
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
            _onUserAction(this);
            return Peer;
        }

        public void Reject()
        {
            if (!TryActivate())
                return;
            Result = ConnectionRequestResult.Reject;
            _onUserAction(this);
        }
    }
}
