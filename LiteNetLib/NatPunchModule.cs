using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    /// <summary>
    /// Specifies the type of network address discovered during NAT punchthrough.
    /// </summary>
    public enum NatAddressType
    {
        /// <summary>
        /// Address within the local area network (LAN).
        /// </summary>
        Internal,
        /// <summary>
        /// Publicly accessible address on the wide area network (WAN).
        /// </summary>
        External
    }

    /// <summary>
    /// Interface for handling events related to NAT punchthrough and introduction.
    /// </summary>
    public interface INatPunchListener
    {
        /// <summary>
        /// Called when a NAT introduction request is received from the mediator server.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint of the client requesting connection.</param>
        /// <param name="remoteEndPoint">The remote endpoint of the client requesting connection.</param>
        /// <param name="token">Custom data token associated with the request.</param>
        void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token);

        /// <summary>
        /// Called when NAT punchthrough is successful and a direct connection can be established.
        /// </summary>
        /// <param name="targetEndPoint">The resolved endpoint of the remote peer.</param>
        /// <param name="type">The type of address (Internal or External) that succeeded.</param>
        /// <param name="token">Custom data token associated with the request.</param>
        void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token);
    }

    /// <summary>
    /// An implementation of <see cref="INatPunchListener"/> that maps callbacks to events.
    /// </summary>
    public class EventBasedNatPunchListener : INatPunchListener
    {
        /// <summary>
        /// Delegate for NAT introduction request events.
        /// </summary>
        public delegate void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token);

        /// <summary>
        /// Delegate for NAT introduction success events.
        /// </summary>
        public delegate void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token);

        /// <summary>
        /// Event triggered when a NAT introduction request is received.
        /// </summary>
        public event OnNatIntroductionRequest NatIntroductionRequest;

        /// <summary>
        /// Event triggered when NAT punchthrough is successfully completed.
        /// </summary>
        public event OnNatIntroductionSuccess NatIntroductionSuccess;

        void INatPunchListener.OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            if(NatIntroductionRequest != null)
                NatIntroductionRequest(localEndPoint, remoteEndPoint, token);
        }

        void INatPunchListener.OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            if (NatIntroductionSuccess != null)
                NatIntroductionSuccess(targetEndPoint, type, token);
        }
    }

    /// <summary>
    /// Module for UDP NAT Hole punching operations. Can be accessed from NetManager
    /// </summary>
    public sealed class NatPunchModule
    {
        struct RequestEventData
        {
            public IPEndPoint LocalEndPoint;
            public IPEndPoint RemoteEndPoint;
            public string Token;
        }

        struct SuccessEventData
        {
            public IPEndPoint TargetEndPoint;
            public NatAddressType Type;
            public string Token;
        }

        class NatIntroduceRequestPacket
        {
            public IPEndPoint Internal { [Preserve] get; [Preserve] set; }
            public string Token { [Preserve] get; [Preserve] set; }
        }

        class NatIntroduceResponsePacket
        {
            public IPEndPoint Internal { [Preserve] get; [Preserve] set; }
            public IPEndPoint External { [Preserve] get; [Preserve] set; }
            public string Token { [Preserve] get; [Preserve] set; }
        }

        class NatPunchPacket
        {
            public string Token { [Preserve] get; [Preserve] set; }
            public bool IsExternal { [Preserve] get; [Preserve] set; }
        }

        private readonly LiteNetManager _socket;
        private readonly ConcurrentQueue<RequestEventData> _requestEvents = new ConcurrentQueue<RequestEventData>();
        private readonly ConcurrentQueue<SuccessEventData> _successEvents = new ConcurrentQueue<SuccessEventData>();
        private readonly NetDataReader _cacheReader = new NetDataReader();
        private readonly NetDataWriter _cacheWriter = new NetDataWriter();
        private readonly NetPacketProcessor _netPacketProcessor = new NetPacketProcessor(MaxTokenLength);
        private INatPunchListener _natPunchListener;
        /// <summary>
        /// Maximum allowed length for the NAT introduction token string.
        /// </summary>
        public const int MaxTokenLength = 256;

        /// <summary>
        /// Events automatically will be called without PollEvents method from another thread
        /// </summary>
        public bool UnsyncedEvents = false;

        internal NatPunchModule(LiteNetManager socket)
        {
            _socket = socket;
            _netPacketProcessor.SubscribeReusable<NatIntroduceResponsePacket>(OnNatIntroductionResponse);
            _netPacketProcessor.SubscribeReusable<NatIntroduceRequestPacket, IPEndPoint>(OnNatIntroductionRequest);
            _netPacketProcessor.SubscribeReusable<NatPunchPacket, IPEndPoint>(OnNatPunch);
        }

        internal void ProcessMessage(IPEndPoint senderEndPoint, NetPacket packet)
        {
            lock (_cacheReader)
            {
                _cacheReader.SetSource(packet.RawData, NetConstants.HeaderSize, packet.Size);
                _netPacketProcessor.ReadAllPackets(_cacheReader, senderEndPoint);
            }
        }

        /// <summary>
        /// Initializes the NAT punch module with a listener to handle punchthrough events.
        /// </summary>
        /// <param name="listener">The listener implementation that will receive NAT events.</param>
        public void Init(INatPunchListener listener)
        {
            _natPunchListener = listener;
        }

        private void Send<
#if NET5_0_OR_GREATER
            [DynamicallyAccessedMembers(Trimming.SerializerMemberTypes)]
#endif
        T>(T packet, IPEndPoint target) where T : class, new()
        {
            _cacheWriter.Reset();
            _cacheWriter.Put((byte)PacketProperty.NatMessage);
            _netPacketProcessor.Write(_cacheWriter, packet);
            _socket.SendRaw(_cacheWriter.Data, 0, _cacheWriter.Length, target);
        }

        /// <summary>
        /// Sends NAT introduction packets to both the host and the client to facilitate punchthrough.
        /// </summary>
        /// <remarks>
        /// This is typically called by a mediator (e.g. a master server).
        /// </remarks>
        /// <param name="hostInternal">Internal (LAN) endpoint of the host.</param>
        /// <param name="hostExternal">External (WAN) endpoint of the host.</param>
        /// <param name="clientInternal">Internal (LAN) endpoint of the client.</param>
        /// <param name="clientExternal">External (WAN) endpoint of the client.</param>
        /// <param name="additionalInfo">Custom token or data to include in the introduction.</param>
        public void NatIntroduce(
            IPEndPoint hostInternal,
            IPEndPoint hostExternal,
            IPEndPoint clientInternal,
            IPEndPoint clientExternal,
            string additionalInfo)
        {
            var req = new NatIntroduceResponsePacket
            {
                Token = additionalInfo
            };

            //First packet (server) send to client
            req.Internal = hostInternal;
            req.External = hostExternal;
            Send(req, clientExternal);

            //Second packet (client) send to server
            req.Internal = clientInternal;
            req.External = clientExternal;
            Send(req, hostExternal);
        }

        /// <summary>
        /// Triggers queued NAT punchthrough events (Success or Request) on the provided <see cref="INatPunchListener"/>.
        /// </summary>
        /// <remarks>
        /// This should be called from the main thread if <see cref="UnsyncedEvents"/> is <see langword="false"/>.
        /// </remarks>
        public void PollEvents()
        {
            if (UnsyncedEvents)
                return;

            if (_natPunchListener == null || (_successEvents.IsEmpty && _requestEvents.IsEmpty))
                return;

            while (_successEvents.TryDequeue(out var evt))
            {
                _natPunchListener.OnNatIntroductionSuccess(
                    evt.TargetEndPoint,
                    evt.Type,
                    evt.Token);
            }

            while (_requestEvents.TryDequeue(out var evt))
            {
                _natPunchListener.OnNatIntroductionRequest(evt.LocalEndPoint, evt.RemoteEndPoint, evt.Token);
            }
        }

        /// <summary>
        /// Sends a request to the Master Server to introduce this peer to another peer.
        /// </summary>
        /// <param name="host">The hostname or IP of the Master Server.</param>
        /// <param name="port">The port of the Master Server.</param>
        /// <param name="additionalInfo">Custom token to identify the connection or room.</param>
        public void SendNatIntroduceRequest(string host, int port, string additionalInfo)
        {
            SendNatIntroduceRequest(NetUtils.MakeEndPoint(host, port), additionalInfo);
        }

        /// <summary>
        /// Sends a request to the Master Server to introduce this peer to another peer.
        /// </summary>
        /// <param name="masterServerEndPoint">The endpoint of the Master Server.</param>
        /// <param name="additionalInfo">Custom token to identify the connection or room.</param>
        public void SendNatIntroduceRequest(IPEndPoint masterServerEndPoint, string additionalInfo)
        {
            //prepare outgoing data
            string networkIp = NetUtils.GetLocalIp(LocalAddrType.IPv4);
            if (string.IsNullOrEmpty(networkIp) || masterServerEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                networkIp = NetUtils.GetLocalIp(LocalAddrType.IPv6);
            }

            Send(
                new NatIntroduceRequestPacket
                {
                    Internal = NetUtils.MakeEndPoint(networkIp, _socket.LocalPort),
                    Token = additionalInfo
                },
                masterServerEndPoint);
        }

        //We got request and must introduce
        private void OnNatIntroductionRequest(NatIntroduceRequestPacket req, IPEndPoint senderEndPoint)
        {
            if (UnsyncedEvents)
            {
                _natPunchListener.OnNatIntroductionRequest(
                    req.Internal,
                    senderEndPoint,
                    req.Token);
            }
            else
            {
                _requestEvents.Enqueue(new RequestEventData
                {
                    LocalEndPoint = req.Internal,
                    RemoteEndPoint = senderEndPoint,
                    Token = req.Token
                });
            }
        }

        //We got introduce and must punch
        private void OnNatIntroductionResponse(NatIntroduceResponsePacket req)
        {
            NetDebug.Write(NetLogLevel.Trace, "[NAT] introduction received");

            // send internal punch
            var punchPacket = new NatPunchPacket {Token = req.Token};
            Send(punchPacket, req.Internal);
            NetDebug.Write(NetLogLevel.Trace, $"[NAT] internal punch sent to {req.Internal}");

            // hack for some routers
            _socket.Ttl = 2;
            _socket.SendRaw(new[] { (byte)PacketProperty.Empty }, 0, 1, req.External);

            // send external punch
            _socket.Ttl = NetConstants.SocketTTL;
            punchPacket.IsExternal = true;
            Send(punchPacket, req.External);
            NetDebug.Write(NetLogLevel.Trace, $"[NAT] external punch sent to {req.External}");
        }

        //We got punch and can connect
        private void OnNatPunch(NatPunchPacket req, IPEndPoint senderEndPoint)
        {
            //Read info
            NetDebug.Write(NetLogLevel.Trace, $"[NAT] punch received from {senderEndPoint} - additional info: {req.Token}");

            //Release punch success to client; enabling him to Connect() to Sender if token is ok
            if(UnsyncedEvents)
            {
                _natPunchListener.OnNatIntroductionSuccess(
                    senderEndPoint,
                    req.IsExternal ? NatAddressType.External : NatAddressType.Internal,
                    req.Token
                    );
            }
            else
            {
                _successEvents.Enqueue(new SuccessEventData
                {
                    TargetEndPoint = senderEndPoint,
                    Type = req.IsExternal ? NatAddressType.External : NatAddressType.Internal,
                    Token = req.Token
                });
            }
        }
    }
}
