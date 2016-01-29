using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace LiteNetLib
{
    sealed class NetSocket
    {
        private readonly DatagramSocket _datagramSocket;
        private readonly Dictionary<NetEndPoint, DataWriter> _peers = new Dictionary<NetEndPoint, DataWriter>();

        //Socket constructor
        public NetSocket(TypedEventHandler<DatagramSocket, DatagramSocketMessageReceivedEventArgs> onReceive)
        {
            _datagramSocket = new DatagramSocket();
            _datagramSocket.Control.DontFragment = true;
            _datagramSocket.MessageReceived += onReceive;
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
