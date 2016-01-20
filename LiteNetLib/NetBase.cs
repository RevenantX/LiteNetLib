using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
#if NETFX_CORE
using Windows.System.Threading;
using Windows.Foundation;
using System.Threading.Tasks;
#else
using System.Threading;
#endif

namespace LiteNetLib
{
    public abstract class NetBase
    {
        protected NetSocket _socket;
        protected IPEndPoint _localEndPoint;

#if NETFX_CORE
        private IAsyncAction _updateAction;
#else
        private Thread _thread;
#endif

        private int _updateTime;
        private bool _running;
        private IPEndPoint _remoteEndPoint;
        private readonly Stopwatch _tickWatch;
        private readonly Queue<NetEvent> _netEventsQueue;
        private readonly Stack<NetEvent> _netEventsPool; 

        protected NetBase()
        {
            _tickWatch = new Stopwatch();
            _netEventsQueue = new Queue<NetEvent>();
            _netEventsPool = new Stack<NetEvent>();
            _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            _updateTime = 100;
#if !NETFX_CORE
            _thread = new Thread(Update);
#endif
            _socket = new NetSocket();
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
            _localEndPoint = new IPEndPoint(IPAddress.Any, port);
            if (_socket.Bind(_localEndPoint))
            {
                _tickWatch.Start();
                _running = true;
#if NETFX_CORE
                _updateAction = ThreadPool.RunAsync(Update);
#else
                _thread.Start();
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
#if !NETFX_CORE
                _thread.Join(1000);
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
#if NETFX_CORE
        private void Update(IAsyncAction operation)
        {
            while (_running)
            {
                //Init timer
                long startTime = _tickWatch.ElapsedMilliseconds;

                while (_tickWatch.ElapsedMilliseconds - startTime < _updateTime && _running)
                {
                    ReceiveLogic();
                    Task.Delay(1).Wait();
                }

                //PostProcess
                PostProcessEvent(_updateTime);
            }
        }
#else
        private void Update()
        {
            while (_running)
            {
                //Init timer
                long startTime = _tickWatch.ElapsedMilliseconds;

                while(_tickWatch.ElapsedMilliseconds - startTime < _updateTime && _running)
                {
                    ReceiveLogic();
                    Thread.Sleep(1);
                }

                //PostProcess
                PostProcessEvent(_updateTime);
            }
        }
#endif

        private bool ReceiveLogic()
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
                    return false;
                }
            }
            return true;
        }

        protected virtual void ProcessError()
        {
            EnqueueEvent(null, null, NetEventType.Error);
        }

        protected abstract void ReceiveFromSocket(byte[] reusableBuffer, int count, IPEndPoint remoteEndPoint);
        protected abstract void PostProcessEvent(int deltaTime);

        internal abstract void ReceiveFromPeer(NetPacket packet, IPEndPoint endPoint);
        internal abstract void ProcessSendError(IPEndPoint endPoint);
    }
}
