#if WINRT && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace LiteNetLib
{
    internal sealed class NetSocket
    {
        private DatagramSocket _datagramSocket;
        private readonly Dictionary<NetEndPoint, IOutputStream> _peers = new Dictionary<NetEndPoint, IOutputStream>();
        private readonly NetBase.OnMessageReceived _onMessageReceived;
        private readonly byte[] _byteBuffer = new byte[NetConstants.PacketSizeLimit];
        private readonly IBuffer _buffer;
        private NetEndPoint _bufferEndPoint;
        private NetEndPoint _localEndPoint;
        private static readonly HostName BroadcastAddress = new HostName("255.255.255.255");
        private static readonly HostName MulticastAddressV6 = new HostName(NetConstants.MulticastGroupIPv6);

        public NetEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        public NetSocket(NetBase.OnMessageReceived onMessageReceived)
        {
            _onMessageReceived = onMessageReceived;
            _buffer = _byteBuffer.AsBuffer();
        }
        
        private void OnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var result = args.GetDataStream().ReadAsync(_buffer, _buffer.Capacity, InputStreamOptions.None).AsTask().Result;
            int length = (int)result.Length;
            if (length <= 0)
                return;

            if (_bufferEndPoint == null ||
                !_bufferEndPoint.HostName.IsEqual(args.RemoteAddress) ||
                !_bufferEndPoint.PortStr.Equals(args.RemotePort))
            {
                _bufferEndPoint = new NetEndPoint(args.RemoteAddress, args.RemotePort);
            }
            _onMessageReceived(_byteBuffer, length, 0, _bufferEndPoint);
        }

        public bool Bind(int port)
        {
            _datagramSocket = new DatagramSocket();
            _datagramSocket.Control.InboundBufferSizeInBytes = NetConstants.SocketBufferSize;
            _datagramSocket.Control.DontFragment = true;
            _datagramSocket.Control.OutboundUnicastHopLimit = NetConstants.SocketTTL;
            _datagramSocket.MessageReceived += OnMessageReceived;

            try
            {
                _datagramSocket.BindServiceNameAsync(port.ToString()).AsTask().Wait();
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

        public bool SendBroadcast(byte[] data, int offset, int size, int port)
        {
            var portString = port.ToString();
            try
            {
                var outputStream =
                    _datagramSocket.GetOutputStreamAsync(BroadcastAddress, portString)
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
            Task<uint> task = null;
            try
            {
                IOutputStream writer;
                if (!_peers.TryGetValue(remoteEndPoint, out writer))
                {
                    writer =
                        _datagramSocket.GetOutputStreamAsync(remoteEndPoint.HostName, remoteEndPoint.PortStr)
                            .AsTask()
                            .Result;
                    _peers.Add(remoteEndPoint, writer);
                }

                task = writer.WriteAsync(data.AsBuffer(offset, length)).AsTask();
                return (int)task.Result;
            }
            catch (Exception ex)
            {
                if (task?.Exception?.InnerExceptions != null)
                {
                    ex = task.Exception.InnerException;
                }
                var errorStatus = SocketError.GetStatus(ex.HResult);
                switch (errorStatus)
                {
                    case SocketErrorStatus.MessageTooLong:
                        errorCode = 10040;
                        break;
                    default:
                        errorCode = (int)errorStatus;
                        NetUtils.DebugWriteError("[S " + errorStatus + "(" + errorCode + ")]" + ex);
                        break;
                }
                
                return -1;
            }
        }

        internal void RemovePeer(NetEndPoint ep)
        {
            _peers.Remove(ep);
        }

        public void Close()
        {
            _datagramSocket.Dispose();
            _datagramSocket = null;
            ClearPeers();
        }

        internal void ClearPeers()
        {
            _peers.Clear();
        }
    }
}
#endif
