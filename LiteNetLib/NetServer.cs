using System;
using System.Collections.Generic;
using System.Net;

namespace LiteNetLib
{
    public class NetServer : NetBase<NetServer>
    {
        private Dictionary<EndPoint, NetPeer> _peers;
        private int _maxClients;
        private Stack<int> _idList;
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

        private void CreateIdList()
        {
            if (_idList == null)
            {
                _idList = new Stack<int>(_maxClients);
            }
            else
            {
                _idList.Clear();
            }
            for (int i = 0; i < _maxClients; i++)
            {
                _idList.Push(_maxClients - i);
            }
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
            _idList.Push(peer.Id);
        }

        public void DisconnectPeer(NetPeer peer)
        {
            if (peer != null && _peers.ContainsKey(peer.EndPoint))
            {
                peer.SendInfo(PacketInfo.Disconnect);
                RemovePeer(peer);
            }
        }

        public override bool Start(int port)
        {
            CreateIdList();
            return base.Start(port);
        }

        public override void Stop()
        {
            foreach (NetPeer netPeer in _peers.Values)
            {
                netPeer.SendInfo(PacketInfo.Disconnect);
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
                    CallNetEventReceived(new NetEvent(netPeer, null, NetEventType.Disconnect));
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

        public override void ProcessReceivedPacket(NetPacket packet, EndPoint remoteEndPoint)
        {
            if (_peers.ContainsKey(remoteEndPoint))
            {
                CallNetEventReceived(new NetEvent(_peers[remoteEndPoint], packet.data, NetEventType.Receive));
            }
        }

        private void OnSendError(EndPoint remoteEndPoint)
        {
            if (_peers.ContainsKey(remoteEndPoint))
            {
                NetPeer peer = _peers[remoteEndPoint];

                CallNetEventReceived(new NetEvent(peer, null, NetEventType.Disconnect));
                RemovePeer(peer);
            }
        }

        protected override NetEvent ProcessPacket(NetPacket packet, EndPoint remoteEndPoint)
        {
            //Check peers
            if(_peers.ContainsKey(remoteEndPoint))
            {
                NetPeer netPeer = _peers[remoteEndPoint];
                if (packet.info == PacketInfo.Disconnect)
                {
                    RemovePeer(netPeer);
                    return new NetEvent(netPeer, null, NetEventType.Disconnect);
                }
                else if (netPeer.ProcessPacket(packet))
                {
                    return new NetEvent(netPeer, packet.data, NetEventType.Receive);
                }
                else
                {
                    return null;
                }
            }

            //Add new peer
            if (_peers.Count < _maxClients && packet.info == PacketInfo.Connect)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NS] Received peer connect request: accepting");
                //Getting new id for peer
                int peerId = _idList.Pop();
                byte[] peerIdData = BitConverter.GetBytes(peerId);

                NetPeer netPeer = new NetPeer(this, socket, remoteEndPoint, peerId);
                netPeer.BadRoundTripTime = UpdateTime * 2 + 250;
                netPeer.ProcessPacket(packet);
                netPeer.SendInfo(PacketInfo.Connect, peerIdData);

                _peers.Add(remoteEndPoint, netPeer);

                return new NetEvent(netPeer, null, NetEventType.Connect);
            }

            return null;
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
