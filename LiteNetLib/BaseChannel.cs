using System.Collections.Generic;

namespace LiteNetLib
{
    using System.Runtime.CompilerServices;

    internal abstract class BaseChannel
    {
        protected readonly NetPeer Peer;
        protected readonly Queue<NetPacket> OutgoingQueue;

        protected BaseChannel(NetPeer peer)
        {
            Peer = peer;
            OutgoingQueue = new Queue<NetPacket>(64);
        }

        public int PacketsInQueue
        {
            get { return OutgoingQueue.Count; }
        }

        public abstract bool HasPacketsToSend { get; }

        public object OutgoingQueueSyncRoot => OutgoingQueue;

        public bool IsAddedToPeerChannelSendQueue;

        public void AddToQueue(NetPacket packet)
        {
            lock (OutgoingQueue)
            {
                OutgoingQueue.Enqueue(packet);
                AddToPeerChannelSendQueueNoLock();
            }
        }

        protected void AddToPeerChannelSendQueue()
        {
            lock (OutgoingQueue)
            {
                AddToPeerChannelSendQueueNoLock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddToPeerChannelSendQueueNoLock()
        {
            if (IsAddedToPeerChannelSendQueue)
            {
                return;
            }

            Peer.AddToReliableChannelSendQueue(this);
            IsAddedToPeerChannelSendQueue = true;
        }

        public abstract void SendNextPackets();
        public abstract bool ProcessPacket(NetPacket packet);
    }
}
