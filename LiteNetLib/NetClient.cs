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

        private void CloseConnection(bool force, DisconnectReason reason, int socketErrorCode)
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
                    SendRaw(disconnectPacket, _peer.EndPoint);
                }

                //Clear data
                _peer = null;
                _connected = false;
                _connectTimer = 0;
                _connectAttempts = 0;
                SocketClearPeers();

                //Send event to Listener
                var netEvent = CreateEvent(NetEventType.Disconnect);
                netEvent.DisconnectReason = reason;
                netEvent.AdditionalData = socketErrorCode;
                EnqueueEvent(netEvent);
            }
        }

        /// <summary>
        /// Force closes connection and stop all threads.
        /// </summary>
        public override void Stop()
        {
            CloseConnection(true, DisconnectReason.DisconnectCalled, 0);
            base.Stop();
        }

        protected override void ProcessReceiveError(int socketErrorCode)
        {
            CloseConnection(true, DisconnectReason.SocketReceiveError, socketErrorCode);
            base.ProcessReceiveError(socketErrorCode);
        }

        /// <summary>
        /// Disconnect from NetServer
        /// </summary>
        public void Disconnect()
        {
            if (!_connected)
            {
                return;
            }
            CloseConnection(false, DisconnectReason.DisconnectCalled, 0);
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
            if (_peer != null)
            {
                //Already connected
                return;
            }

            //Create connect id for proper connection
            _connectId = (ulong)DateTime.UtcNow.Ticks;
            NetUtils.DebugWrite(ConsoleColor.Cyan, "[CC] ConnectId: {0}", _connectId);

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
            SendRaw(connectPacket, _peer.EndPoint);
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
                        CloseConnection(true, DisconnectReason.ConnectionFailed, 0);
                        return;
                    }

                    //else send connect again
                    SendConnectRequest();
                }
            }
            else if (_peer.TimeSinceLastPacket > DisconnectTimeout)
            {
                CloseConnection(true, DisconnectReason.Timeout, 0);
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

        internal override void ProcessSendError(NetEndPoint remoteEndPoint, int socketErrorCode)
        {
            CloseConnection(true, DisconnectReason.SocketSendError, socketErrorCode);
            base.ProcessSendError(remoteEndPoint, socketErrorCode);
        }

        private void ProcessConnectAccept()
        {
            if (_connected)
                return;

            NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received connection accept");
            _peer.StartConnectionTimer();
            _connected = true;
            var connectEvent = CreateEvent(NetEventType.Connect);
            connectEvent.Peer = _peer;
            EnqueueEvent(connectEvent);
        }

        protected override void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint)
        {
            //Parse packet
            //Peer null when P2P connection packets
            NetPacket packet = _peer == null ? new NetPacket() : _peer.GetPacketFromPool(init: false);
            if (!packet.FromBytes(reusableBuffer, 0, count))
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
                SendRaw(connectPacket, remoteEndPoint);

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
                CloseConnection(true, DisconnectReason.RemoteConnectionClose, 0);
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
