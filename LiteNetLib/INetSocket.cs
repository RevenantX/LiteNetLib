using System.Net;

namespace LiteNetLib
{
    public interface INetSocket
    {
        bool Bind(int port);
        int SendTo(NetPacket packet, EndPoint endPoint);
        int ReceiveFrom(out NetPacket packet, ref EndPoint remoteEp, ref int errorCode);
        void Close();
    }
}
