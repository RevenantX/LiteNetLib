using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib.Layers;
using NUnit.Framework;

namespace LiteNetLib.Tests.TestUtility
{
    public abstract class NetManagerStack<TManager, TListener> : IDisposable where TManager : LiteNetManager
    {
        private struct NetContainer
        {
            public readonly TManager Manager;
            public readonly TListener Listener;

            public NetContainer(TManager netManager, TListener listener)
            {
                Manager = netManager;
                Listener = listener;
            }
        }

        protected readonly string AppKey;
        private readonly int _serverPort;

        private readonly HashSet<ushort> _clientIds = new();
        private readonly HashSet<ushort> _serverIds = new();
        private readonly Dictionary<uint, NetContainer> _managers = new();

        public NetManagerStack(string appKey, int serverPort)
        {
            AppKey = appKey;
            _serverPort = serverPort;
        }

        public void ClientForeach(Action<ushort, TManager, TListener> action)
        {
            foreach (var id in _clientIds)
            {
                var tuple = GetNetworkManager(id, true);
                action(id, tuple.Manager, tuple.Listener);
            }
        }

        public void ServerForeach(Action<ushort, TManager, TListener> action)
        {
            foreach (var id in _serverIds)
            {
                var tuple = GetNetworkManager(id, false);
                action(id, tuple.Manager, tuple.Listener);
            }
        }

        public TManager Client(ushort id)
        {
            _clientIds.Add(id);
            return GetNetworkManager(id, true).Manager;
        }

        public TListener ClientListener(ushort id)
        {
            _clientIds.Add(id);
            return GetNetworkManager(id, true).Listener;
        }

        public TManager Server(ushort id)
        {
            _serverIds.Add(id);
            return GetNetworkManager(id, false).Manager;
        }

        public TListener ServerListener(ushort id)
        {
            _serverIds.Add(id);
            return GetNetworkManager(id, false).Listener;
        }

        public void Dispose()
        {
            foreach (var manager in _managers.Values.Select(v => v.Manager))
                manager.Stop();
        }

        protected abstract (TManager,TListener) CreateNetworkManager();

        private NetContainer GetNetworkManager(ushort id, bool isClient)
        {
            if (id == 0)
            {
                Assert.Fail("Id cannot be 0");
            }

            var key = isClient ? id : (uint) id << 16;
            if (!_managers.TryGetValue(key, out var container))
            {
                var (netManager, listener) = CreateNetworkManager();
                if (isClient)
                {
                    if (!netManager.Start())
                        Assert.Fail($"Client {id} start failed");
                }
                else
                {
                    if (!netManager.Start(_serverPort))
                        Assert.Fail($"Server {id} on port{_serverPort} start failed");
                }

                container = new NetContainer(netManager, listener);
                _managers[key] = container;
            }

            return container;
        }
    }

    public class LiteNetManagerStack : NetManagerStack<LiteNetManager, EventBasedLiteNetListener>
    {
        public LiteNetManagerStack(string appKey, int serverPort) : base(appKey, serverPort)
        {
        }

        protected override (LiteNetManager, EventBasedLiteNetListener) CreateNetworkManager()
        {
            var listener = new EventBasedLiteNetListener();
            listener.ConnectionRequestEvent += request =>request.AcceptIfKey(AppKey);
            return (new LiteNetManager(listener, new Crc32cLayer()), listener);
        }
    }

    public class NetManagerStack : NetManagerStack<NetManager, EventBasedNetListener>
    {
        public NetManagerStack(string appKey, int serverPort) : base(appKey, serverPort)
        {
        }

        protected override (NetManager, EventBasedNetListener) CreateNetworkManager()
        {
            var listener = new EventBasedNetListener();
            listener.ConnectionRequestEvent += request =>request.AcceptIfKey(AppKey);
            return (new NetManager(listener, new Crc32cLayer()), listener);
        }
    }
}
