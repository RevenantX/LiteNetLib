using System;
using System.Collections.Generic;

namespace LiteNetLib
{
    public class NetServer : NetBase
    {
        private readonly Dictionary<NetEndPoint, NetPeer> _peers;
        private readonly int _maxClients;
        private readonly Queue<NetEndPoint> _peersToRemove;
        private long _timeout = 5000;

        public long DisconnectTimeout
        {
            get { return _timeout; }
            set { _timeout = DisconnectTimeout; }
        }

        public NetServer(int maxClients)
        {
            _peers = new Dictionary<NetEndPoint, NetPeer>(maxClients);
            _peersToRemove = new Queue<NetEndPoint>(maxClients);
            _maxClients = maxClients;
        }

        public NetPeer[] GetPeers()
        {
            NetPeer[] peers = new NetPeer[_peers.Count];

            int num = 0;
            foreach (NetPeer netPeer in _peers.Values)
            {
                peers[num++] = netPeer;
            }

            return peers;
        }

        private void RemovePeer(NetPeer peer)
        {
            _peersToRemove.Enqueue(peer.EndPoint);
        }

        public void DisconnectPeer(NetPeer peer)
        {
            if (peer != null && _peers.ContainsKey(peer.EndPoint))
            {
                peer.Send(PacketProperty.Disconnect);
                RemovePeer(peer);
            }
        }

        public override void Stop()
        {
            foreach (NetPeer netPeer in _peers.Values)
            {
                netPeer.Send(PacketProperty.Disconnect);
            }
            _peers.Clear();

            base.Stop();
        }

        protected override void ProcessError()
        {
            _peers.Clear();
            base.ProcessError();
        }

        protected override void PostProcessEvent(int deltaTime)
        {
            //Process acks
            foreach (NetPeer netPeer in _peers.Values)
            {
                if (netPeer.Ping > _timeout)
                {
                    EnqueueEvent(netPeer, null, NetEventType.Disconnect);
                    RemovePeer(netPeer);
                }
                else
                {
                    netPeer.Update(deltaTime);
                }
            }
            while (_peersToRemove.Count > 0)
            {
                _peers.Remove(_peersToRemove.Dequeue());
            }
        }

        internal override void ReceiveFromPeer(NetPacket packet, NetEndPoint remoteEndPoint)
        {
            if (_peers.ContainsKey(remoteEndPoint))
            {
                EnqueueEvent(_peers[remoteEndPoint], packet.Data, NetEventType.Receive);
            }
        }

        internal override void ProcessSendError(NetEndPoint remoteEndPoint)
        {
            if (_peers.ContainsKey(remoteEndPoint))
            {
                NetPeer peer = _peers[remoteEndPoint];

                EnqueueEvent(peer, null, NetEventType.Disconnect);
                RemovePeer(peer);
            }
        }

        protected override void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint)
        {
            NetPacket packet;
            //Check peers
            if (_peers.ContainsKey(remoteEndPoint))
            {
                NetPeer netPeer = _peers[remoteEndPoint];
                packet = netPeer.CreatePacket();

                //Bad packet check
                if (!packet.FromBytes(reusableBuffer, count))
                {
                    netPeer.Recycle(packet);
                    return;
                }
                
                //Send
                if (packet.Property == PacketProperty.Disconnect)
                {
                    RemovePeer(netPeer);
                    EnqueueEvent(netPeer, null, NetEventType.Disconnect);
                }
                else
                {
                    netPeer.ProcessPacket(packet);
                }
                return;
            }

            //Else add new peer
            packet = new NetPacket();
            if (!packet.FromBytes(reusableBuffer, count))
            {
                //Bad packet
                return;
            }
            if (_peers.Count < _maxClients && packet.Property == PacketProperty.Connect)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NS] Received peer connect request: accepting");
                //Getting new id for peer

                NetPeer netPeer = new NetPeer(this, _socket, remoteEndPoint);
                netPeer.BadRoundTripTime = UpdateTime * 2 + 250;
                netPeer.Recycle(packet);
                netPeer.Send(PacketProperty.Connect);

                _peers.Add(remoteEndPoint, netPeer);

                EnqueueEvent(netPeer, null, NetEventType.Connect);
            }
        }

        public void SendToClients(byte[] data, SendOptions options)
        {
            foreach (NetPeer netPeer in _peers.Values)
            {
                netPeer.Send(data, options);
            }
        }

        public void SendToClients(byte[] data, SendOptions options, NetPeer excludePeer)
		{
			foreach (NetPeer netPeer in _peers.Values)
			{
				if(netPeer != excludePeer)
				{
                    netPeer.Send(data, options);
				}
			}
		}
    }
}
