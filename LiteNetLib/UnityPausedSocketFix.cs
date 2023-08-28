using System;
using System.Net;
using System.Reflection;

namespace LiteNetLib
{
    public class UnityPausedSocketFix
    {
        private readonly NetManager _netManager;
        private readonly IPAddress _ipv4;
        private readonly IPAddress _ipv6;
        private readonly int _port;
        private readonly bool _manualMode;
        private bool _initialized;
        private static EventInfo focusChangedEvent;

        public UnityPausedSocketFix(NetManager netManager, IPAddress ipv4, IPAddress ipv6, int port, bool manualMode)
        {
            if (focusChangedEvent == null)
            {
                focusChangedEvent = Type.GetType("UnityEngine.Application, UnityEngine.CoreModule")?.GetEvent("focusChanged", BindingFlags.Public | BindingFlags.Static);
                if (focusChangedEvent == null)
                {
                    NetDebug.WriteError($"Cannot find UnityEngine.Application.focusChanged event. {nameof(UnityPausedSocketFix)} will not work.");
                    return;
                }
            }

            _netManager = netManager;
            _ipv4 = ipv4;
            _ipv6 = ipv6;
            _port = port;
            _manualMode = manualMode;
            focusChangedEvent.AddEventHandler(this, (Action<bool>)Application_focusChanged);
            _initialized = true;
        }

        public void Deinitialize()
        {
            if (_initialized && focusChangedEvent != null)
                focusChangedEvent.RemoveEventHandler(this, (Action<bool>)Application_focusChanged);
            _initialized = false;
        }

        private void Application_focusChanged(bool focused)
        {
            //If coming back into focus see if a reconnect is needed.
            if (focused)
            {
                //try reconnect
                if (!_initialized)
                    return;
                //Was intentionally disconnected at some point.
                if (!_netManager.IsRunning)
                    return;
                //Socket is in working state.
                if (_netManager.NotConnected == false)
                    return;

                //Socket isn't running but should be. Try to start again.
                if (!_netManager.Start(_ipv4, _ipv6, _port, _manualMode))
                {
                    NetDebug.WriteError($"[S] Cannot restore connection. Ipv4 {_ipv4}, Ipv6 {_ipv6}, Port {_port}, ManualMode {_manualMode}");
                }
            }
        }
    }
}
