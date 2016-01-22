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
            _datagramSocket.Control.DontFragment = true;
            _datagramSocket.MessageReceived += onReceive;
        }

        //Bind socket to port
        public bool Bind(NetEndPoint ep)
        {
            var task = Task.Run(async () => await BindAsync(ep));
            task.Wait();
            return task.Result;
        }

        private async Task<bool> BindAsync(NetEndPoint ep)
        {
            try
            {
                if (ep.HostName == null)
                    await _datagramSocket.BindServiceNameAsync(ep.PortStr);
                else
                    await _datagramSocket.BindEndpointAsync(ep.HostName, ep.PortStr);
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
            var task = Task.Run(async () => await SendToAsync(data, remoteEndPoint));
            task.Wait();
            return task.Result;
        }

        private async Task<int> SendToAsync(byte[] data, NetEndPoint remoteEndPoint)
        {
            try
            {
                IOutputStream stream;
                if (!_peers.TryGetValue(remoteEndPoint, out stream))
                {
                    stream = await _datagramSocket.GetOutputStreamAsync(remoteEndPoint.HostName, remoteEndPoint.PortStr);
                    _peers.Add(remoteEndPoint, stream);
                }

                uint result = await stream.WriteAsync(data.AsBuffer());
                return (int)result;
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
