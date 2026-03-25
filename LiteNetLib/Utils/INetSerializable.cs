namespace LiteNetLib.Utils
{
    /// <summary>
    /// Interface for implementing custom data serialization for network transmission.
    /// </summary>
    /// <remarks>
    /// This is the most efficient way to send complex objects as it avoids reflection.
    /// </remarks>
    public interface INetSerializable
    {
        /// <summary>
        /// Writes the object data into the provided <see cref="NetDataWriter"/>.
        /// </summary>
        /// <param name="writer">The writer to pack data into.</param>
        void Serialize(NetDataWriter writer);

        /// <summary>
        /// Reads the object data from the provided <see cref="NetDataReader"/>.
        /// </summary>
        /// <param name="reader">The reader to extract data from.</param>
        void Deserialize(NetDataReader reader);
    }
}
