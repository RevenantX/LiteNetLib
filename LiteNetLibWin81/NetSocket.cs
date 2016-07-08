#if WINRT && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Networking.Sockets;

namespace LiteNetLib
{
    internal sealed class NetSocket
    {
        private DatagramSocket _datagramSocket;
        private readonly Dictionary<NetEndPoint, Stream> _peers = new Dictionary<NetEndPoint, Stream>();
        private readonly NetBase.OnMessageReceived _onMessageReceived;
        private readonly byte[] _buffer = new byte[NetConstants.PacketSizeLimit];
        private NetEndPoint _bufferEndPoint;
        private NetEndPoint _localEndPoint;

        public NetEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        //Socket constructor
        public NetSocket(NetBase.OnMessageReceived onMessageReceived)
        {
            _onMessageReceived = onMessageReceived;
        }
        
        private void OnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var stream = args.GetDataStream().AsStreamForRead();
            int count = stream.Read(_buffer, 0, _buffer.Length);
            if (count > 0)
            {
                if (_bufferEndPoint == null ||
                    !_bufferEndPoint.HostName.IsEqual(args.RemoteAddress) ||
                    !_bufferEndPoint.PortStr.Equals(args.RemotePort))
                {
                    _bufferEndPoint = new NetEndPoint(args.RemoteAddress, args.RemotePort);
                }
                _onMessageReceived(_buffer, count, 0, _bufferEndPoint);
            }
        }

        //Bind socket to port
        public bool Bind(int port)
        {
            _datagramSocket = new DatagramSocket();
            _datagramSocket.Control.DontFragment = true;
            _datagramSocket.MessageReceived += OnMessageReceived;

            try
            {
                _datagramSocket.BindServiceNameAsync(port.ToString()).AsTask().Wait();
                _localEndPoint = new NetEndPoint(_datagramSocket.Information.LocalAddress, _datagramSocket.Information.LocalPort);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        //Send to
        public int SendTo(byte[] data, int offset, int length, NetEndPoint remoteEndPoint, ref int errorCode)
        {
            try
            {
                Stream writer;
                if (!_peers.TryGetValue(remoteEndPoint, out writer))
                {
                    var outputStream =
                        _datagramSocket.GetOutputStreamAsync(remoteEndPoint.HostName, remoteEndPoint.PortStr)
                            .AsTask()
                            .Result;
                    writer = outputStream.AsStreamForWrite();
                    _peers.Add(remoteEndPoint, writer);
                }

                writer.Write(data, offset, length);
                writer.Flush();
                return length;
            }
            catch (Exception)
            {
                errorCode = -1;
                return -1;
            }
        }

        internal void RemovePeer(NetEndPoint ep)
        {
            _peers.Remove(ep);
        }

        //Close socket
        public void Close()
        {
            //_datagramSocket.MessageReceived -= OnMessageReceived;
            _datagramSocket.Dispose();
            _datagramSocket = null;
            ClearPeers();
        }

        internal void ClearPeers()
        {
            foreach (var dataWriter in _peers)
            {
                dataWriter.Value.Dispose();
            }
            _peers.Clear();
        }
    }
}
#endif
