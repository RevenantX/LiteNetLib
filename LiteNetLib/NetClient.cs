using System;
using System.Text;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    public sealed class NetClient : NetBase
    {
        public int ReconnectDelay = 500;
        public int MaxConnectAttempts = 10;
        public bool PeerToPeerMode;

        private NetPeer _peer;
        private bool _connected;
        private int _connectAttempts;
        private int _connectTimer;
        private ulong _connectId;
        private readonly string _connectKey;
        private readonly object _connectionCloseLock = new object();

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
        /// Returns true if client connected
        /// </summary>
        public bool IsConnected
        {
            get { return _connected; }
        }

        private void CloseConnection(bool force, string info)
        {
            lock (_connectionCloseLock)
            {
                //Nothing to do
                if (!IsRunning)
                    return;

                //Send goodbye
                if (_peer != null && !force && _connected)
                {
                    //Send disconnect data
                    var disconnectPacket = NetPacket.CreateRawPacket(PacketProperty.Disconnect, 8);
                    FastBitConverter.GetBytes(disconnectPacket, 1, _connectId);
                    _peer.SendRawData(disconnectPacket);
                }

                //Close threads and socket close
                base.Stop();

                //Clear data
                _peer = null;
                _connected = false;
                _connectTimer = 0;
                _connectAttempts = 0;
                SocketClearPeers();

                //Send event to Listener
                var netEvent = CreateEvent(NetEventType.Disconnect);
                netEvent.AdditionalInfo = info;
                EnqueueEvent(netEvent);
            }
        }

        /// <summary>
        /// Force closes connection and stop all threads.
        /// </summary>
        public override void Stop()
        {
            CloseConnection(false, "Stop method called"); 
        }

        protected override void ProcessError(string errorMessage)
        {
            CloseConnection(true, errorMessage);
        }

        /// <summary>
        /// Connect to NetServer or NetClient with PeerToPeerMode
        /// </summary>
        /// <param name="address">Server IP or hostname</param>
        /// <param name="port">Server Port</param>
        public void Connect(string address, int port)
        {
            //Create target endpoint
            NetEndPoint ep = new NetEndPoint(address, port, AddressType);
            Connect(ep);
        }

        public void Connect(NetEndPoint target)
        {
            if (!IsRunning)
            {
                throw new Exception("Client is not running");
            }
            if (_peer != null)
            {
                //Already connected
                return;
            }

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

        protected override void PostProcessEvent(int deltaTime)
        {
            if (_peer == null)
                return;

            if (!_connected)
            {
                _connectTimer += deltaTime;
                if (_connectTimer > ReconnectDelay)
                {
                    _connectTimer = 0;
                    _connectAttempts++;
                    if (_connectAttempts > MaxConnectAttempts)
                    {
                        CloseConnection(true, "connection timeout");
                        return;
                    }

                    //else send connect again
                    SendConnectRequest();
                }
            }
            else if (_peer.TimeSinceLastPacket > DisconnectTimeout)
            {
                CloseConnection(true, "Timeout");
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
            CloseConnection(true, "Send error: " + errorMessage);
            base.ProcessSendError(remoteEndPoint, errorMessage);
        }

        private void ProcessConnectAccept()
        {
            if (_connected)
                return;

            NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received connection accept");
            _connected = true;
            var connectEvent = CreateEvent(NetEventType.Connect);
            connectEvent.Peer = _peer;
            EnqueueEvent(connectEvent);
            _peer.StartConnectionTimer();
        }

        protected override void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint)
        {
            //Parse packet
            //Peer null when P2P connection packets
            NetPacket packet = _peer == null ? new NetPacket() : _peer.GetPacketFromPool(init: false);
            if (!packet.FromBytes(reusableBuffer, count))
            {
                if(_peer != null)
                    _peer.Recycle(packet);
                return;
            }

            //Check P2P mode
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

                //Check our peer and create
                if (_peer == null)
                {
                    //Create connect id for proper connection
                    Connect(remoteEndPoint);
                }

                //Send raw
                _peer.SendRawData(connectPacket);

                //clean incoming packet
                _peer.Recycle(packet);

                //We connected
                ProcessConnectAccept();

                return;
            }

            //Check peer
            if (_peer == null)
            {
                return;
            }

            //Check endpoint 
            if (!_peer.EndPoint.Equals(remoteEndPoint))
            {
                NetUtils.DebugWriteForce(ConsoleColor.DarkCyan, "[NC] Bad EndPoint " + remoteEndPoint);
                return;
            }

            if (packet.Property == PacketProperty.Disconnect)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received disconnection");
                CloseConnection(true, "Received disconnection from server");
                return;
            }

            if (packet.Property == PacketProperty.ConnectAccept)
            {
                if (_connected)
                {
                    return;
                }

                //check connection id
                if (BitConverter.ToUInt64(packet.RawData, 1) != _connectId)
                {
                    return;
                }

                //connection things
                ProcessConnectAccept();
                return;
            }

            //Process income packet
            _peer.ProcessPacket(packet);
        }
    }
}
