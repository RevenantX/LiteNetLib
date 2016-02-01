namespace LiteNetLib
{
    public class NtpSyncModule
    {
        private readonly NetBase _netBase;
        private readonly NetSocket _socket;

        internal NtpSyncModule(NetBase netBase, NetSocket socket)
        {
            _netBase = netBase;
            _socket = socket;
        }
    }
}
