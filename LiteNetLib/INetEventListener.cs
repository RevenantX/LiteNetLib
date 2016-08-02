using System;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    public enum UnconnectedMessageType
    {
        Default,
        DiscoveryRequest,
        DiscoveryResponse
    }

    public interface INetEventListener
    {
        void OnPeerConnected(NetPeer peer);
        void OnPeerDisconnected(NetPeer peer, string additionalInfo);
        void OnNetworkError(NetEndPoint endPoint, string error);
        void OnNetworkReceive(NetPeer peer, NetDataReader reader);
        void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType);
        void OnNetworkLatencyUpdate(NetPeer peer, int latency);
    }

    public class EventBasedNetListener : INetEventListener
    {
        public event Action<NetPeer> PeerConnectedEvent;
        public event Action<NetPeer, string> PeerDisconnectedEvent;
        public event Action<NetEndPoint, string> NetworkErrorEvent;
        public event Action<NetPeer, NetDataReader> NetworkReceiveEvent;
        public event Action<NetEndPoint, NetDataReader, UnconnectedMessageType> NetworkReceiveUnconnectedEvent;
        public event Action<NetPeer, int> NetworkLatencyUpdateEvent; 
         
        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            if (PeerConnectedEvent != null)
                PeerConnectedEvent(peer);
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, string additionalInfo)
        {
            if (PeerDisconnectedEvent != null)
                PeerDisconnectedEvent(peer, additionalInfo);
        }

        void INetEventListener.OnNetworkError(NetEndPoint endPoint, string error)
        {
            if (NetworkErrorEvent != null)
                NetworkErrorEvent(endPoint, error);
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetDataReader reader)
        {
            if (NetworkReceiveEvent != null)
                NetworkReceiveEvent(peer, reader);
        }

        void INetEventListener.OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
        {
            if (NetworkReceiveUnconnectedEvent != null)
                NetworkReceiveUnconnectedEvent(remoteEndPoint, reader, messageType);
        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            if (NetworkLatencyUpdateEvent != null)
                NetworkLatencyUpdateEvent(peer, latency);
        }
    }
}
