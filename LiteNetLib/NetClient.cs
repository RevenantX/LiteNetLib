using System;
using System.Net;
#if !WINRT
using System.Net.Sockets;
#endif

namespace LiteNetLib
{
    public class NetClient : NetBase
    {
        private NetPeer _peer;
        private bool _connected;
        private long _id;
        private int _maxConnectAttempts = 10;
        private int _connectAttempts;
        private int _connectTimer;
        private int _reconnectDelay = 500;
        private bool _waitForConnect;

        public long Id
        {
            get { return _id; }
        }

        public NetClient()
        {

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
        public NetPeer GetPeer()
        {
            return _peer;
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
                    _peer.Send(PacketProperty.Disconnect);
                }
                _peer = null;
            }
            _connected = false;
        }

        public override void Stop()
        {
            CloseConnection(false);
            base.Stop();
        }

        protected override void ProcessError()
        {
            if (_peer != null)
            {
                CloseConnection(true);
                EnqueueEvent(null, null, NetEventType.Error);
            }
            else
            {
                base.ProcessError();
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
            _peer.Send(PacketProperty.Connect);

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
                        EnqueueEvent(null, null, NetEventType.Disconnect);
                        Stop();
                        return;
                    }

                    //else
                    _peer.Send(PacketProperty.Connect);
                }
            }

             _peer.Update(deltaTime);
        }

        internal override void ReceiveFromPeer(NetPacket packet, NetEndPoint remoteEndPoint)
        {
            NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received message");
            EnqueueEvent(_peer, packet.Data, NetEventType.Receive);
            //_peer.Recycle(packet);
        }

        internal override void ProcessSendError(NetEndPoint remoteEndPoint)
        {
            Stop();
            EnqueueEvent(null, null, NetEventType.Error);
        }

        protected override void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint)
        {
            if (_peer == null)
				return;

            NetPacket packet = _peer.GetOrCreatePacket();
            if (!packet.FromBytes(reusableBuffer, count))
            {
                _peer.Recycle(packet);
            }

            if (!_peer.EndPoint.Equals(remoteEndPoint))
            {
                NetUtils.DebugWrite(ConsoleColor.DarkCyan, "[NC] Bad EndPoint " + remoteEndPoint);
                return;
            }

            if (packet.Property == PacketProperty.Disconnect)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received disconnection");
                CloseConnection(true);
                EnqueueEvent(null, null, NetEventType.Disconnect);
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
