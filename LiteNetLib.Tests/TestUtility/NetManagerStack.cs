using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

namespace LiteNetLib.Tests.TestUtility
{
    public class NetManagerStack : IDisposable
    {
        private struct NetContainer
        {
            public readonly NetManager Manager;
            public readonly EventBasedNetListener Listener;

            public NetContainer(NetManager netManager, EventBasedNetListener listener)
            {
                Manager = netManager;
                Listener = listener;
            }
        }

        private readonly string _appKey;
        private readonly int _serverPort;

        private readonly HashSet<ushort> _clientIds = new HashSet<ushort>();
        private readonly HashSet<ushort> _serverIds = new HashSet<ushort>();

        private readonly Dictionary<uint, NetContainer> _managers =
            new Dictionary<uint, NetContainer>();

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
                action(id, tuple.Manager, tuple.Listener);
            }
        }

        public void ServerForeach(Action<ushort, NetManager, EventBasedNetListener> action)
        {
            foreach (var id in _clientIds)
            {
                var tuple = GetNetworkManager(id, false);
                action(id, tuple.Manager, tuple.Listener);
            }
        }

        public NetManager Client(ushort id)
        {
            _clientIds.Add(id);
            return GetNetworkManager(id, true).Manager;
        }

        public EventBasedNetListener ClientListener(ushort id)
        {
            _clientIds.Add(id);
            return GetNetworkManager(id, true).Listener;
        }

        public NetManager Server(ushort id)
        {
            _serverIds.Add(id);
            return GetNetworkManager(id, false).Manager;
        }

        public EventBasedNetListener ServerListener(ushort id)
        {
            _serverIds.Add(id);
            return GetNetworkManager(id, false).Listener;
        }

        public void Dispose()
        {
            foreach (var manager in _managers.Values.Select(v => v.Manager))
            {
                manager.Stop();
            }
        }

        private NetContainer GetNetworkManager(ushort id, bool isClient)
        {
            NetContainer container;
            if (id == 0)
            {
                Assert.Fail("Id cannot be 0");
            }

            var key = isClient ? id : (uint) id << 16;
            if (!_managers.TryGetValue(key, out container))
            {
                var listener = new EventBasedNetListener();
                listener.ConnectionRequestEvent += request =>
                {
                    request.AcceptIfKey(_appKey);
                };
                NetManager netManager;
                if (isClient)
                {
                    netManager = new NetManager(listener);
                    if (!netManager.Start())
                    {
                        Assert.Fail($"Client {id} start failed");
                    }
                }
                else
                {
                    netManager = new NetManager(listener);
                    if (!netManager.Start(_serverPort))
                    {
                        Assert.Fail($"Server {id} on port{_serverPort} start failed");
                    }
                }

                container = new NetContainer(netManager, listener);
                _managers[key] = container;
            }

            return container;
        }
    }
}