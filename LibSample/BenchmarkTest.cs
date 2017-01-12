using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibSample
{
    internal class BenchmarkTest
    {
        internal class TestHost
        {
            private int _clientCount = 200;
            internal List<ClientListener> _clients;
            internal ServerListener _serverListener;

            public void Run()
            {
                //Server
                _serverListener = new ServerListener();

                NetServer server = new NetServer(_serverListener, _clientCount, "myapp1");
                server.UnsyncedEvents = true;
                server.UpdateTime = 1;
                if (!server.Start(9050))
                {
                    Console.WriteLine("Server start failed");
                    Console.ReadKey();
                    return;
                }
                _serverListener.Server = server;

                //Clients
                _clients = new List<ClientListener>(_clientCount);

                for (int i = 0; i < _clientCount; i++)
                {
                    var _clientListener = new ClientListener();
                    var client1 = new NetClient(_clientListener, "myapp1");
                    client1.SimulationMaxLatency = 1500;
                    client1.MergeEnabled = true;
                    client1.UnsyncedEvents = true;
                    client1.UpdateTime = 1;
                    _clientListener.Client = client1;
                    if (!client1.Start())
                    {
                        Console.WriteLine("Client1 start failed");
                        return;
                    }
                    _clients.Add(_clientListener);
                    client1.Connect("127.0.0.1", 9050);
                }

                // Wait
                bool waitForConnection = true;
                while (waitForConnection)
                {
                    Thread.Sleep(15);

                    Console.WriteLine("Waiting:: Errors " + _serverListener.Errors + " Peers " + _serverListener.Server.PeersCount);

                    if (_serverListener.Errors > 0)
                        return;

                    waitForConnection = _serverListener.Server.PeersCount < _clientCount;
                }

                Console.WriteLine("Ready. Press any key to start");
                Console.ReadKey();
                Console.WriteLine("Starting");

                var checker = new HealthChecker();
                checker.Start(this);

                //Send

                _serverListener.Start();

                foreach (var client in _clients)
                {
                    client.Start();
                }

                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(15);

                }

                Console.WriteLine("DONE");

                _serverListener.Stop();

                foreach (var client in _clients)
                {
                    client.Stop();
                }

                server.Stop();

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
        }

        internal class ClientListener : INetEventListener
        {
            internal int MessagesReceivedCount;
            internal DateTime StartTime;
            internal DateTime StopTime;
            internal bool Connected;
            internal int Errors;

            byte[] testData = new byte[13218];

            internal Stopwatch Watch = new Stopwatch();
            internal NetClient Client;

            public void OnPeerConnected(NetPeer peer)
            {
                Connected = true;
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
            {
                Connected = false;
            }

            public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
            {
                Console.WriteLine("[Client] error! " + socketErrorCode);
                Errors++;
            }

            public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
            {
                if (reader.AvailableBytes == testData.Length)
                {
                    MessagesReceivedCount++;

                    Watch.Stop();

                    Send();
                }
            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
            {
                Console.WriteLine("[Client] ReceiveUnconnected: {0}", reader.GetString(100));
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }

            void Send()
            {
                var peer = Client.Peer;

                Watch.Start();

                peer.Send(testData, SendOptions.ReliableOrdered);
            }

            public void Start()
            {
                StartTime = DateTime.UtcNow;
                Send();
            }

            public void Stop()
            {
                Watch.Stop();
                StopTime = DateTime.UtcNow;
                Client.Stop();
            }
        }

        internal class ServerListener : INetEventListener
        {
            internal int MessagesReceivedCount;
            internal DateTime StartTime;
            internal DateTime StopTime;
            internal int Errors;
            internal NetServer Server;

            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Server] Peer connected: " + peer.EndPoint);
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
            {
                Console.WriteLine("[Server] Peer disconnected: " + peer.EndPoint + ", reason: " + disconnectReason);
            }

            public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
            {
                Console.WriteLine("[Server] error: " + socketErrorCode);
                Errors++;
            }

            public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
            {
                MessagesReceivedCount++;

                //echo
                peer.Send(reader.Data, SendOptions.ReliableUnordered);
            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
            {
                Console.WriteLine("[Server] ReceiveUnconnected: {0}", reader.GetString(100));
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }

            public void Start()
            {
                StartTime = DateTime.UtcNow;
            }

            public void Stop()
            {
                StopTime = DateTime.UtcNow;
                Server.Stop();
            }
        }

        internal class HealthChecker
        {
            private int _threadId;
            private BenchmarkTest.TestHost _context;

            public void Start(BenchmarkTest.TestHost context)
            {
                _context = context;
                _threadId++;
                PollInternal();
            }

            public void Stop()
            {
                _threadId++;

                PrintServer();
                PrintClient();
            }

            async void PollInternal()
            {
                var id = _threadId;

                while (id == _threadId)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15));
                    PrintServer();
                    PrintClient();
                }
            }

            public void PrintServer()
            {
                var fmt = "[{5}] CON {0} MSG {1} TIME {2} OPS {3} LAG {4} ERR {5}";

                var span = DateTime.UtcNow.Subtract(_context._serverListener.StartTime);

                Console.WriteLine(
                    fmt,
                    _context._serverListener.Server.PeersCount,
                    _context._serverListener.MessagesReceivedCount,
                    span.TotalSeconds,
                    _context._serverListener.MessagesReceivedCount / span.TotalSeconds,
                    "NA",
                    _context._serverListener.Errors,
                    "SERVER");
            }

            public void PrintClient()
            {
                var fmt = "[{5}] CON {0} MSG {1} TIME {2} OPS {3} LAG {4} ERR {5}";

                var span = DateTime.UtcNow.Subtract(_context._serverListener.StartTime);

                var cons = _context._clients.Count;
                var msg = _context._clients.Sum(o => o.MessagesReceivedCount);
                var ops = msg / span.TotalSeconds;
                var lag = _context._clients.Sum(o => o.Watch.ElapsedMilliseconds) / msg;
                var err = _context._clients.Sum(o => o.Errors);

                Console.WriteLine(
                    fmt,
                    cons,
                    msg,
                    span.TotalSeconds,
                    ops,
                    lag,
                    err,
                    "CLIENT");
            }
        }
    }
}