using System.Collections.Generic;

namespace LiteNetLib
{
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

        /// <summary>
        /// The lock object is required when iterating over the channels in the send queue
        /// and when marking the channel as added to the send queue.
        /// </summary>
        public object OutgoingQueueSyncRoot
        {
            get { return this.OutgoingQueue; }
        }

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
