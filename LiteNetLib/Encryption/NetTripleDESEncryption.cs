#if !WINRT

using System.Security.Cryptography;

namespace LiteNetLib.Encryption
{
    public class NetTripleDESEncryption : NetCryptoProviderBase
    {
        public NetTripleDESEncryption()
            : base(TripleDES.Create())
        {}

        public NetTripleDESEncryption(string key)
            : base(TripleDES.Create())
        {
            SetKey(key);
        }

        public NetTripleDESEncryption(byte[] data, int offset, int count)
            : base(TripleDES.Create())
        {
            SetKey(data, offset, count);
        }
    }
}
#endif