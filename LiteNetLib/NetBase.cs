using System;
using System.Net;
using System.Threading;

namespace LiteNetLib
{
    public abstract class NetBase<T> where T : NetBase<T>
    {
        public delegate void NetEventReceivedDelegate(T sender, NetEvent netEvent);

        protected NetSocket socket;

        private Thread _thread;
        private int _updateTime;
        private event NetEventReceivedDelegate NetEventReceived;
        private bool _running;
        private EndPoint _remoteEndPoint;

        protected NetBase()
        {
            _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            _updateTime = 100;
            _thread = new Thread(Update);

            socket = new NetSocket();
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
            if (socket.Bind(port))
            {
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
                socket.Close();
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

        /// <summary>
        /// Subscribe method to receive events
        /// </summary>
        /// <param name="func"></param>
        public void AddNetEventListener(NetEventReceivedDelegate func)
        {
            NetEventReceived += func;
        }

        protected void CallNetEventReceived(NetEvent netEvent)
        {
            NetEventReceived((T)this, netEvent);
        }

        //Update function
        private void Update()
        {
            NetPacket packet;
            int startTime, diffTime;

            while (_running)
            {
                //Init timer
                startTime = Environment.TickCount;

                do
                {
                    int errorCode = 0;
                    //Receive some info
                    int result = socket.ReceiveFrom(out packet, ref _remoteEndPoint, ref errorCode);

                    if (result >= 0)
                    {
                        //ProcessEvents
                        if (packet != null)
                        {
                            NetEvent netEvent = ProcessPacket(packet, _remoteEndPoint);
                            if (netEvent != null)
                            {
                                NetEventReceived((T)this, netEvent);
                            }
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
                                NetEventReceived((T)this, netEvent);
                            }
                            _running = false;
                            socket.Close();
                            return;
                        }
                    }

                    //Calc diffTime
                    diffTime = Environment.TickCount - startTime;
                } while (diffTime < _updateTime && _running);

                //PostProcess
                PostProcessEvent(diffTime);
            }
        }

        protected virtual NetEvent ProcessError()
        {
            return new NetEvent(null, null, NetEventType.Error);
        }

        protected abstract NetEvent ProcessPacket(NetPacket packet, EndPoint remoteEndPoint);
        protected abstract void PostProcessEvent(int deltaTime);
    }
}
