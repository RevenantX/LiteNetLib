using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace LiteNetLib
{
    internal sealed class IPEndPointComparer : IEqualityComparer<IPEndPoint>
    {
        public bool Equals(IPEndPoint x, IPEndPoint y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(IPEndPoint obj)
        {
            return obj.GetHashCode();
        }
    }

    internal sealed class NetPeerCollection
    {
        private readonly Dictionary<IPEndPoint, NetPeer> _peersDict;
        private readonly ReaderWriterLockSlim _lock;
        public int Count;
        public volatile NetPeer HeadPeer;

        public NetPeerCollection()
        {
            _peersDict = new Dictionary<IPEndPoint, NetPeer>(new IPEndPointComparer());
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        public bool TryGetValue(IPEndPoint endPoint, out NetPeer peer)
        {
            _lock.EnterReadLock();
            bool result = _peersDict.TryGetValue(endPoint, out peer);
            _lock.ExitReadLock();
            return result;
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            HeadPeer = null;
            _peersDict.Clear();
            Count = 0;
            _lock.ExitWriteLock();
        }

        public NetPeer TryAdd(NetPeer peer)
        {
            _lock.EnterUpgradeableReadLock();
            NetPeer existingPeer;
            if (_peersDict.TryGetValue(peer.EndPoint, out existingPeer))
            {
                _lock.ExitUpgradeableReadLock();
                return existingPeer;
            }
            _lock.EnterWriteLock();
            if (HeadPeer != null)
            {
                peer.NextPeer = HeadPeer;
                HeadPeer.PrevPeer = peer;
            }
            HeadPeer = peer;
            _peersDict.Add(peer.EndPoint, peer);
            Count++;
            _lock.ExitWriteLock();
            _lock.ExitUpgradeableReadLock();
            return peer;
        }

        public void RemovePeers(List<NetPeer> peersList)
        {
            _lock.EnterWriteLock();
            for (int i = 0; i < peersList.Count; i++)
                RemovePeerInternal(peersList[i]);
            _lock.ExitWriteLock();
        }

        public void RemovePeer(NetPeer peer)
        {
            _lock.EnterWriteLock();
            RemovePeerInternal(peer);
            _lock.ExitWriteLock();
        }

        private void RemovePeerInternal(NetPeer peer)
        {
            if (!_peersDict.Remove(peer.EndPoint))
                return;
            if (peer == HeadPeer)
                HeadPeer = peer.NextPeer;
            if (peer.PrevPeer != null)
                peer.PrevPeer.NextPeer = peer.NextPeer;
            if (peer.NextPeer != null)
                peer.NextPeer.PrevPeer = peer.PrevPeer;
            peer.PrevPeer = null;
            peer.NextPeer = null;
            Count--;
        }
    }
}
