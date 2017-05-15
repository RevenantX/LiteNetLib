using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib;

namespace LibSample
{
    class WaitPeer
    {
        public NetEndPoint InternalAddr { get; private set; }
        public NetEndPoint ExternalAddr { get; private set; }
        public DateTime RefreshTime { get; private set; }

        public void Refresh()
        {
            RefreshTime = DateTime.Now;
        }

        public WaitPeer(NetEndPoint internalAddr, NetEndPoint externalAddr)
        {
            Refresh();
            InternalAddr = internalAddr;
            ExternalAddr = externalAddr;
        }
    }

    class HolePunchServerTest : INatPunchListener, IRunnable
    {
        private const int ServerPort = 50010;
        private static readonly TimeSpan KickTime = new TimeSpan(0, 0, 6);

        private readonly Dictionary<string, WaitPeer> _waitingPeers = new Dictionary<string, WaitPeer>();
        private readonly List<string> _peersToRemove = new List<string>();
        private NetManager _puncher;
        private NetManager _c1;
        private NetManager _c2;

        void INatPunchListener.OnNatIntroductionRequest(NetEndPoint localEndPoint, NetEndPoint remoteEndPoint, string token)
        {
            WaitPeer wpeer;
            if (_waitingPeers.TryGetValue(token, out wpeer))
            {
                if (wpeer.InternalAddr.Equals(localEndPoint) &&
                    wpeer.ExternalAddr.Equals(remoteEndPoint))
                {
                    wpeer.Refresh();
                    return;
                }

                Console.WriteLine("Wait peer found, sending introduction...");

                //found in list - introduce client and host to eachother
                Console.WriteLine(
                    "host - i({0}) e({1})\nclient - i({2}) e({3})",
                    wpeer.InternalAddr,
                    wpeer.ExternalAddr,
                    localEndPoint,
                    remoteEndPoint);

                _puncher.NatPunchModule.NatIntroduce(
                    wpeer.InternalAddr, // host internal
                    wpeer.ExternalAddr, // host external
                    localEndPoint, // client internal
                    remoteEndPoint, // client external
                    token // request token
                    );

                //Clear dictionary
                _waitingPeers.Remove(token);
            }
            else
            {
                Console.WriteLine("Wait peer created. i({0}) e({1})", localEndPoint, remoteEndPoint);
                _waitingPeers[token] = new WaitPeer(localEndPoint, remoteEndPoint);
            }
        }

        void INatPunchListener.OnNatIntroductionSuccess(NetEndPoint targetEndPoint, string token)
        {
            //Ignore we are server
        }

        public void Run()
        {
            EventBasedNetListener netListener = new EventBasedNetListener();
            EventBasedNatPunchListener natPunchListener1 = new EventBasedNatPunchListener();
            EventBasedNatPunchListener natPunchListener2 = new EventBasedNatPunchListener();

            netListener.PeerConnectedEvent += peer =>
            {
                Console.WriteLine("PeerConnected: " + peer.EndPoint.ToString());
            };

            netListener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                Console.WriteLine("PeerDisconnected: " + disconnectInfo.Reason);
                if (disconnectInfo.AdditionalData.AvailableBytes > 0)
                {
                    Console.WriteLine("Disconnect data: " + disconnectInfo.AdditionalData.GetInt());
                }
            };

            natPunchListener1.NatIntroductionSuccess += (point, token) =>
            {
                Console.WriteLine("Success C1. Connecting to C2: {0}", point);
                _c1.Connect(point);
            };

            natPunchListener2.NatIntroductionSuccess += (point, token) =>
            {
                Console.WriteLine("Success C2. Connecting to C1: {0}", point);
                _c2.Connect(point);
            };

            _c1 = new NetManager(netListener, "gamekey");
            _c1.NatPunchEnabled = true;
            _c1.NatPunchModule.Init(natPunchListener1);
            _c1.Start();

            _c2 = new NetManager(netListener, "gamekey");
            _c2.NatPunchEnabled = true;
            _c2.NatPunchModule.Init(natPunchListener2);
            _c2.Start();

            _puncher = new NetManager(netListener, "notneed");
            _puncher.Start(ServerPort);
            _puncher.NatPunchEnabled = true;
            _puncher.NatPunchModule.Init(this);

            _c1.NatPunchModule.SendNatIntroduceRequest(new NetEndPoint("::1", ServerPort), "token1");
            _c2.NatPunchModule.SendNatIntroduceRequest(new NetEndPoint("::1", ServerPort), "token1");

            // keep going until ESCAPE is pressed
            Console.WriteLine("Press ESC to quit");

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Escape)
                    {
                        break;
                    }
                    if (key == ConsoleKey.A)
                    {
                        Console.WriteLine("C1 stopped");
                        _c1.DisconnectPeer(_c1.GetFirstPeer(), new byte[] {1,2,3,4});
                        _c1.Stop();
                    }
                }
                
                DateTime nowTime = DateTime.Now;

                _c1.NatPunchModule.PollEvents();
                _c2.NatPunchModule.PollEvents();
                _puncher.NatPunchModule.PollEvents();
                _c1.PollEvents();
                _c2.PollEvents();

                //check old peers
                foreach (var waitPeer in _waitingPeers)
                {
                    if (nowTime - waitPeer.Value.RefreshTime > KickTime)
                    {
                        _peersToRemove.Add(waitPeer.Key);
                    }
                }

                //remove
                for (int i = 0; i < _peersToRemove.Count; i++)
                {
                    Console.WriteLine("Kicking peer: " + _peersToRemove[i]);
                    _waitingPeers.Remove(_peersToRemove[i]);
                }
                _peersToRemove.Clear();

                Thread.Sleep(10);
            }

            _c1.Stop();
            _c2.Stop();
            _puncher.Stop();
        }
    }
}
