using System.Text;

namespace LiteNetLib.Encryption
{
    /// <summary>
    ///     Interface for an encryption algorithm
    /// </summary>
    public abstract class NetEncryption
    {
        /// <summary>
        ///     Decrypt an incoming message in place
        /// </summary>
        public abstract bool Decrypt(byte[] rawData, int start, ref int length);

        /// <summary>
        ///     Encrypt an outgoing message in place
        /// </summary>
        public abstract bool Encrypt(byte[] rawData, ref int start, ref int length);

        public void SetKey(string str)
        {
#if WINRT
            var bytes = Encoding.Unicode.GetBytes(str);
#else
            var bytes = Encoding.ASCII.GetBytes(str);
#endif
            SetKey(bytes, 0, bytes.Length);
        }

        public abstract void SetKey(byte[] data, int offset, int count);
    }
}