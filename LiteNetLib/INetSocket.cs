using System.Net;

namespace LiteNetLib
{
    public interface INetSocket
    {
        bool Bind(IPEndPoint ep);
        int SendTo(NetPacket packet, EndPoint endPoint);
        int ReceiveFrom(out NetPacket packet, ref EndPoint remoteEp, ref int errorCode);
        void Close();
    }
}
