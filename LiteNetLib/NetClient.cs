using System;
using System.Text;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    public sealed class NetClient : NetBase
    {
        private NetPeer _peer;
        private bool _connected;
        private int _maxConnectAttempts = 10;
        private int _connectAttempts;
        private int _connectTimer;
        private int _reconnectDelay = 500;
        private ulong _connectId;
        private string _connectKey;

        public bool PeerToPeerMode;

        public NetClient(INetEventListener listener, string connectKey) : base(listener)
        {
            _connectKey = connectKey;
        }

        public NetClient(INetEventListener listener, string connectKey, ConnectionAddressType addressType) : base(listener, addressType)
        {
            _connectKey = connectKey;
        }

        public int Ping
        {
            get { return _peer == null ? 0 : _peer.Ping; }
        }

        /// <summary>
        /// Returns client NetPeer
        /// </summary>
        /// <returns></returns>
        public NetPeer Peer
        {
            get { return _peer; }
        }

        /// <summary>
        /// Returns true if client connected to server
        /// </summary>
        public bool IsConnected
        {
            get { return _connected; }
        }

        private void CloseConnection(bool force)
        {
            if (_peer != null && !force)
            {
                //Send disconnect data
                var disconnectPacket = NetPacket.CreateRawPacket(PacketProperty.Disconnect, 8);
                FastBitConverter.GetBytes(disconnectPacket, 1, _connectId);
                _peer.SendRawData(disconnectPacket);
            }
            _peer = null;
            _connected = false;
            _connectTimer = 0;
            _connectAttempts = 0;
            SocketClearPeers();
        }

        public override void Stop()
        {
            CloseConnection(false);
            base.Stop();
        }

        protected override void ProcessError(string errorMessage)
        {
            var netEvent = CreateEvent(NetEventType.Disconnect);
            netEvent.AdditionalInfo = errorMessage;
            EnqueueEvent(netEvent);
            if (_peer != null)
            {
                CloseConnection(true);
            }
        }

        /// <summary>
        /// Connect to NetServer or NetClient with PeerToPeerMode
        /// </summary>
        /// <param name="address">Server IP or hostname</param>
        /// <param name="port">Server Port</param>
        public void Connect(string address, int port)
        {
            //Create target endpoint
            NetEndPoint ep = new NetEndPoint(address, port);
            Connect(ep);
        }

        public void Connect(NetEndPoint target)
        {
            if (!IsRunning)
            {
                throw new Exception("Client is not running");
            }

            //Force close connection
            CloseConnection(true);

            //Create connect id for proper connection
            _connectId = (ulong)DateTime.UtcNow.Ticks;

            //Create reliable connection
            _peer = CreatePeer(target);
            _peer.DebugTextColor = ConsoleColor.Yellow;

            //Create connection packet and send
            SendConnectRequest();

            _connectAttempts = 0;
        }

        private void SendConnectRequest()
        {
            //Get connect key bytes
            byte[] keyData = Encoding.UTF8.GetBytes(_connectKey);

            //Make initial packet
            var connectPacket = NetPacket.CreateRawPacket(PacketProperty.ConnectRequest, 8+keyData.Length);

            //Add data
            FastBitConverter.GetBytes(connectPacket, 1, _connectId);
            Buffer.BlockCopy(keyData, 0, connectPacket, 9, keyData.Length);

            //Send raw
            _peer.SendRawData(connectPacket);
        }

        public void Disconnect()
        {
            var netEvent = CreateEvent(NetEventType.Disconnect);
            netEvent.AdditionalInfo = "Disconnect method called";
            EnqueueEvent(netEvent);
            CloseConnection(false);
        }

        protected override void PostProcessEvent(int deltaTime)
        {
            if (_peer == null)
                return;

            if (!_connected)
            {
                _connectTimer += deltaTime;
                if (_connectTimer > _reconnectDelay)
                {
                    _connectTimer = 0;
                    _connectAttempts++;
                    if (_connectAttempts > _maxConnectAttempts)
                    {
                        var netEvent = CreateEvent(NetEventType.Disconnect);
                        netEvent.AdditionalInfo = "connection timeout";
                        EnqueueEvent(netEvent);
                        Stop();
                        return;
                    }

                    //else send connect again
                    SendConnectRequest();
                }
            }
            else if (_peer.TimeSinceLastPacket > DisconnectTimeout)
            {
                Stop();
                var netEvent = CreateEvent(NetEventType.Disconnect);
                netEvent.AdditionalInfo = "Timeout";
                EnqueueEvent(netEvent);
                return;
            }

            _peer.Update(deltaTime);
        }

        internal override void ReceiveFromPeer(NetPacket packet, NetEndPoint remoteEndPoint)
        {
            NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received message");
            var netEvent = CreateEvent(NetEventType.Receive);
            netEvent.DataReader.SetSource(packet.GetPacketData());
            netEvent.Peer = _peer;
            netEvent.RemoteEndPoint = remoteEndPoint;
            EnqueueEvent(netEvent);
        }

        internal override void ProcessSendError(NetEndPoint remoteEndPoint, string errorMessage)
        {
            CloseConnection(true);
            base.Stop();
            base.ProcessSendError(remoteEndPoint, errorMessage);
        }

        protected override void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint)
        {
            //Check peer
            if (_peer == null)
            {
                return;
            }

            //Check endpoint 
            if (!_peer.EndPoint.Equals(remoteEndPoint))
            {
                NetUtils.DebugWrite(ConsoleColor.DarkCyan, "[NC] Bad EndPoint " + remoteEndPoint);
                return;
            }

            //Parse packet
            NetPacket packet = _peer.GetPacketFromPool(init: false);
            if (!packet.FromBytes(reusableBuffer, count))
            {
                _peer.Recycle(packet);
            }

            if (packet.Property == PacketProperty.Disconnect)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received disconnection");
                CloseConnection(true);
                var disconnectEvent = CreateEvent(NetEventType.Disconnect);
                disconnectEvent.AdditionalInfo = "Received disconnection from server";
                EnqueueEvent(disconnectEvent);
                return;
            }

            if (PeerToPeerMode && packet.Property == PacketProperty.ConnectRequest)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received peer connect request");

                string peerKey = Encoding.UTF8.GetString(packet.RawData, 9, packet.RawData.Length - 9);
                if (peerKey != _connectKey)
                {
                    NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Peer connect reject. Invalid key: " + peerKey);
                    return;
                }

                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Peer connect accepting");

                //Make initial packet and put id from received packet
                var connectPacket = NetPacket.CreateRawPacket(PacketProperty.ConnectAccept, 8);
                Buffer.BlockCopy(packet.RawData, 1, connectPacket, 1, 8);

                //Send raw
                _peer.SendRawData(connectPacket);

                //clean incoming packet
                _peer.Recycle(packet);

                return;
            }

            if (packet.Property == PacketProperty.ConnectAccept)
            {
                if (_connected)
                {
                    return;
                }

                //get id
                if (BitConverter.ToUInt64(packet.RawData, 1) != _connectId)
                {
                    return;
                }

                //connection things
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received connection accept");
                _connected = true;
                var connectEvent = CreateEvent(NetEventType.Connect);
                connectEvent.Peer = _peer;
                EnqueueEvent(connectEvent);

                _peer.StartConnectionTimer();
                return;
            }

            //Process income packet
            _peer.ProcessPacket(packet);
        }
    }
}
