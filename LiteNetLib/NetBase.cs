using System;
using System.Collections.Generic;
#if WINRT
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.Foundation;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
#else
using System.Threading;
#endif

namespace LiteNetLib
{
    public abstract class NetBase
    {
        protected NetSocket _socket;
        protected NetEndPoint _localEndPoint;

#if WINRT
        private IAsyncAction _updateAction;
        private readonly byte[] _reusableBuffer;
        private IBuffer _buffer;
#else
        private readonly Thread _logicThread;
        private readonly Thread _receiveThread;
        private NetEndPoint _remoteEndPoint;
#endif

        private int _updateTime;
        private bool _running;
        private readonly Queue<NetEvent> _netEventsQueue;
        private readonly Stack<NetEvent> _netEventsPool; 

        protected NetBase()
        {
            _netEventsQueue = new Queue<NetEvent>();
            _netEventsPool = new Stack<NetEvent>();
            
            _updateTime = 100;

#if WINRT
            _reusableBuffer = new byte[NetConstants.MaxPacketSize];
            _buffer = _reusableBuffer.AsBuffer();
            _socket = new NetSocket(ReceiveLogic);
#else
            _remoteEndPoint = new NetEndPoint(0);
            _logicThread = new Thread(UpdateLogic);
            _receiveThread = new Thread(ReceiveLogic);
            _socket = new NetSocket();
#endif
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
                _logicThread.Start();
                _receiveThread.Start();
#endif
                return true;
            }
            return false;
        }

        protected NetEvent GetOrCreateNetEvent()
        {
            if (_netEventsPool.Count > 0)
            {
                lock (_netEventsPool)
                {
                    return _netEventsPool.Pop();
                }
            }
            return new NetEvent();
        }

        public void Recycle(NetEvent netEvent)
        {
            lock (_netEventsPool)
            {
                netEvent.Data = null;
                _netEventsPool.Push(netEvent);
            }
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
                _logicThread.Join(2000);
                _receiveThread.Join(2000);
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

        protected void EnqueueEvent(NetPeer peer, byte[] data, NetEventType type)
        {
            NetEvent evt = GetOrCreateNetEvent();
            evt.Init(peer, data, type);
            lock (_netEventsQueue)
            {
                _netEventsQueue.Enqueue(evt);
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
                Task.Delay(_updateTime).Wait();
#else
                Thread.Sleep(_updateTime);
#endif
            }
        }

#if WINRT
        private void ReceiveLogic(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            var task = Task.Run(async () => 
            {
                var outputSteam = args.GetDataStream();
                return await outputSteam.ReadAsync(_buffer, _buffer.Length, InputStreamOptions.None);
            });
            task.Wait();

            ReceiveFromSocket(_reusableBuffer, (int)task.Result.Length, new NetEndPoint(args.RemoteAddress, args.RemotePort));
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
                    ReceiveFromSocket(reusableBuffer, result, _remoteEndPoint);
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

        protected virtual void ProcessError()
        {
            EnqueueEvent(null, null, NetEventType.Error);
        }

        protected abstract void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint);
        protected abstract void PostProcessEvent(int deltaTime);

        internal abstract void ReceiveFromPeer(NetPacket packet, NetEndPoint endPoint);
        internal abstract void ProcessSendError(NetEndPoint endPoint);
    }
}
