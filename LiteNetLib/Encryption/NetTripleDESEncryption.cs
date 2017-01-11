using System.Security.Cryptography;

namespace LiteNetLib.Encryption
{
    public class NetTripleDESEncryption : NetCryptoProviderBase
    {
        public NetTripleDESEncryption(NetPeer peer)
            : base(new TripleDESCryptoServiceProvider())
        {}

        public NetTripleDESEncryption(NetPeer peer, string key)
            : base(new TripleDESCryptoServiceProvider())
        {
            SetKey(key);
        }

        public NetTripleDESEncryption(NetPeer peer, byte[] data, int offset, int count)
            : base(new TripleDESCryptoServiceProvider())
        {
            SetKey(data, offset, count);
        }
    }
}