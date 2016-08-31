using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

public class GameClient : MonoBehaviour, INetEventListener
{
    private NetClient _netClient;

    [SerializeField] private GameObject _clientBall;
    [SerializeField] private GameObject _clientBallInterpolated;

    private float _newBallPosX;
    private float _oldBallPosX;
    private float _lerpTime;

    void Start ()
    {
        _netClient = new NetClient(this, "sample_app");
	    _netClient.Start();
        _netClient.Connect("localhost", 5000);
	    _netClient.UpdateTime = 15;
    }

	void Update ()
    {
	    _netClient.PollEvents();

	    if (_netClient.IsConnected)
	    {
            //Fixed delta set to 0.05
	        var pos = _clientBallInterpolated.transform.position;
	        pos.x = Mathf.Lerp(_oldBallPosX, _newBallPosX, _lerpTime);
	        _clientBallInterpolated.transform.position = pos;

            //Basic lerp
	        _lerpTime += Time.deltaTime / Time.fixedDeltaTime;
	    }
    }

    void OnDestroy()
    {
        _netClient.Stop();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Debug.Log("[CLIENT] We connected to " + peer.EndPoint);
    }

    public void OnPeerDisconnected(NetPeer peer, string additionalInfo)
    {
        Debug.Log("[CLIENT] We disconnected because " + additionalInfo);
    }

    public void OnNetworkError(NetEndPoint endPoint, string error)
    {
        Debug.Log("[CLIENT] We received error " + error);
    }

    public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
    {
        _newBallPosX = reader.GetFloat();

        var pos = _clientBall.transform.position;

        _oldBallPosX = pos.x;
        pos.x = _newBallPosX;

        _clientBall.transform.position = pos;

        _lerpTime = 0f;
    }

    public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
    {
       
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        
    }
}
