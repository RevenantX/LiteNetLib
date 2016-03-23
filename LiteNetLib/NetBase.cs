using System.Collections.Generic;
using System.Threading;
using LiteNetLib.Utils;

#if WINRT
using Windows.System.Threading;
#else
using System;
#endif

namespace LiteNetLib
{
    sealed class FlowMode
    {
        public int PacketsPerSecond;
        public int StartRtt;
    }

    public class NetBase
    {
        private readonly NetSocket _socket;
        private readonly List<FlowMode> _flowModes;
        private NetEndPoint _localEndPoint;

#if WINRT
        private readonly ManualResetEvent _updateWaiter = new ManualResetEvent(false);
#else
        private Thread _logicThread;
        private Thread _receiveThread;
#endif
        private NetEndPoint _remoteEndPoint;

        private bool _running;
        private readonly Queue<NetEvent> _netEventsQueue;
        private readonly Stack<NetEvent> _netEventsPool;

        //config section
        public bool UnconnectedMessagesEnabled = false;
        public bool NatPunchEnabled = false;
        public int UpdateTime = 100;
        public int ReliableResendTime = 500;

        //modules
        public readonly NatPunchModule NatPunchModule;

        public void AddFlowMode(int startRtt, int packetsPerSecond)
        {
            var fm = new FlowMode {PacketsPerSecond = packetsPerSecond, StartRtt = startRtt};

            if (_flowModes.Count > 0 && startRtt < _flowModes[0].StartRtt)
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
            return _flowModes[flowMode].StartRtt;
        }

        protected NetBase()
        {
            _flowModes = new List<FlowMode>();

            _netEventsQueue = new Queue<NetEvent>();
            _netEventsPool = new Stack<NetEvent>();

            _remoteEndPoint = new NetEndPoint(0);
            _socket = new NetSocket();

            NatPunchModule = new NatPunchModule(this, _socket);
        }

        protected NetPeer CreatePeer(NetEndPoint remoteEndPoint)
        {
            return new NetPeer(this, _socket, remoteEndPoint);
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
                ThreadPool.RunAsync(
                    a => UpdateLogic(), 
                    WorkItemPriority.Normal, 
                    WorkItemOptions.TimeSliced).GetResults();

                ThreadPool.RunAsync(
                    a => ReceiveLogic(), 
                    WorkItemPriority.Normal,
                    WorkItemOptions.TimeSliced).GetResults();
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
            p.PutData(message, 0, length);
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
                if(Thread.CurrentThread != _logicThread)
                    _logicThread.Join();
                if(Thread.CurrentThread != _receiveThread)
                    _receiveThread.Join();
                _logicThread = null;
                _receiveThread = null;
#endif
                _socket.Close();
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

        protected NetEvent CreateEvent(NetEventType type)
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
                PostProcessEvent(UpdateTime);
#if WINRT
                _updateWaiter.WaitOne(UpdateTime);
#else
                Thread.Sleep(UpdateTime);
#endif
            }
        }

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
                    //10054 - remote close (not error)
                    //10040 - message too long (impossible now)
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

        protected virtual void ProcessError(string errorMessage)
        {
            var netEvent = CreateEvent(NetEventType.Error);
            netEvent.AdditionalInfo = errorMessage;
            EnqueueEvent(netEvent);
        }

        protected virtual void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint)
        {
            //ignore because we can accept only not connected messages and nat messages
        }

        protected virtual void PostProcessEvent(int deltaTime)
        {
            
        }

        internal virtual void ReceiveFromPeer(NetPacket packet, NetEndPoint endPoint)
        {

        }

        internal virtual void ProcessSendError(NetEndPoint endPoint, string errorMessage)
        {
            var netEvent = CreateEvent(NetEventType.Error);
            netEvent.AdditionalInfo = string.Format("Send error to {0}: {1}", endPoint, errorMessage);
            EnqueueEvent(netEvent);
        }
    }
}
