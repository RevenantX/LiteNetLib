using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace LiteNetLib
{
    public abstract class NetBase<T> : IPeerListener where T : NetBase<T>
    {
        protected NetSocket _socket;
        protected IPEndPoint _localEndPoint;

        private Thread _thread;
        private int _updateTime;
        private bool _running;
        private EndPoint _remoteEndPoint;
        private Stopwatch _tickWatch;
        private Queue<NetEvent> _netEventsQueue;

        protected NetBase()
        {
            _tickWatch = new Stopwatch();
            _netEventsQueue = new Queue<NetEvent>();
            _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            _updateTime = 100;
            _thread = new Thread(Update);
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
                _thread.Start();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Stop updating thread and listening
        /// </summary>
        public virtual void Stop()
        {
            if (_running)
            {
                _running = false;
                _thread.Join(1000);
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

        protected void EnqueueEvent(NetEvent netEvent)
        {
            lock (_netEventsQueue)
            {
                _netEventsQueue.Enqueue(netEvent);
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
        private void Update()
        {
            while (_running)
            {
                //Init timer
                long startTime = _tickWatch.ElapsedMilliseconds;
                long diffTime = 0;

                do
                {
                    int errorCode = 0;

                    //Receive some info
                    NetPacket packet;
                    int result = _socket.ReceiveFrom(out packet, ref _remoteEndPoint, ref errorCode);

                    if (result >= 0)
                    {
                        //ProcessEvents
                        if (packet != null)
                        {
                            ReceiveFromSocket(packet, _remoteEndPoint);
                        }
                    }
                    else
                    {
                        //If not 10054
                        if (errorCode != 10054)
                        {
                            //NetUtils.DebugWrite(ConsoleColor.Red, "(NB)Socket error!");

                            NetEvent netEvent = ProcessError();
                            if (netEvent != null)
                            {
                                EnqueueEvent(netEvent);
                            }
                            _running = false;
                            _socket.Close();
                            return;
                        }
                    }

                    //Calc diffTime
                    diffTime = _tickWatch.ElapsedMilliseconds - startTime;
                } while (diffTime < _updateTime && _running);

                //PostProcess
                PostProcessEvent((int)diffTime);
            }
        }

        protected virtual NetEvent ProcessError()
        {
            return new NetEvent(null, null, NetEventType.Error);
        }

        protected abstract void ReceiveFromSocket(NetPacket packet, EndPoint remoteEndPoint);
        protected abstract void PostProcessEvent(int deltaTime);

        public abstract void ReceiveFromPeer(NetPacket packet, EndPoint endPoint);
        public abstract void ProcessSendError(EndPoint endPoint);
    }
}
