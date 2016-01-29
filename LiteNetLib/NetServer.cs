using System;
using System.Collections.Generic;

namespace LiteNetLib
{
    public sealed class NetServer : NetBase
    {
        private readonly Dictionary<NetEndPoint, NetPeer> _peers;
        private readonly int _maxClients;
        private readonly Queue<NetEndPoint> _peersToRemove;
        private long _timeout = 5000;

        public long DisconnectTimeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        public NetServer(int maxClients)
        {
            _peers = new Dictionary<NetEndPoint, NetPeer>(maxClients);
            _peersToRemove = new Queue<NetEndPoint>(maxClients);
            _maxClients = maxClients;
        }

        public int PeersCount
        {
            get { return _peers.Count; }
        }

        public NetPeer[] GetPeers()
        {
            NetPeer[] peers;

            lock (_peers)
            {
                peers = new NetPeer[_peers.Count];
                int num = 0;
                foreach (NetPeer netPeer in _peers.Values)
                {
                    peers[num++] = netPeer;
                }
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
                peer.CreateAndSend(PacketProperty.Disconnect);
                RemovePeer(peer);
            }
        }

        public override void Stop()
        {
            foreach (NetPeer netPeer in _peers.Values)
            {
                netPeer.CreateAndSend(PacketProperty.Disconnect);
            }
            lock (_peers)
            {
                _peers.Clear();
            }

            base.Stop();
        }

        protected override void ProcessError(string errorMessage)
        {
            _peers.Clear();
            var netEvent = CreateEvent(NetEventType.Error);
            netEvent.AdditionalInfo = errorMessage;
            EnqueueEvent(netEvent);
        }

        protected override void PostProcessEvent(int deltaTime)
        {
            //Process acks
            lock (_peers)
            {
                foreach (NetPeer netPeer in _peers.Values)
                {
                    if (netPeer.TimeSinceLastPacket > _timeout)
                    {
                        var netEvent = CreateEvent(NetEventType.Disconnect);
                        netEvent.Peer = netPeer;
                        netEvent.RemoteEndPoint = netPeer.EndPoint;
                        netEvent.AdditionalInfo = "Timeout";
                        EnqueueEvent(netEvent);
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
        }

        internal override void ReceiveFromPeer(NetPacket packet, NetEndPoint remoteEndPoint)
        {
            NetPeer fromPeer;
            if (_peers.TryGetValue(remoteEndPoint, out fromPeer))
            {
                var netEvent = CreateEvent(NetEventType.Receive);
                netEvent.Peer = fromPeer;
                netEvent.RemoteEndPoint = fromPeer.EndPoint;
                netEvent.DataReader.SetSource(packet.GetPacketData());
                EnqueueEvent(netEvent);
            }
        }

        internal override void ProcessSendError(NetEndPoint remoteEndPoint, string errorMessage)
        {
            NetPeer fromPeer;
            if (_peers.TryGetValue(remoteEndPoint, out fromPeer))
            {
                var netEvent = CreateEvent(NetEventType.Disconnect);
                netEvent.Peer = fromPeer;
                netEvent.RemoteEndPoint = fromPeer.EndPoint;
                netEvent.AdditionalInfo = "Peer send error: " + errorMessage;
                EnqueueEvent(netEvent);
                RemovePeer(fromPeer);
            }
        }

        protected override void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint)
        {
            NetPacket packet;
            NetPeer netPeer;

            //Check peers
            if (_peers.TryGetValue(remoteEndPoint, out netPeer))
            {
                packet = netPeer.GetPacketFromPool(init: false);

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
                    var netEvent = CreateEvent(NetEventType.Disconnect);
                    netEvent.Peer = netPeer;
                    netEvent.RemoteEndPoint = netPeer.EndPoint;
                    netEvent.AdditionalInfo = "successfuly disconnected";
                    EnqueueEvent(netEvent);
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
                NetEndPoint peerEndPoint = remoteEndPoint.Clone();
                netPeer = CreatePeer(peerEndPoint);
                netPeer.Recycle(packet);
                netPeer.CreateAndSend(PacketProperty.Connect);

                lock (_peers)
                {
                    _peers.Add(peerEndPoint, netPeer);
                }

                var netEvent = CreateEvent(NetEventType.Connect);
                netEvent.Peer = netPeer;
                netEvent.RemoteEndPoint = peerEndPoint;
                EnqueueEvent(netEvent);
            }
        }

        public void SendToClients(byte[] data, int length, SendOptions options)
        {
            foreach (NetPeer netPeer in _peers.Values)
            {
                netPeer.Send(data, length, options);
            }
        }

        public void SendToClients(byte[] data, int length, SendOptions options, NetPeer excludePeer)
		{
			foreach (NetPeer netPeer in _peers.Values)
			{
				if(netPeer != excludePeer)
				{
                    netPeer.Send(data, length, options);
				}
			}
		}
    }
}
