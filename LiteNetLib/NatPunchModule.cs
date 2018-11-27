using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib.Utils;

//Some code parts taken from lidgren-network-gen3
namespace LiteNetLib
{
    public interface INatPunchListener
    {
        void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token);
        void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, string token);
    }

    public class EventBasedNatPunchListener : INatPunchListener
    {
        public delegate void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token);
        public delegate void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, string token);

        public event OnNatIntroductionRequest NatIntroductionRequest;
        public event OnNatIntroductionSuccess NatIntroductionSuccess;

        void INatPunchListener.OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            if(NatIntroductionRequest != null)
                NatIntroductionRequest(localEndPoint, remoteEndPoint, token);
        }

        void INatPunchListener.OnNatIntroductionSuccess(IPEndPoint targetEndPoint, string token)
        {
            if (NatIntroductionSuccess != null)
                NatIntroductionSuccess(targetEndPoint, token);
        }
    }

    /// <summary>
    /// Module for UDP NAT Hole punching operations. Can be accessed from NetManager
    /// </summary>
    public sealed class NatPunchModule
    {
        struct RequestEventData
        {
            public IPEndPoint LocalEndPoint;
            public IPEndPoint RemoteEndPoint;
            public string Token;
        }

        struct SuccessEventData
        {
            public IPEndPoint TargetEndPoint;
            public string Token;
        }

        private readonly NetSocket _socket;
        private readonly Queue<RequestEventData> _requestEvents;
        private readonly Queue<SuccessEventData> _successEvents; 
        private const byte HostByte = 1;
        private const byte ClientByte = 0;
        public const int MaxTokenLength = 256;

        private INatPunchListener _natPunchListener;

        internal NatPunchModule(NetSocket socket)
        {
            _socket = socket;
            _requestEvents = new Queue<RequestEventData>();
            _successEvents = new Queue<SuccessEventData>();
        }

        public void Init(INatPunchListener listener)
        {
            _natPunchListener = listener;
        }

        public void NatIntroduce(
            IPEndPoint hostInternal,
            IPEndPoint hostExternal,
            IPEndPoint clientInternal,
            IPEndPoint clientExternal,
            string additionalInfo)
        {
            NetDataWriter dw = new NetDataWriter();

            //First packet (server)
            //send to client
            dw.Put((byte)PacketProperty.NatIntroduction);
            dw.Put(ClientByte);
            dw.Put(hostInternal);
            dw.Put(hostExternal);
            dw.Put(additionalInfo, MaxTokenLength);
            SocketError errorCode = 0;
            _socket.SendTo(dw.Data, 0, dw.Length, clientExternal, ref errorCode);

            //Second packet (client)
            //send to server
            dw.Reset();
            dw.Put((byte)PacketProperty.NatIntroduction);
            dw.Put(HostByte);
            dw.Put(clientInternal);
            dw.Put(clientExternal);
            dw.Put(additionalInfo, MaxTokenLength);
            _socket.SendTo(dw.Data, 0, dw.Length, hostExternal, ref errorCode);
        }

        public void PollEvents()
        {
            if (_natPunchListener == null)
                return;
            lock (_successEvents)
            {
                while (_successEvents.Count > 0)
                {
                    var evt = _successEvents.Dequeue();
                    _natPunchListener.OnNatIntroductionSuccess(evt.TargetEndPoint, evt.Token);
                }
            }
            lock (_requestEvents)
            {
                while (_requestEvents.Count > 0)
                {
                    var evt = _requestEvents.Dequeue();
                    _natPunchListener.OnNatIntroductionRequest(evt.LocalEndPoint, evt.RemoteEndPoint, evt.Token);
                }
            }
        }

        public void SendNatIntroduceRequest(IPEndPoint masterServerEndPoint, string additionalInfo)
        {
            //prepare outgoing data
            NetDataWriter dw = new NetDataWriter();
            string networkIp = NetUtils.GetLocalIp(LocalAddrType.IPv4);
            if (string.IsNullOrEmpty(networkIp))
            {
                networkIp = NetUtils.GetLocalIp(LocalAddrType.IPv6);
            }
            IPEndPoint localEndPoint = NetUtils.MakeEndPoint(networkIp, _socket.LocalPort);
            dw.Put((byte)PacketProperty.NatIntroductionRequest);
            dw.Put(localEndPoint);
            dw.Put(additionalInfo, MaxTokenLength);

            //prepare packet
            SocketError errorCode = 0;
            _socket.SendTo(dw.Data, 0, dw.Length, masterServerEndPoint, ref errorCode);
        }

        private void HandleNatPunch(IPEndPoint senderEndPoint, NetDataReader dr)
        {
            byte fromHostByte = dr.GetByte();
            if (fromHostByte != HostByte && fromHostByte != ClientByte)
            {
                //garbage
                return;
            }

            //Read info
            string additionalInfo = dr.GetString(MaxTokenLength);
            NetDebug.Write(NetLogLevel.Trace, "[NAT] punch received from {0} - additional info: {1}", senderEndPoint, additionalInfo);

            //Release punch success to client; enabling him to Connect() to msg.Sender if token is ok
            lock (_successEvents)
            {
                _successEvents.Enqueue(new SuccessEventData { TargetEndPoint = senderEndPoint, Token = additionalInfo });
            }
        }

        private void HandleNatIntroduction(NetDataReader dr)
        {
            // read intro
            byte hostByte = dr.GetByte();
            IPEndPoint remoteInternal = dr.GetNetEndPoint();
            IPEndPoint remoteExternal = dr.GetNetEndPoint();
            string token = dr.GetString(MaxTokenLength);

            NetDebug.Write(NetLogLevel.Trace, "[NAT] introduction received; we are designated " + (hostByte == HostByte ? "host" : "client"));
            NetDataWriter writer = new NetDataWriter();

            // send internal punch
            writer.Put((byte)PacketProperty.NatPunchMessage);
            writer.Put(hostByte);
            writer.Put(token);
            SocketError errorCode = 0;
            _socket.SendTo(writer.Data, 0, writer.Length, remoteInternal, ref errorCode);
            NetDebug.Write(NetLogLevel.Trace, "[NAT] internal punch sent to " + remoteInternal);

            // send external punch
            writer.Reset();
            writer.Put((byte)PacketProperty.NatPunchMessage);
            writer.Put(hostByte);
            writer.Put(token);
            if (hostByte == HostByte)
            {
                _socket.Ttl = 2;
                _socket.SendTo(writer.Data, 0, writer.Length, remoteExternal, ref errorCode);
                _socket.Ttl = NetConstants.SocketTTL;
            }
            else
            {
                _socket.SendTo(writer.Data, 0, writer.Length, remoteExternal, ref errorCode);
            }
    
            NetDebug.Write(NetLogLevel.Trace, "[NAT] external punch sent to " + remoteExternal);
        }

        private void HandleNatIntroductionRequest(IPEndPoint senderEndPoint, NetDataReader dr)
        {
            IPEndPoint localEp = dr.GetNetEndPoint();
            string token = dr.GetString(MaxTokenLength);
            lock (_requestEvents)
            {
                _requestEvents.Enqueue(new RequestEventData
                {
                    LocalEndPoint = localEp,
                    RemoteEndPoint = senderEndPoint,
                    Token = token
                });
            }
        }

        internal void ProcessMessage(IPEndPoint senderEndPoint, NetPacket packet)
        {
            var dr = new NetDataReader(packet.RawData, NetConstants.HeaderSize, packet.Size);
            switch (packet.Property)
            {
                case PacketProperty.NatIntroductionRequest:
                    //We got request and must introduce
                    HandleNatIntroductionRequest(senderEndPoint, dr);
                    break;
                case PacketProperty.NatIntroduction:
                    //We got introduce and must punch
                    HandleNatIntroduction(dr);
                    break;
                case PacketProperty.NatPunchMessage:
                    //We got punch and can connect
                    HandleNatPunch(senderEndPoint, dr);
                    break;
            }
        }
    }
}
