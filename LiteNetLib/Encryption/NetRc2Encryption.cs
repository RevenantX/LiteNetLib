using System.Security.Cryptography;

namespace LiteNetLib.Encryption
{
    public class NetRc2Encryption : NetCryptoProviderBase
    {
        public NetRc2Encryption()
            : base(new RC2CryptoServiceProvider())
        {}

        public NetRc2Encryption(string key)
            : base(new RC2CryptoServiceProvider())
        {
            SetKey(key);
        }

        public NetRc2Encryption(byte[] data, int offset, int count)
            : base(new RC2CryptoServiceProvider())
        {
            SetKey(data, offset, count);
        }
    }
}