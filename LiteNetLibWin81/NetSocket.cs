using System;
using System.Collections.Generic;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace LiteNetLib
{
    sealed class NetSocket
    {
        private readonly DatagramSocket _datagramSocket;
        private readonly Dictionary<NetEndPoint, DataWriter> _peers = new Dictionary<NetEndPoint, DataWriter>();
        private readonly Queue<IncomingData> _incomingData = new Queue<IncomingData>();

        private struct IncomingData
        {
            public NetEndPoint EndPoint;
            public byte[] Data;
        }

        //Socket constructor
        public NetSocket()
        {
            _datagramSocket = new DatagramSocket();
            _datagramSocket.Control.DontFragment = true;
            _datagramSocket.MessageReceived += OnMessageReceived;
        }

        
        private void OnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var dataReader = args.GetDataReader();
            uint count = dataReader.UnconsumedBufferLength;
            if (count > 0)
            {
                byte[] data = new byte[count];
                dataReader.ReadBytes(data);
                _incomingData.Enqueue(
                    new IncomingData
                    {
                        EndPoint = new NetEndPoint(args.RemoteAddress, args.RemotePort),
                        Data = data
                    });
            }
        }

        //Bind socket to port
        public bool Bind(NetEndPoint ep)
        {
            try
            {
                if (ep.HostName == null)
                    _datagramSocket.BindServiceNameAsync(ep.PortStr).GetResults();
                else
                    _datagramSocket.BindEndpointAsync(ep.HostName, ep.PortStr).GetResults();
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        //Send to
        public int SendTo(byte[] data, NetEndPoint remoteEndPoint)
        {
            int errorCode = 0;
            return SendTo(data, remoteEndPoint, ref errorCode);
        }

        public int SendTo(byte[] data, NetEndPoint remoteEndPoint, ref int errorCode)
        {
            try
            {
                DataWriter writer;
                if (!_peers.TryGetValue(remoteEndPoint, out writer))
                {
                    var outputStream =
                        _datagramSocket.GetOutputStreamAsync(remoteEndPoint.HostName, remoteEndPoint.PortStr)
                            .AsTask()
                            .Result;
                    writer = new DataWriter(outputStream);
                    _peers.Add(remoteEndPoint, writer);
                }

                writer.WriteBytes(data);
                var res = writer.StoreAsync().AsTask().Result;
                return (int)res;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public int ReceiveFrom(ref byte[] data, ref NetEndPoint remoteEndPoint, ref int errorCode)
        {
            if (_incomingData.Count == 0)
                return 0;
            var incomingData = _incomingData.Dequeue();
            data = incomingData.Data;
            remoteEndPoint = incomingData.EndPoint;
            return data.Length;
        }

        //Close socket
        public void Close()
        {
            foreach (var dataWriter in _peers)
            {
                dataWriter.Value.Dispose();
            }
            _peers.Clear();
            _datagramSocket.Dispose();
        }
    }
}
