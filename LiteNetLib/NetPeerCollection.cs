using System;
using System.Collections.Generic;
using System.Threading;

namespace LiteNetLib
{
    internal sealed class NetPeerCollection
    {
        private readonly Dictionary<NetEndPoint, NetPeer> _peersDict;
        private NetPeer[] _peersArray;
        private readonly ReaderWriterLockSlim _lock;
        public int Count;

        public NetPeer this[int index]
        {
            get { return _peersArray[index]; }
        }

        public void EnterWriteLock()
        {
            _lock.EnterWriteLock();
        }

        public void ExitWriteLock()
        {
            _lock.ExitWriteLock();
        }

        public void EnterReadLock()
        {
            _lock.EnterReadLock();
        }

        public void ExitReadLock()
        {
            _lock.ExitReadLock();
        }

        public NetPeerCollection(int maxPeers)
        {
            _peersArray = new NetPeer[maxPeers];
            _peersDict = new Dictionary<NetEndPoint, NetPeer>(new NetEndPointComparer());
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }

        public bool TryGetValue(NetEndPoint endPoint, out NetPeer peer)
        {
            _lock.EnterReadLock();
            bool result = _peersDict.TryGetValue(endPoint, out peer);
            _lock.ExitReadLock();
            return result;
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            Array.Clear(_peersArray, 0, Count);
            _peersDict.Clear();
            Count = 0;
            _lock.ExitWriteLock();
        }

        public void Add(NetEndPoint endPoint, NetPeer peer)
        {
            if (Count == _peersArray.Length)
            {
                Array.Resize(ref _peersArray, _peersArray.Length*2);
            }
            _peersArray[Count] = peer;
            _peersDict.Add(endPoint, peer);
            Count++;
        }

        public bool ContainsAddress(NetEndPoint endPoint)
        {
            _lock.EnterReadLock();
            bool result = _peersDict.ContainsKey(endPoint);
            _lock.ExitReadLock();
            return result;
        }

        public NetPeer[] ToArray()
        {
            _lock.EnterReadLock();
            NetPeer[] result = new NetPeer[Count];
            Array.Copy(_peersArray, 0, result, 0, Count);
            _lock.ExitReadLock();
            return result;
        }

        public void RemovePeers(List<int> idxList)
        {
            if (idxList.Count == 0)
                return;
            _lock.EnterWriteLock();
            for (int i = idxList.Count - 1; i >= 0; i--)
            {
                _peersDict.Remove(_peersArray[i].EndPoint);
                if (i == Count - 1)
                    _peersArray[i] = null;
                else
                {
                    _peersArray[i] = _peersArray[Count - 1];
                    _peersArray[Count - 1] = null;
                }
                Count--;
            }
            _lock.ExitWriteLock();
        }
    }
}
