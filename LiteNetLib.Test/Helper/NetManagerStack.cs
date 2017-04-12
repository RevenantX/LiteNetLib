using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LiteNetLib.Test.Helper
{
    public class NetManagerStack : IDisposable
    {
        private readonly string _appKey;
        private readonly int _serverPort;

        private readonly HashSet<ushort> _clientIds = new HashSet<ushort>();
        private readonly HashSet<ushort> _serverIds = new HashSet<ushort>();

        private readonly Dictionary<uint, Tuple<NetManager, EventBasedNetListener>> _managers =
            new Dictionary<uint, Tuple<NetManager, EventBasedNetListener>>();

        public NetManagerStack(string appKey, int serverPort)
        {
            _appKey = appKey;
            _serverPort = serverPort;
        }

        public void ClientForeach(Action<ushort, NetManager, EventBasedNetListener> action)
        {
            foreach (var id in _clientIds)
            {
                var tuple = GetNetworkManager(id, true);
                action(id, tuple.Item1, tuple.Item2);
            }
        }

        public void ServerForeach(Action<ushort, NetManager, EventBasedNetListener> action)
        {
            foreach (var id in _clientIds)
            {
                var tuple = GetNetworkManager(id, false);
                action(id, tuple.Item1, tuple.Item2);
            }
        }

        public NetManager Client(ushort id)
        {
            _clientIds.Add(id);
            return GetNetworkManager(id, true).Item1;
        }

        public EventBasedNetListener ClientListener(ushort id)
        {
            _clientIds.Add(id);
            return GetNetworkManager(id, true).Item2;
        }

        public NetManager Server(ushort id)
        {
            _serverIds.Add(id);
            return GetNetworkManager(id, false).Item1;
        }

        public EventBasedNetListener ServerListener(ushort id)
        {
            _serverIds.Add(id);
            return GetNetworkManager(id, false).Item2;
        }

        public void Dispose()
        {
            foreach (var manager in _managers.Values.Select(v => v.Item1))
            {
                manager.Stop();
            }
        }

        private Tuple<NetManager, EventBasedNetListener> GetNetworkManager(ushort id, bool isClient)
        {
            Tuple<NetManager, EventBasedNetListener> tuple;
            if (id == 0)
            {
                Assert.Fail("Id cannot be 0");
            }

            var key = isClient ? id : (uint) id << 16;
            if (!_managers.TryGetValue(key, out tuple))
            {
                var listener = new EventBasedNetListener();
                NetManager netManager;
                if (isClient)
                {
                    netManager = new NetManager(listener, _appKey);
                    if (!netManager.Start())
                    {
                        Assert.Fail($"Client {id} start failed");
                    }
                }
                else
                {
                    netManager = new NetManager(listener, 20, _appKey);
                    if (!netManager.Start(_serverPort))
                    {
                        Assert.Fail($"Server {id} on port{_serverPort} start failed");
                    }
                }

                tuple = new Tuple<NetManager, EventBasedNetListener>(netManager, listener);
                _managers[key] = tuple;
            }

            return tuple;
        }
    }
}