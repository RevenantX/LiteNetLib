using System;
using System.Collections.Generic;
using LiteNetLib.Utils;

//Some code parts taked from lidgren-network-gen3

namespace LiteNetLib
{
    public sealed class NatPunchModule
    {
        struct RequestEventData
        {
            public NetEndPoint LocalEndPoint;
            public NetEndPoint RemoteEndPoint;
            public string Token;
        }

        struct SuccessEventData
        {
            public NetEndPoint TargetEndPoint;
            public string Token;
        }

        private readonly NetBase _netBase;
        private readonly NetSocket _socket;
        private readonly Queue<RequestEventData> _requestEvents;
        private readonly Queue<SuccessEventData> _successEvents; 
        private const byte HostByte = 1;
        private const byte ClientByte = 0;
        public const int MaxTokenLength = 256;

        public delegate void NatIntroductionRequestDelegate(
            NetEndPoint localEndPoint, NetEndPoint remoteEndPoint, string token);

        public delegate void NatIntroductionSuccessDelegate(NetEndPoint targetEndPoint, string token);

        public event NatIntroductionRequestDelegate OnNatIntroductionRequest;
        public event NatIntroductionSuccessDelegate OnNatIntroductionSuccess;

        internal NatPunchModule(NetBase netBase, NetSocket socket)
        {
            _netBase = netBase;
            _socket = socket;
            _requestEvents = new Queue<RequestEventData>();
            _successEvents = new Queue<SuccessEventData>();
        }

        public void NatIntroduce(
            NetEndPoint hostInternal,
            NetEndPoint hostExternal,
            NetEndPoint clientInternal,
            NetEndPoint clientExternal,
            string additionalInfo)
        {
            NetDataWriter dw = new NetDataWriter();

            //First packet (server)
            //send to client
            dw.Put(ClientByte);
            dw.Put(hostInternal);
            dw.Put(hostExternal);
            dw.Put(additionalInfo, MaxTokenLength);

            _socket.SendTo(NetPacket.CreateRawPacket(PacketProperty.NatIntroduction, dw), clientExternal);

            //Second packet (client)
            //send to server
            dw.Reset();
            dw.Put(HostByte);
            dw.Put(clientInternal);
            dw.Put(clientExternal);
            dw.Put(additionalInfo, MaxTokenLength);

            _socket.SendTo(NetPacket.CreateRawPacket(PacketProperty.NatIntroduction, dw), hostExternal);
        }

        public void Update()
        {
            lock (_successEvents)
            {
                while (_successEvents.Count > 0)
                {
                    var evt = _successEvents.Dequeue();
                    if (OnNatIntroductionSuccess != null)
                    {
                        OnNatIntroductionSuccess(evt.TargetEndPoint, evt.Token);
                    }
                }
            }
            lock (_requestEvents)
            {
                while (_requestEvents.Count > 0)
                {
                    var evt = _requestEvents.Dequeue();
                    if (OnNatIntroductionRequest != null)
                    {
                        OnNatIntroductionRequest(evt.LocalEndPoint, evt.RemoteEndPoint, evt.Token);
                    }
                }
            }
        }

        public void SendNatIntroduceRequest(NetEndPoint masterServerEndPoint, string additionalInfo)
        {
            if (!_netBase.IsRunning)
                return;

            //prepare outgoing data
            NetDataWriter dw = new NetDataWriter();
            string networkIp = NetUtils.GetLocalIP();
            int networkPort = _netBase.LocalEndPoint.Port;
            NetEndPoint localEndPoint = new NetEndPoint(networkIp, networkPort);
            dw.Put(localEndPoint);
            dw.Put(additionalInfo, MaxTokenLength);

            //prepare packet
            _socket.SendTo(NetPacket.CreateRawPacket(PacketProperty.NatIntroductionRequest, dw), masterServerEndPoint);
        }

        private void HandleNatPunch(NetEndPoint senderEndPoint, NetDataReader dr)
        {
            byte fromHostByte = dr.GetByte();
            if (fromHostByte != HostByte)
            {
                //it's from client or garbage
                return;
            }

            NetDataWriter dw = new NetDataWriter();
            string additionalInfo = dr.GetString(MaxTokenLength);

            NetUtils.DebugWrite(ConsoleColor.Green, "[NAT] punch received from {0} we're client, so we've succeeded - additional info: {1}", senderEndPoint, additionalInfo);

            //Release punch success to client; enabling him to Connect() to msg.Sender if token is ok
            lock (_successEvents)
            {
                _successEvents.Enqueue(new SuccessEventData { TargetEndPoint = senderEndPoint, Token = additionalInfo });
            }

            //send a return punch just for good measure
            dw.Put(ClientByte);
            dw.Put(additionalInfo);
            _socket.SendTo(NetPacket.CreateRawPacket(PacketProperty.NatPunchMessage, dw), senderEndPoint);
        }

        private void HandleNatIntroduction(NetDataReader dr)
        {
            // read intro
            byte hostByte = dr.GetByte();
            NetEndPoint remoteInternal = dr.GetNetEndPoint();
            NetEndPoint remoteExternal = dr.GetNetEndPoint();
            string token = dr.GetString(MaxTokenLength);
            bool isHost = (hostByte == HostByte);

            NetUtils.DebugWrite(ConsoleColor.Cyan, "[NAT] introduction received; we are designated " + (isHost ? "host" : "client"));

            NetPacket punch = new NetPacket();
            NetDataWriter writer = new NetDataWriter();
            punch.Init(PacketProperty.NatPunchMessage, writer);

            // send internal punch
            writer.Put(hostByte);
            writer.Put(token);
            _socket.SendTo(NetPacket.CreateRawPacket(PacketProperty.NatPunchMessage, writer), remoteInternal);
            NetUtils.DebugWrite(ConsoleColor.Cyan, "[NAT] punch sent to " + remoteInternal);

            // send external punch
            writer.Reset();
            writer.Put(hostByte);
            writer.Put(token);
            _socket.SendTo(NetPacket.CreateRawPacket(PacketProperty.NatPunchMessage, writer), remoteExternal);
            NetUtils.DebugWrite(ConsoleColor.Cyan, "[NAT] punch sent to " + remoteExternal);
        }

        private void HandleNatIntroductionRequest(NetEndPoint senderEndPoint, NetDataReader dr)
        {
            NetEndPoint localEp = dr.GetNetEndPoint();
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

        internal void ProcessMessage(NetEndPoint senderEndPoint, PacketProperty property, byte[] data)
        {
            NetDataReader dr = new NetDataReader(data);

            switch (property)
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
