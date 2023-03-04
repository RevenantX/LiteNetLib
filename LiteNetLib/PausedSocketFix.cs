
using System.Net;

namespace LiteNetLib
{

    public class PausedSocketFix
    {
        public bool ApplicationFocused { get; private set; }

        private NetManager _netManager;
        private IPAddress _ipv4;
        private IPAddress _ipv6;
        private int _port;
        private bool _manualMode;

   
        public PausedSocketFix() 
        {
            UnityEngine.Application.focusChanged += Application_focusChanged;
        }
        public PausedSocketFix(NetManager netManager, IPAddress ipv4, IPAddress ipv6, int port, bool manualMode) : this()
        {
            Initialize(netManager, ipv4, ipv6, port, manualMode);
        }

        ~PausedSocketFix()
        {
            UnityEngine.Application.focusChanged -= Application_focusChanged;
        }

        public void Initialize(NetManager netManager, IPAddress ipv4, IPAddress ipv6, int port, bool manualMode)
        {
            _netManager = netManager;
            _ipv4 = ipv4;
            _ipv6 = ipv6;
            _port = port;
            _manualMode = manualMode;
        }


        private void Application_focusChanged(bool focused)
        {
            ApplicationFocused = focused;
            //If coming back into focus see if a reconnect is needed.
            if (focused)
                TryReconnect();
        }


        private void TryReconnect()
        {
            if (_netManager == null)
                return;
            //Was intentionally disconnected at some point.
            if (!_netManager.IsRunning)
                return;
            //Socket is still running.
            if (_netManager.SocketActive(false) || _netManager.SocketActive(true))
                return;

            //Socket isn't running but should be. Try to start again.
            if (!_netManager.Start(_ipv4, _ipv6, _port, _manualMode))
            {
                NetDebug.WriteError($"[S] Cannot restore connection. Ipv4 {_ipv4}, Ipv6 {_ipv6}, Port {_port}, ManualMode {_manualMode}");
                _netManager.CloseSocket(false);
            }
        }


    }

}
