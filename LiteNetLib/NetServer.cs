using System;
using System.Collections.Generic;
using System.Text;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    public sealed class NetServer : NetBase
    {
        private readonly Dictionary<NetEndPoint, NetPeer> _peers;
        private readonly Dictionary<NetEndPoint, ulong> _peerConnectionIds; 
        private readonly int _maxClients;
        private readonly Queue<NetEndPoint> _peersToRemove;
        private readonly string _connectKey;

        /// <summary>
        /// Creates server object
        /// </summary>
        /// <param name="maxClients">Maximum clients</param>
        /// <param name="key">Application key to identify connecting clients</param>
        public NetServer(INetEventListener listener, int maxClients, string key) : this(listener, ConnectionAddressType.IPv4, maxClients, key)
        {
   
        }

        public NetServer(INetEventListener listener, ConnectionAddressType addressType, int maxClients, string key) : base(listener, addressType)
        {
            _peers = new Dictionary<NetEndPoint, NetPeer>();
            _peerConnectionIds = new Dictionary<NetEndPoint, ulong>();
            _peersToRemove = new Queue<NetEndPoint>(maxClients);
            _maxClients = maxClients;
            _connectKey = key;
        }

        public int PeersCount
        {
            get { return _peers.Count; }
        }

        /// <summary>
        /// Get copy of current connected peers
        /// </summary>
        /// <returns>Array with connected peers</returns>
        public NetPeer[] GetPeers()
        {
            NetPeer[] peers;
            int num = 0;

            lock (_peers)
            {
                peers = new NetPeer[_peers.Count];
                foreach (NetPeer netPeer in _peers.Values)
                {
                    peers[num++] = netPeer;
                }
            }

            return peers;
        }

        private void RemovePeer(NetPeer peer)
        {
            lock (_peers)
            {
                _peers.Remove(peer.EndPoint);
                _peerConnectionIds.Remove(peer.EndPoint);
                SocketRemovePeer(peer.EndPoint);
            }
        }

        /// <summary>
        /// Disconnect peer from server
        /// </summary>
        /// <param name="peer">peer to disconnect</param>
        public void DisconnectPeer(NetPeer peer)
        {
            if (peer != null && _peers.ContainsKey(peer.EndPoint))
            {
                peer.CreateAndSend(PacketProperty.Disconnect);
                var netEvent = CreateEvent(NetEventType.Disconnect);
                netEvent.Peer = peer;
                netEvent.AdditionalInfo = "Disconnect peer called";
                EnqueueEvent(netEvent);
                RemovePeer(peer);
            }
        }

        public override void Stop()
        {
            lock (_peers)
            foreach (NetPeer netPeer in _peers.Values)
            {
                netPeer.CreateAndSend(PacketProperty.Disconnect);
            }
            ClearPeers();
            base.Stop();
        }

        private void ClearPeers()
        {
            lock (_peers)
            {
                _peers.Clear();
                _peerConnectionIds.Clear();
            }
        }

        protected override void ProcessError(string errorMessage)
        {
            ClearPeers();
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
                    if (netPeer.TimeSinceLastPacket > DisconnectTimeout)
                    {
                        netPeer.DebugWrite("Disconnect by timeout: {0} > {1}", netPeer.TimeSinceLastPacket, DisconnectTimeout);
                        var netEvent = CreateEvent(NetEventType.Disconnect);
                        netEvent.Peer = netPeer;
                        netEvent.AdditionalInfo = "Timeout";
                        EnqueueEvent(netEvent);
                        _peersToRemove.Enqueue(netPeer.EndPoint);
                    }
                    else
                    {
                        netPeer.Update(deltaTime);
                    }
                }
                while (_peersToRemove.Count > 0)
                {
                    var ep = _peersToRemove.Dequeue();
                    _peers.Remove(ep);
                    _peerConnectionIds.Remove(ep);
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
                netEvent.AdditionalInfo = "Peer send error: " + errorMessage;
                EnqueueEvent(netEvent);
                RemovePeer(fromPeer);
            }
        }

        private void SendConnectAccept(NetPeer peer, ulong id)
        {
            //Reset connection timer
            peer.StartConnectionTimer();

            //Make initial packet
            var connectPacket = NetPacket.CreateRawPacket(PacketProperty.ConnectAccept, 8);

            //Add data
            FastBitConverter.GetBytes(connectPacket, 1, id);

            //Send raw
            peer.SendRawData(connectPacket);
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
                    if (BitConverter.ToUInt64(packet.RawData, 1) != _peerConnectionIds[remoteEndPoint])
                    {
                        //Old or incorrect disconnect
                        netPeer.Recycle(packet);
                        return;
                    }
                    RemovePeer(netPeer);
                    var netEvent = CreateEvent(NetEventType.Disconnect);
                    netEvent.Peer = netPeer;
                    netEvent.AdditionalInfo = "successfuly disconnected";
                    EnqueueEvent(netEvent);
                }
                else if (packet.Property == PacketProperty.ConnectRequest) //response with connect
                {
                    ulong lastId = _peerConnectionIds[remoteEndPoint];
                    ulong newId = BitConverter.ToUInt64(packet.RawData, 1);
                    if (newId > lastId)
                    {
                        _peerConnectionIds[remoteEndPoint] = newId;
                    }
                    
                    NetUtils.DebugWrite(ConsoleColor.Blue, "ConnectRequest LastId: {0}, NewId: {1}", lastId, newId);
                    SendConnectAccept(netPeer, _peerConnectionIds[remoteEndPoint]);
                    netPeer.Recycle(packet);
                }
                else //throw out garbage packets
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

            if (_peers.Count < _maxClients && packet.Property == PacketProperty.ConnectRequest)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NS] Received peer connect request");

                string peerKey = Encoding.UTF8.GetString(packet.RawData, 9, packet.RawData.Length - 9);
                if (peerKey != _connectKey)
                {
                    NetUtils.DebugWrite(ConsoleColor.Cyan, "[NS] Peer connect reject. Invalid key: " + peerKey);
                    return;
                }

                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NS] Peer connect accepting");
                //Getting new id for peer
                netPeer = CreatePeer(remoteEndPoint);

                //response with id
                ulong connectionId = BitConverter.ToUInt64(packet.RawData, 1);
                SendConnectAccept(netPeer, connectionId);

                //clean incoming packet
                netPeer.Recycle(packet);

                lock (_peers)
                {
                    _peers.Add(remoteEndPoint, netPeer);
                    _peerConnectionIds.Add(remoteEndPoint, connectionId);
                }

                var netEvent = CreateEvent(NetEventType.Connect);
                netEvent.Peer = netPeer;
                EnqueueEvent(netEvent);
            }
        }

        public void SendToClients(byte[] data, SendOptions options)
        {
            lock (_peers)
            foreach (NetPeer netPeer in _peers.Values)
            {
                netPeer.Send(data, options);
            }
        }

        public void SendToClients(byte[] data, int start, int length, SendOptions options)
        {
            lock (_peers)
            foreach (NetPeer netPeer in _peers.Values)
            {
                netPeer.Send(data, start, length, options);
            }
        }

        public void SendToClients(byte[] data, SendOptions options, NetPeer excludePeer)
		{
            lock (_peers)
            foreach (NetPeer netPeer in _peers.Values)
			{
				if(netPeer != excludePeer)
				{
                    netPeer.Send(data, options);
				}
			}
		}

        public void SendToClients(byte[] data, int start, int length, SendOptions options, NetPeer excludePeer)
        {
            lock (_peers)
            foreach (NetPeer netPeer in _peers.Values)
            {
                if (netPeer != excludePeer)
                {
                    netPeer.Send(data, start, length, options);
                }
            }
        }
    }
}
