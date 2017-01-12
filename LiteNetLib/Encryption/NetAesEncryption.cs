#if !WINRT
using System.Security.Cryptography;

namespace LiteNetLib.Encryption
{
    public class NetAesEncryption : NetCryptoProviderBase
    {
        public NetAesEncryption()
#if UNITY 
			: base(new RijndaelManaged())
#else
            : base(Aes.Create())
#endif
        {}

        public NetAesEncryption(string key)
#if UNITY
			: base(new RijndaelManaged())
#else
            : base(Aes.Create())
#endif
        {
            SetKey(key);
        }

        public NetAesEncryption(byte[] data, int offset, int count)
#if UNITY
			: base(new RijndaelManaged())
#else
            : base(Aes.Create())
#endif
        {
            SetKey(data, offset, count);
        }
    }
}
#endif