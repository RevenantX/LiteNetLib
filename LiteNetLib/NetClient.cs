using System;
using System.Net;

namespace LiteNetLib
{
    public class NetClient : NetBase<NetClient>
    {
        private NetPeer _peer;
        private bool _connected;

        public NetClient()
        {

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

        protected override NetEvent ProcessError()
        {
            if (_peer != null)
            {
                CloseConnection(true);
                return new NetEvent(null, null, NetEventType.Error);
            }
            else
            {
                return base.ProcessError();
            }
        }

        /// <summary>
        /// Connect to NetServer
        /// </summary>
        /// <param name="address">Server IP or hostname</param>
        /// <param name="port">Server Port</param>
        public void Connect(string address, int port)
        {   
            //Parse ip address
            IPAddress ipAddress = NetUtils.GetHostIP(address);

            //Create server endpoint
            EndPoint ep = new IPEndPoint(ipAddress, port);

            //Force close connection
            CloseConnection(true);
            //Create reliable connection
            _peer = new NetPeer(this, socket, ep);
            _peer.DebugTextColor = ConsoleColor.Yellow;
            _peer.BadRoundTripTime = UpdateTime * 2 + 250;
            _peer.Send(PacketProperty.Connect);
        }

        protected override void PostProcessEvent(int deltaTime)
        {
            if (_peer != null)
            {
                _peer.Update(deltaTime);
                //Console.WriteLine("PQ: {0}, RQ: {1}", _peer.GetConnection().PendingAckQueue, _peer.GetConnection().ReceivedQueueSize);
            }
        }

        public override void ProcessReceivedPacket(NetPacket packet, EndPoint remoteEndPoint)
        {
            EnqueueEvent(new NetEvent(_peer, packet.data, NetEventType.Receive));
        }

        public override void ProcessSendError(EndPoint remoteEndPoint)
        {
            Stop();
            EnqueueEvent(new NetEvent(null, null, NetEventType.Error));
        }

        protected override NetEvent ProcessPacket(NetPacket packet, EndPoint remoteEndPoint)
        {
            if (_peer == null)
				return null;
            if (!_peer.EndPoint.Equals(remoteEndPoint))
            {
                NetUtils.DebugWrite(ConsoleColor.DarkCyan, "[NC] Bad remoteEndPoint " + remoteEndPoint);
                return null;
            }
            //Process income packet
            bool packetHasInfo = _peer.ProcessPacket(packet);

            if (packetHasInfo)
            {
                if (packet.info == PacketInfo.Disconnect)
                {
                    NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received disconnection");
                    CloseConnection(true);
                    return new NetEvent(null, null, NetEventType.Disconnect);
                }
                else if (packet.info == PacketInfo.Connect && packet.data.Length == 4)
                {
                    NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received connection accept");
                    //_peer = new NetPeer(_connection, BitConverter.ToInt32(packet.data, 0));
                    _peer.Id = BitConverter.ToInt32(packet.data, 0);
                    _connected = true;
                    return new NetEvent(_peer, null, NetEventType.Connect);
                }
                else
                {
                    NetUtils.DebugWrite(ConsoleColor.Cyan, "[NC] Received message");
                    if (_peer == null)
                        return null;

                    return new NetEvent(_peer, packet.data, NetEventType.Receive);
                }
            }
            return null;
        }
    }
}
