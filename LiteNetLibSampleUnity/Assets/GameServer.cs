using System.Net;
using System.Net.Sockets;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

public class GameServer : MonoBehaviour, INetEventListener
{
    private NetManager _netServer;
    private NetPeer _ourPeer;
    private NetDataWriter _dataWriter;

    [SerializeField] private GameObject _serverBall;

    void Start()
    {
        _dataWriter = new NetDataWriter();
        _netServer = new NetManager(this);
        _netServer.Start(5000);
        _netServer.DiscoveryEnabled = true;
        _netServer.UpdateTime = 15;
    }

    void Update()
    {
        _netServer.PollEvents();
    }

    void FixedUpdate()
    {
        if (_ourPeer != null)
        {
            _serverBall.transform.Translate(1f * Time.fixedDeltaTime, 0f, 0f);
            _dataWriter.Reset();
            _dataWriter.Put(_serverBall.transform.position.x);
            _ourPeer.Send(_dataWriter, DeliveryMethod.Sequenced);
        }
    }

    void OnDestroy()
    {
        if (_netServer != null)
            _netServer.Stop();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Debug.Log("[SERVER] We have new peer " + peer.EndPoint);
        _ourPeer = peer;
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
    {
        Debug.Log("[SERVER] error " + socketErrorCode);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
        UnconnectedMessageType messageType)
    {
        if (messageType == UnconnectedMessageType.DiscoveryRequest)
        {
            Debug.Log("[SERVER] Received discovery request. Send discovery response");
            _netServer.SendDiscoveryResponse(new byte[] {1}, remoteEndPoint);
        }
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("sample_app");
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Debug.Log("[SERVER] peer disconnected " + peer.EndPoint + ", info: " + disconnectInfo.Reason);
        if (peer == _ourPeer)
            _ourPeer = null;
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
    }
}