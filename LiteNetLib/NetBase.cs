using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib.Utils;

#if WINRT
using Windows.System.Threading;
using Windows.Foundation;
using Windows.Networking.Sockets;
#endif

namespace LiteNetLib
{
    sealed class FlowMode
    {
        public int PacketsPerSecond;
        public int StartRTT;
    }

    public abstract class NetBase
    {
        internal readonly NetSocket Socket;
        private readonly List<FlowMode> _flowModes;
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

        //config section
        public bool UnconnectedMessagesEnabled = false;
        public bool NatPunchEnabled = false;

        /// <summary>
        /// Process and send packets delay
        /// </summary>
        public int UpdateTime
        {
            set { _updateTime = value; }
            get { return _updateTime; }
        }

        public void AddFlowMode(int startRtt, int packetsPerSecond)
        {
            var fm = new FlowMode {PacketsPerSecond = packetsPerSecond, StartRTT = startRtt};

            if (_flowModes.Count > 0 && startRtt < _flowModes[0].StartRTT)
            {
                _flowModes.Insert(0, fm);
            }
            else
            {
                _flowModes.Add(fm);
            }
        }

        internal int GetPacketsPerSecond(int flowMode)
        {
            if (flowMode < 0 || _flowModes.Count == 0)
                return NetConstants.PacketsPerSecondMax;
            return _flowModes[flowMode].PacketsPerSecond;
        }

        internal int GetMaxFlowMode()
        {
            return _flowModes.Count - 1;
        }

        internal int GetStartRtt(int flowMode)
        {
            if (flowMode < 0 || _flowModes.Count == 0)
                return 0;
            return _flowModes[flowMode].StartRTT;
        }

        public readonly NatPunchModule NatPunchModule;

        protected NetBase()
        {
            _flowModes = new List<FlowMode>();

            _netEventsQueue = new Queue<NetEvent>();
            _netEventsPool = new Stack<NetEvent>();
            
            _updateTime = 100;

#if WINRT
            Socket = new NetSocket(OnMessageReceived);
#else
            _remoteEndPoint = new NetEndPoint(0);
            Socket = new NetSocket();
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

            if (Socket.Bind(_localEndPoint))
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
            return SendUnconnectedMessage(message, message.Length, remoteEndPoint);
        }

        public bool SendUnconnectedMessage(byte[] message, int length, NetEndPoint remoteEndPoint)
        {
            if (!_running)
                return false;
            NetPacket p = new NetPacket();
            p.Init(PacketProperty.UnconnectedMessage, length);
            p.PutData(message, length);
            return Socket.SendTo(p.RawData, remoteEndPoint) > 0;
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
                Socket.Close();
            }
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

        internal NetEvent CreateEvent(NetEventType type)
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
            evt.Type = type;
            return evt;
        }

        internal void EnqueueEvent(NetEvent evt)
        {
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
                netEvent.AdditionalInfo = string.Empty;
                netEvent.RemoteEndPoint = null;
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
                int result = Socket.ReceiveFrom(ref reusableBuffer, ref _remoteEndPoint, ref errorCode);

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
                        NetUtils.DebugWrite(ConsoleColor.Red, "(NB)Socket error!");
                        ProcessError("Receive socket error: " + errorCode);
                        Stop();
                        return;
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
                    {
                        var netEvent = CreateEvent(NetEventType.ReceiveUnconnected);
                        netEvent.RemoteEndPoint = remoteEndPoint;
                        netEvent.DataReader.SetSource(NetPacket.GetUnconnectedData(reusableBuffer, count));
                        EnqueueEvent(netEvent);
                    }
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

        protected abstract void ProcessError(string errorMessage);

        protected abstract void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint);
        protected abstract void PostProcessEvent(int deltaTime);

        internal abstract void ReceiveFromPeer(NetPacket packet, NetEndPoint endPoint);
        internal abstract void ProcessSendError(NetEndPoint endPoint, string errorMessage);
    }
}
