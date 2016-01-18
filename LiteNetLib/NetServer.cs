using System;
using System.Collections.Generic;
using System.Net;

namespace LiteNetLib
{
    public class NetServer : NetBase<NetServer>
    {
        private Dictionary<EndPoint, NetPeer> _peers;
        private int _maxClients;
        private long _timeout = 5000; //5sec
        private Queue<EndPoint> _peersToRemove;

        public long DisconnectTimeout
        {
            get { return _timeout; }
            set { _timeout = DisconnectTimeout; }
        }

        public NetServer(int maxClients)
        {
            _peers = new Dictionary<EndPoint, NetPeer>(maxClients);
            _peersToRemove = new Queue<EndPoint>(maxClients);
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

        public override bool Start(int port)
        {
            return base.Start(port);
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

        protected override NetEvent ProcessError()
        {
            _peers.Clear();
            return base.ProcessError();
        }

        protected override void PostProcessEvent(int deltaTime)
        {
            //Process acks
            foreach (NetPeer netPeer in _peers.Values)
            {
                if (netPeer.LastPing > _timeout)
                {
                    EnqueueEvent(new NetEvent(netPeer, null, NetEventType.Disconnect));
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

        public override void ReceiveFromPeer(NetPacket packet, EndPoint remoteEndPoint)
        {
            if (_peers.ContainsKey(remoteEndPoint))
            {
                EnqueueEvent(new NetEvent(_peers[remoteEndPoint], packet.Data, NetEventType.Receive));
            }
        }

        public override void ProcessSendError(EndPoint remoteEndPoint)
        {
            if (_peers.ContainsKey(remoteEndPoint))
            {
                NetPeer peer = _peers[remoteEndPoint];

                EnqueueEvent(new NetEvent(peer, null, NetEventType.Disconnect));
                RemovePeer(peer);
            }
        }

        protected override void ReceiveFromSocket(NetPacket packet, EndPoint remoteEndPoint)
        {
            //Check peers
            if(_peers.ContainsKey(remoteEndPoint))
            {
                NetPeer netPeer = _peers[remoteEndPoint];
                if (packet.Property == PacketProperty.Disconnect)
                {
                    RemovePeer(netPeer);
                    EnqueueEvent(new NetEvent(netPeer, null, NetEventType.Disconnect));
                }
                else
                {
                    netPeer.ProcessPacket(packet);
                }
            }

            //Add new peer
            if (_peers.Count < _maxClients && packet.Property == PacketProperty.Connect)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NS] Received peer connect request: accepting");
                //Getting new id for peer

                NetPeer netPeer = new NetPeer(this, _socket, (IPEndPoint)remoteEndPoint);
                netPeer.BadRoundTripTime = UpdateTime * 2 + 250;
                netPeer.ProcessPacket(packet);
                netPeer.Send(PacketProperty.Connect);

                _peers.Add(remoteEndPoint, netPeer);

                EnqueueEvent(new NetEvent(netPeer, null, NetEventType.Connect));
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
