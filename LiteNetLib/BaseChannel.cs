using System.Collections.Generic;
using System.Threading;

namespace LiteNetLib
{
    /// <summary>
    /// Base class for all communication channels (Reliable, Unreliable, Sequenced). <br/>
    /// Handles the queuing and scheduling of outgoing packets.
    /// </summary>
    internal abstract class BaseChannel
    {
        /// <summary>
        /// The peer associated with this channel.
        /// </summary>
        protected readonly LiteNetPeer Peer;
        /// <summary>
        /// Queue containing packets waiting to be sent over the network.
        /// </summary>
        protected readonly Queue<NetPacket> OutgoingQueue = new Queue<NetPacket>(NetConstants.DefaultWindowSize);
        private int _isAddedToPeerChannelSendQueue;

        /// <summary>
        /// Gets the number of packets currently residing in the outgoing queue.
        /// </summary>
        public int PacketsInQueue => OutgoingQueue.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseChannel"/> class.
        /// </summary>
        /// <param name="peer">The peer that owns this channel.</param>
        protected BaseChannel(LiteNetPeer peer) =>
            Peer = peer;

        /// <summary>
        /// Adds a packet to the outgoing queue and notifies the peer to schedule a send update.
        /// </summary>
        /// <param name="packet">The packet to be enqueued.</param>
        public void AddToQueue(NetPacket packet)
        {
            lock (OutgoingQueue)
            {
                OutgoingQueue.Enqueue(packet);
            }
            AddToPeerChannelSendQueue();
        }

        /// <summary>
        /// Thread-safely marks this channel as having pending data and adds it to the peer's update list.
        /// </summary>
        protected void AddToPeerChannelSendQueue()
        {
            if (Interlocked.CompareExchange(ref _isAddedToPeerChannelSendQueue, 1, 0) == 0)
                Peer.AddToReliableChannelSendQueue(this);
        }

        /// <summary>
        /// Attempts to send packets from the queue. If the queue becomes empty or throttled, 
        /// it resets the update flag.
        /// </summary>
        /// <returns><see langword="true"/> if there are still packets remaining to be sent in future updates.</returns>
        public bool SendAndCheckQueue()
        {
            bool hasPacketsToSend = SendNextPackets();
            if (!hasPacketsToSend)
                Interlocked.Exchange(ref _isAddedToPeerChannelSendQueue, 0);

            return hasPacketsToSend;
        }

        /// <summary>
        /// Abstract method to implement the specific logic for sending packets (e.g., windowing for reliable).
        /// </summary>
        /// <returns><see langword="true"/> if packets were sent and the channel should remain in the send queue.</returns>
        public abstract bool SendNextPackets();

        /// <summary>
        /// Abstract method to handle an incoming packet received on this specific channel.
        /// </summary>
        /// <param name="packet">The received packet.</param>
        /// <returns><see langword="true"/> if the packet was processed successfully.</returns>
        public abstract bool ProcessPacket(NetPacket packet);
    }
}
