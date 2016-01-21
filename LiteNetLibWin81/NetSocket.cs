using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace LiteNetLib
{
    public class NetSocket
    {
        private readonly DatagramSocket _datagramSocket;
        private readonly Dictionary<NetEndPoint, IOutputStream> _peers = new Dictionary<NetEndPoint, IOutputStream>(); 

        //Socket constructor
        public NetSocket(TypedEventHandler<DatagramSocket, DatagramSocketMessageReceivedEventArgs> onReceive)
        {
            _datagramSocket = new DatagramSocket();
            _datagramSocket.MessageReceived += onReceive;
        }

        //Bind socket to port
        public bool Bind(NetEndPoint ep)
        {
            try
            {
                var task = Task.Run(async () =>
                {
                    if (ep.HostName == null)
                        await _datagramSocket.BindServiceNameAsync(ep.PortStr);
                    else
                        await _datagramSocket.BindEndpointAsync(ep.HostName, ep.PortStr);
                });
                task.Wait();
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
            try
            {
                SendToAsync(data, remoteEndPoint);
            }
            catch (Exception)
            {
                return -1;
            }
            return data.Length;
        }

        //async
        private async void SendToAsync(byte[] data, NetEndPoint remoteEndPoint)
        {
            IOutputStream stream;
            if (!_peers.TryGetValue(remoteEndPoint, out stream))
            {
                stream = await _datagramSocket.GetOutputStreamAsync(remoteEndPoint.HostName, remoteEndPoint.PortStr);
                _peers.Add(remoteEndPoint, stream);
            }

            await stream.WriteAsync(data.AsBuffer());
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
