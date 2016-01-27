using System.Collections.Generic;
using System.Threading;
using LiteNetLib.Utils;

#if WINRT
using System;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.Foundation;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
#endif

namespace LiteNetLib
{
    public abstract class NetBase
    {
        internal NetSocket _socket;
        protected NetEndPoint _localEndPoint;

#if WINRT
        private readonly ManualResetEvent _manualResetEvent = new ManualResetEvent(false);
        private IAsyncAction _updateAction;
#else
        private Thread _logicThread;
        private Thread _receiveThread;
        private NetEndPoint _remoteEndPoint;
#endif

        private int _updateTime;
        private bool _running;
        private readonly Queue<NetEvent> _netEventsQueue;
        private readonly Stack<NetEvent> _netEventsPool;

        public bool UnconnectedMessagesEnabled = false;
        public bool NatPunchEnabled = false;

        public readonly NatPunchModule NatPunchModule;

        protected NetBase()
        {
            _netEventsQueue = new Queue<NetEvent>();
            _netEventsPool = new Stack<NetEvent>();
            
            _updateTime = 100;

#if WINRT
            _socket = new NetSocket(OnMessageReceived);
#else
            _remoteEndPoint = new NetEndPoint(0);
            _socket = new NetSocket();
#endif

            NatPunchModule = new NatPunchModule(this);
        }

        /// <summary>
        /// Start updating thread and listening on selected port
        /// </summary>
        /// <param name="port">port to listen</param>
        public virtual bool Start(int port)
        {
            if (_running)
            {
                return false;
            }

            _localEndPoint = new NetEndPoint(port);

            if (_socket.Bind(_localEndPoint))
            {
                _running = true;
#if WINRT
                _updateAction = ThreadPool.RunAsync(a => UpdateLogic(), WorkItemPriority.Normal, WorkItemOptions.TimeSliced);
#else
                _logicThread = new Thread(UpdateLogic);
                _receiveThread = new Thread(ReceiveLogic);
                _logicThread.Start();
                _receiveThread.Start();
#endif
                return true;
            }
            return false;
        }

        public bool SendUnconnectedMessage(byte[] message, NetEndPoint remoteEndPoint)
        {
            if (!_running)
                return false;
            NetPacket p = new NetPacket();
            p.Init(PacketProperty.UnconnectedMessage, message.Length);
            p.PutData(message);
            return _socket.SendTo(p.RawData, remoteEndPoint) > 0;
        }

        /// <summary>
        /// Stop updating thread and listening
        /// </summary>
        public virtual void Stop()
        {
            if (_running)
            {
                _running = false;
#if !WINRT
                _logicThread.Join();
                _receiveThread.Join();
                _logicThread = null;
                _receiveThread = null;
#endif
                _socket.Close();
            }
        }

        /// <summary>
        /// Process and send packets delay
        /// </summary>
        public int UpdateTime
        {
            set { _updateTime = value; }
            get { return _updateTime; }
        }

        /// <summary>
        /// Returns true if socket listening and update thread is running
        /// </summary>
        public bool IsRunning
        {
            get { return _running; }
        }

        public NetEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        internal void EnqueueEvent(NetEndPoint remoteEndPoint, byte[] data, NetEventType type)
        {
            EnqueueEvent(null, remoteEndPoint, data, type);
        }

        internal void EnqueueEvent(NetPeer peer, byte[] data, NetEventType type)
        {
            EnqueueEvent(peer, peer.EndPoint, data, type);
        }

        internal void EnqueueEvent(NetEventType type)
        {
            EnqueueEvent(null, null, null, type);
        }

        internal void EnqueueEvent(NetPeer peer, NetEndPoint remoteEndPoint, byte[] data, NetEventType type)
        {
            NetEvent evt;
            if (_netEventsPool.Count > 0)
            {
                lock (_netEventsPool)
                {
                    evt = _netEventsPool.Pop();
                }
            }
            else
            {
                evt = new NetEvent();
                evt.DataReader = new NetDataReader();
            }
            if (data != null)
                evt.DataReader.SetSource(data);

            evt.Peer = peer;
            evt.Type = type;
            evt.RemoteEndPoint = remoteEndPoint;
            lock (_netEventsQueue)
            {
                _netEventsQueue.Enqueue(evt);
            }
        }

        public void Recycle(NetEvent netEvent)
        {
            lock (_netEventsPool)
            {
                netEvent.DataReader.Clear();
                netEvent.Peer = null;
                _netEventsPool.Push(netEvent);
            }
        }

        public NetEvent GetNextEvent()
        {
            if (_netEventsQueue.Count > 0)
            {
                lock(_netEventsQueue)
                {
                    return _netEventsQueue.Dequeue();
                }
            }
            return null;
        }

        //Update function
        private void UpdateLogic()
        {
            while (_running)
            {
                PostProcessEvent(_updateTime);
#if WINRT
                _manualResetEvent.WaitOne(_updateTime);
#else
                Thread.Sleep(_updateTime);
#endif
            }
        }

#if WINRT
        private readonly NetEndPoint _tempEndPoint = new NetEndPoint(0);
        private void OnMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var dataReader = args.GetDataReader();
            uint count = dataReader.UnconsumedBufferLength;
            if (count > 0)
            {
                byte[] data = new byte[count];
                dataReader.ReadBytes(data);
                _tempEndPoint.Set(args.RemoteAddress, args.RemotePort);
                DataReceived(data, data.Length, _tempEndPoint);
            }
        }
#else
        private void ReceiveLogic()
        {
            while (_running)
            {
                int errorCode = 0;

                //Receive some info
                byte[] reusableBuffer = null;
                int result = _socket.ReceiveFrom(ref reusableBuffer, ref _remoteEndPoint, ref errorCode);

                if (result > 0)
                {
                    //ProcessEvents
                    DataReceived(reusableBuffer, result, _remoteEndPoint);
                }
                else if (result < 0)
                {
                    //If not 10054
                    if (errorCode != 10054)
                    {
                        //NetUtils.DebugWrite(ConsoleColor.Red, "(NB)Socket error!");
                        ProcessError();
                        _running = false;
                        _socket.Close();
                    }
                }
            }
        }
#endif

        private void DataReceived(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint)
        {
            //Try get packet property
            PacketProperty property;
            if (!NetPacket.GetPacketProperty(reusableBuffer, out property))
                return;

            //Check unconnected
            switch (property)
            {
                case PacketProperty.UnconnectedMessage:
                    if (UnconnectedMessagesEnabled)
                        EnqueueEvent(remoteEndPoint, NetPacket.GetUnconnectedData(reusableBuffer, count), NetEventType.ReceiveUnconnected);
                    return;
                case PacketProperty.NatIntroduction:
                case PacketProperty.NatIntroductionRequest:
                case PacketProperty.NatPunchMessage:
                    if(NatPunchEnabled)
                        NatPunchModule.ProcessMessage(remoteEndPoint, property, NetPacket.GetUnconnectedData(reusableBuffer, count));
                    return;
            }

            //other
            ReceiveFromSocket(reusableBuffer, count, remoteEndPoint);
        }

        protected abstract void ProcessError();

        protected abstract void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint);
        protected abstract void PostProcessEvent(int deltaTime);

        internal abstract void ReceiveFromPeer(NetPacket packet, NetEndPoint endPoint);
        internal abstract void ProcessSendError(NetEndPoint endPoint);
    }
}
