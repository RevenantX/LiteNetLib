using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibSample
{
    struct CustomStruct : INetSerializable
    {
        public int X;
        public int Y;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(X);
            writer.Put(Y);
        }

        public void Deserialize(NetDataReader reader)
        {
            X = reader.GetInt();
            Y = reader.GetInt();
        }
    }

    class ArgumentsForLogin
    {
        public string UserId { get; set; }
        public string Password { get; set; }
        public int SomeInt { get; set; }
        public List<CustomStruct> SomeList { get; set; }
    }

    class PacketProcessorExample : IExample
    {
        private readonly NetPacketProcessor _netPacketProcessor = new NetPacketProcessor();
        private NetManager _client;
        private NetManager _server;

        public void Run()
        {
            //setup netpacketprocessor
            _netPacketProcessor.RegisterNestedType<CustomStruct>();
            _netPacketProcessor.SubscribeReusable<ArgumentsForLogin, NetPeer>(Method1);

            //setup events
            EventBasedNetListener clientListener = new EventBasedNetListener();
            EventBasedNetListener serverListener = new EventBasedNetListener();
            serverListener.ConnectionRequestEvent += request =>
            {
                request.AcceptIfKey("key");
            };
            serverListener.NetworkReceiveEvent +=
                (peer, reader, method, channel) =>
                {
                    _netPacketProcessor.ReadAllPackets(reader, peer);
                };
            clientListener.PeerConnectedEvent += peer =>
            {
                //send after connect
                var testList = new List<CustomStruct>
                {
                    new CustomStruct {X = 1, Y = -1},
                    new CustomStruct {X = 5, Y = -28},
                    new CustomStruct {X = -114, Y = 65535}
                };
                _netPacketProcessor.Send(
                    peer,
                    new ArgumentsForLogin {Password = "pass", SomeInt = 5, UserId = "someUser", SomeList = testList},
                    DeliveryMethod.ReliableOrdered);
            };

            //start client/server
            _client = new NetManager(clientListener);
            _server = new NetManager(serverListener);
            _client.Start();
            _server.Start(9050);
            _client.Connect("localhost", 9050, "key");

            while (!Console.KeyAvailable)
            {
                _server.PollEvents();
                _client.PollEvents();
                Thread.Sleep(10);
            }
            _client.Stop();
            _server.Stop();
        }

        void Method1(ArgumentsForLogin args, NetPeer peer)
        {
            Console.WriteLine("Received: \n {0}\n {1}\n {2}", args.UserId, args.Password, args.SomeInt);
            Console.WriteLine("List count: " + args.SomeList.Count);
            for (int i = 0; i < args.SomeList.Count; i++)
            {
                Console.WriteLine($" X: {args.SomeList[i].X}, Y: {args.SomeList[i].Y}");
            }
        }
    }
}
