using System;

namespace LiteNetLib
{
    public sealed class NetClient : NetBase
    {
        private NetPeer _peer;
        private bool _connected;
        private long _id;
        private int _maxConnectAttempts = 10;
        private int _connectAttempts;
        private int _connectTimer;
        private int _reconnectDelay = 500;
        private bool _waitForConnect;
        private long _timeout = 5000;

        public long DisconnectTimeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        public long Id
        {
            get { return _id; }
        }

        public override bool Start(int port)
        {
            bool result = base.Start(port);
            if (result)
            {
                _id = _localEndPoint.GetId();
            }
            return result;
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
            if (_peer != null)
            {
                if (!force)
                {
                    _peer.CreateAndSend(PacketProperty.Disconnect);
                }
                _peer = null;
            }
            _connected = false;
            _connectTimer = 0;
            _connectAttempts = 0;
            _waitForConnect = false;
            _id = 0;
        }

        public override void Stop()
        {
            CloseConnection(false);
            base.Stop();
        }

        protected override void ProcessError()
        {
            EnqueueEvent(NetEventType.Error);
            if (_peer != null)
            {
                CloseConnection(true);
            }
        }

        /// <summary>
        /// Connect to NetServer
        /// </summary>
        /// <param name="address">Server IP or hostname</param>
        /// <param name="port">Server Port</param>
        public void Connect(string address, int port)
        {
            //Create server endpoint
            NetEndPoint ep = new NetEndPoint(address, port);

            //Force close connection
            CloseConnection(true);

            //Create reliable connection
            _peer = new NetPeer(this, _socket, ep);
            _peer.DebugTextColor = ConsoleColor.Yellow;
            _peer.BadRoundTripTime = UpdateTime * 2 + 250;
            _peer.CreateAndSend(PacketProperty.Connect);

            _connectAttempts = 0;
            _waitForConnect = true;
        }

        protected override void PostProcessEvent(int deltaTime)
        {
            if (_peer == null)
                return;

            if (_waitForConnect)
            {
                _connectTimer += deltaTime;
                if (_connectTimer > _reconnectDelay)
                {
                    _connectTimer = 0;
                    _connectAttempts++;
                    if (_connectAttempts > _maxConnectAttempts)
                    {
                        EnqueueEvent(NetEventType.Disconnect);
                        Stop();
                        return;
                    }

                    //else
                    _peer.CreateAndSend(PacketProperty.Connect);
                }
            }

            _peer.Update(deltaTime);

            if (_peer.TimeSinceLastPacket > _timeout)
            {
                Stop();
                EnqueueEvent(NetEventType.Disconnect);
            }
        }

        internal override void ReceiveFromPeer(NetPacket packet, NetEndPoint remoteEndPoint)
        {
            NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received message");
            EnqueueEvent(_peer, packet.GetPacketData(), NetEventType.Receive);
        }

        internal override void ProcessSendError(NetEndPoint remoteEndPoint)
        {
            Stop();
            EnqueueEvent(NetEventType.Error);
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
                EnqueueEvent(NetEventType.Disconnect);
                return;
            }

            if (packet.Property == PacketProperty.Connect)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received connection accept");
                _waitForConnect = false;
                _connected = true;
                EnqueueEvent(_peer, null, NetEventType.Connect);
                return;
            }

            //Process income packet
            _peer.ProcessPacket(packet);
        }
    }
}
