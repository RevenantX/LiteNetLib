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

    class HolePunchServerTest
    {
        private const int ServerPort = 48291;
        private static readonly TimeSpan KickTime = new TimeSpan(0, 0, 6);

        private readonly Dictionary<string, WaitPeer> _waitingPeers = new Dictionary<string, WaitPeer>();
        private readonly List<string> _peersToRemove = new List<string>();
        private NetBase _puncher;

        void RequestIntroduction(NetEndPoint localEndPoint, NetEndPoint remoteEndPoint, string token)
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

        public void Run()
        {
            _puncher = new NetBase();
            _puncher.Start(ServerPort);
            _puncher.NatPunchEnabled = true;
            _puncher.NatPunchModule.OnNatIntroductionRequest += RequestIntroduction;

            // keep going until ESCAPE is pressed
            Console.WriteLine("Press ESC to quit");
            while (!Console.KeyAvailable || Console.ReadKey().Key != ConsoleKey.Escape)
            {
                DateTime nowTime = DateTime.Now;
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

            _puncher.Stop();
        }
    }
}
