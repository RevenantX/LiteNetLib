#if !WINRT && !NETCORE
using System.Security.Cryptography;

namespace LiteNetLib.Encryption
{
    public class NetDesEncryption : NetCryptoProviderBase
    {
        public NetDesEncryption()
            : base(new DESCryptoServiceProvider())
        {}

        public NetDesEncryption(string key)
            : base(new DESCryptoServiceProvider())
        {
            SetKey(key);
        }

        public NetDesEncryption(byte[] data, int offset, int count)
            : base(new DESCryptoServiceProvider())
        {
            SetKey(data, offset, count);
        }
    }
}
#endif