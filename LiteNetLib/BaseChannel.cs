using System.Collections.Concurrent;
using System.Threading;

namespace LiteNetLib
{
    internal abstract class BaseChannel
    {
        protected readonly NetPeer Peer;
        protected readonly ConcurrentQueue<NetPacket> OutgoingQueue;
        private int _isAddedToPeerChannelSendQueue;

        public int PacketsInQueue => OutgoingQueue.Count;

        protected BaseChannel(NetPeer peer)
        {
            Peer = peer;
            OutgoingQueue = new ConcurrentQueue<NetPacket>();
        }

        public void AddToQueue(NetPacket packet)
        {
            OutgoingQueue.Enqueue(packet);
            AddToPeerChannelSendQueue();
        }

        protected void AddToPeerChannelSendQueue()
        {
            if (Interlocked.CompareExchange(ref _isAddedToPeerChannelSendQueue, 1, 0) == 0)
            {
                Peer.AddToReliableChannelSendQueue(this);
            }
        }

        public bool SendAndCheckQueue()
        {
            bool hasPacketsToSend = SendNextPackets();
            if (!hasPacketsToSend)
                Interlocked.Exchange(ref _isAddedToPeerChannelSendQueue, 0);

            return hasPacketsToSend;
        }

        protected abstract bool SendNextPackets();
        public abstract bool ProcessPacket(NetPacket packet);
    }
}
