using System;
using System.Collections.Generic;

namespace LiteNetLib
{
    internal sealed class NetPeerCollection
    {
        private class PeerComparer : IEqualityComparer<NetEndPoint>
        {
            public bool Equals(NetEndPoint x, NetEndPoint y)
            {
                return x.EndPoint.Equals(y.EndPoint);
            }

            public int GetHashCode(NetEndPoint obj)
            {
                return obj.GetHashCode();
            }
        }

        private readonly Dictionary<NetEndPoint, NetPeer> _peersDict;
        private readonly NetPeer[] _peersArray;
        public int Count;

        public NetPeer this[int index]
        {
            get { return _peersArray[index]; }
        }

        public NetPeerCollection(int maxPeers)
        {
            _peersArray = new NetPeer[maxPeers];
            _peersDict = new Dictionary<NetEndPoint, NetPeer>(new PeerComparer());
        }

        public bool TryGetValue(NetEndPoint endPoint, out NetPeer peer)
        {
            return _peersDict.TryGetValue(endPoint, out peer);
        }

        public void Clear()
        {
            Array.Clear(_peersArray, 0, Count);
            _peersDict.Clear();
            Count = 0;
        }

        public void Add(NetEndPoint endPoint, NetPeer peer)
        {
            _peersArray[Count] = peer;
            _peersDict.Add(endPoint, peer);
            Count++;
        }

        public bool ContainsAddress(NetEndPoint endPoint)
        {
            return _peersDict.ContainsKey(endPoint);
        }

        public NetPeer[] ToArray()
        {
            NetPeer[] result = new NetPeer[Count];
            Array.Copy(_peersArray, 0, result, 0, Count);
            return result;
        }

        public void RemoveAt(int idx)
        {
            _peersDict.Remove(_peersArray[idx].EndPoint);
            _peersArray[idx] = _peersArray[Count - 1];
            _peersArray[Count - 1] = null;
            Count--;
        }
    }
}
