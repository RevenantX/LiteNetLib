using System;
using System.Threading;

namespace LiteNetLib
{
    internal sealed class NetThread
    {
        private Thread _thread;

        private readonly Action _callback;

        public int SleepTime;
        private volatile bool _running;
        private readonly string _name;

        public bool IsRunning
        {
            get { return _running; }
        }

        public NetThread(string name, int sleepTime, Action callback)
        {
            _callback = callback;
            SleepTime = sleepTime;
            _name = name;
        }

        public void Sleep(int msec)
        {
            Thread.Sleep(msec);
        }

        public void Start()
        {
            if (_running)
                return;
            _running = true;
            _thread = new Thread(ThreadLogic)
            {
                Name = _name,
                IsBackground = true
            };
            _thread.Start();
        }

        public void Stop()
        {
            if (!_running)
                return;
            _running = false;
            _thread.Join();
        }

        private void ThreadLogic()
        {
            while (_running)
            {
                _callback();
                Thread.Sleep(SleepTime);
            }
        }
    }
}
