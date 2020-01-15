using System;

namespace LiteNetLib
{
    internal sealed class SequencedChannel : BaseChannel
    {
        private int _localSequence;
        private ushort _remoteSequence;
        private readonly bool _reliable;
        private NetPacket _lastPacket;
        private readonly NetPacket _ackPacket;
        private bool _mustSendAck;
        private readonly byte _id;
        private long _lastPacketSendTime;

        public SequencedChannel(NetPeer peer, bool reliable, byte id) : base(peer)
        {
            _id = id;
            _reliable = reliable;
            if (_reliable)
                _ackPacket = new NetPacket(PacketProperty.Ack, 0) {ChannelId = id};
        }

        public override void SendNextPackets()
        {
            if (_reliable && OutgoingQueue.Count == 0)
            {
                long currentTime = DateTime.UtcNow.Ticks;
                long packetHoldTime = currentTime - _lastPacketSendTime;
                if (packetHoldTime < Peer.ResendDelay * TimeSpan.TicksPerMillisecond)
                    return;
                var packet = _lastPacket;
                if (packet != null)
                {
                    _lastPacketSendTime = currentTime;
                    Peer.SendUserData(packet);
                }
            }
            else
            {
                lock (OutgoingQueue)
                {
                    while (OutgoingQueue.Count > 0)
                    {
                        NetPacket packet = OutgoingQueue.Dequeue();
                        _localSequence = (_localSequence + 1) % NetConstants.MaxSequence;
                        packet.Sequence = (ushort)_localSequence;
                        packet.ChannelId = _id;
                        Peer.SendUserData(packet);

                        if (_reliable && OutgoingQueue.Count == 0)
                        {
                            _lastPacketSendTime = DateTime.UtcNow.Ticks;
                            _lastPacket = packet;
                        }
                        else
                        {
                            Peer.NetManager.NetPacketPool.Recycle(packet);
                        }
                    }
                }
            }

            if (_reliable && _mustSendAck)
            {
                _mustSendAck = false;
                _ackPacket.Sequence = _remoteSequence;
                Peer.SendUserData(_ackPacket);
            }
        }

        public override bool ProcessPacket(NetPacket packet)
        {
            if (packet.Property == PacketProperty.Ack)
            {
                if (_reliable && _lastPacket != null && packet.Sequence == _lastPacket.Sequence)
                    _lastPacket = null;
                return false;
            }
            int relative = NetUtils.RelativeSequenceNumber(packet.Sequence, _remoteSequence);
            bool packetProcessed = false;
            if (packet.Sequence < NetConstants.MaxSequence && relative > 0)
            {
                Peer.Statistics.PacketLoss += (ulong)(relative - 1);
                _remoteSequence = packet.Sequence;
                Peer.AddIncomingPacket(packet);
                packetProcessed = true;
            }
            _mustSendAck = true;
            return packetProcessed;
        }
    }
}
