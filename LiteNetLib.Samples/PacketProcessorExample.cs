using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LiteNetLib.Samples
{
    class ArgumentsForLogin
    {
        public string UserId { get; set; }
        public string Password { get; set; }
        public int SomeInt { get; set; }
    }

    class PacketProcessorExample
    {
        private readonly NetPacketProcessor _netPacketProcessor = new NetPacketProcessor();
        private NetManager _client;
        private NetManager _server;

        public void Run()
        {
            //setup netpacketprocessor
            _netPacketProcessor.SubscribeReusable<ArgumentsForLogin, NetPeer>(Method1);

            //setup events
            EventBasedNetListener clientListener = new EventBasedNetListener();
            EventBasedNetListener serverListener = new EventBasedNetListener();
            serverListener.ConnectionRequestEvent += request => request.AcceptIfKey("key");
            serverListener.NetworkReceiveEvent +=
                (peer, reader, method) => _netPacketProcessor.ReadAllPackets(reader, peer);

            //start client/server
            _client = new NetManager(clientListener);
            _server = new NetManager(serverListener);
            _client.Start();
            _server.Start(9050);
            var clientPeer = _client.Connect("localhost", 9050, "key");

            //send
            _netPacketProcessor.Send(
                clientPeer, 
                new ArgumentsForLogin { Password = "pass", SomeInt = 5, UserId = "someUser"}, 
                DeliveryMethod.ReliableOrdered);

            while (!Console.KeyAvailable)
            {
                _server.PollEvents();
                Thread.Sleep(10);
            }
            _client.Stop();
            _server.Stop();
        }

        void Method1(ArgumentsForLogin args, NetPeer peer)
        {
            Console.WriteLine("Received: \n {0}\n {1}\n {2}", args.UserId, args.Password, args.SomeInt);
        }
    }
}
