#if WINRT && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Networking;
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
        private static readonly HostName MulticastAddressV4 = new HostName("224.0.0.1");
        private static readonly HostName MulticastAddressV6 = new HostName("FF02:0:0:0:0:0:0:1");

        public NetEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

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

        public bool Bind(int port)
        {
            _datagramSocket = new DatagramSocket();
            _datagramSocket.Control.InboundBufferSizeInBytes = NetConstants.SocketBufferSize;
            _datagramSocket.Control.DontFragment = true;
            _datagramSocket.MessageReceived += OnMessageReceived;

            try
            {
                _datagramSocket.BindServiceNameAsync(port.ToString()).AsTask().Wait();
                _datagramSocket.JoinMulticastGroup(MulticastAddressV4);
                _datagramSocket.JoinMulticastGroup(MulticastAddressV6);
                _localEndPoint = new NetEndPoint(_datagramSocket.Information.LocalAddress, _datagramSocket.Information.LocalPort);
            }
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[B]Bind exception: {0}", ex.ToString());
                return false;
            }
            return true;
        }

        public bool SendMulticast(byte[] data, int offset, int size, int port)
        {
            var portString = port.ToString();
            try
            {
                var outputStream =
                    _datagramSocket.GetOutputStreamAsync(MulticastAddressV4, portString)
                        .AsTask()
                        .Result;
                var writer = outputStream.AsStreamForWrite();
                writer.Write(data, offset, size);
                writer.Flush();

                outputStream =
                    _datagramSocket.GetOutputStreamAsync(MulticastAddressV6, portString)
                        .AsTask()
                        .Result;
                writer = outputStream.AsStreamForWrite();
                writer.Write(data, offset, size);
                writer.Flush();
            }
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[S][MCAST]" + ex);
                return false;
            }
            return true;
        }

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
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[S]" + ex);
                errorCode = -1;
                return -1;
            }
        }

        internal void RemovePeer(NetEndPoint ep)
        {
            _peers.Remove(ep);
        }

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
