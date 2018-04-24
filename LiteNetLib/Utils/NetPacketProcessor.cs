using System;
using System.Collections.Generic;

namespace LiteNetLib.Utils
{
    public class NetPacketProcessor
    {
        protected delegate void SubscribeDelegate(NetDataReader reader, object userData);
        private readonly Dictionary<string, ulong> _hashCache = new Dictionary<string, ulong>();
        private readonly char[] _hashBuffer = new char[1024];
        private readonly NetSerializer _netSerializer;
        private readonly Dictionary<ulong, SubscribeDelegate> _callbacks = new Dictionary<ulong, SubscribeDelegate>();
        private readonly NetDataWriter _netDataWriter = new NetDataWriter();

        public NetPacketProcessor()
        {
            _netSerializer = new NetSerializer();
        }

        public NetPacketProcessor(int maxStringLength)
        {
            _netSerializer = new NetSerializer(maxStringLength);
        }

        //FNV-1 64 bit hash
        protected virtual ulong GetHash(Type type)
        {
            ulong hash;
            string typeName = type.FullName;
            if (_hashCache.TryGetValue(typeName, out hash))
            {
                return hash;
            }
            hash = 14695981039346656037UL; //offset
            typeName.CopyTo(0, _hashBuffer, 0, typeName.Length);
            for (var i = 0; i < typeName.Length; i++)
            {
                hash = hash ^ _hashBuffer[i];
                hash *= 1099511628211UL; //prime
            }
            _hashCache.Add(typeName, hash);
            return hash;
        }

        protected virtual SubscribeDelegate GetCallbackFromData(NetDataReader reader)
        {
            var hash = reader.GetULong();
            SubscribeDelegate action;
            if (!_callbacks.TryGetValue(hash, out action))
            {
                throw new ParseException("Undefined packet in NetDataReader");
            }
            return action;
        }

        protected virtual void WriteHash(Type type, NetDataWriter writer)
        {
            writer.Put(GetHash(type));
        }

        /// <summary>
        /// Register nested property type
        /// </summary>
        /// <typeparam name="T">INetSerializable structure</typeparam>
        /// <returns>True - if register successful, false - if type already registered</returns>
        public bool RegisterNestedType<T>() where T : struct, INetSerializable
        {
            return _netSerializer.RegisterNestedType<T>();
        }

        /// <summary>
        /// Register nested property type
        /// </summary>
        /// <param name="writeDelegate"></param>
        /// <param name="readDelegate"></param>
        /// <returns>True - if register successful, false - if type already registered</returns>
        public bool RegisterNestedType<T>(Action<NetDataWriter, T> writeDelegate, Func<NetDataReader, T> readDelegate)
        {
            return _netSerializer.RegisterNestedType<T>(writeDelegate, readDelegate);
        }

        /// <summary>
        /// Register nested property type
        /// </summary>
        /// <typeparam name="T">INetSerializable class</typeparam>
        /// <returns>True - if register successful, false - if type already registered</returns>
        public bool RegisterNestedType<T>(Func<T> constructor) where T : class, INetSerializable
        {
            return _netSerializer.RegisterNestedType(constructor);
        }

        /// <summary>
        /// Reads all available data from NetDataReader and calls OnReceive delegates
        /// </summary>
        /// <param name="reader">NetDataReader with packets data</param>
        public void ReadAllPackets(NetDataReader reader)
        {
            while (reader.AvailableBytes > 0)
            {
                ReadPacket(reader);
            }
        }

        /// <summary>
        /// Reads all available data from NetDataReader and calls OnReceive delegates
        /// </summary>
        /// <param name="reader">NetDataReader with packets data</param>
        /// <param name="userData">Argument that passed to OnReceivedEvent</param>
        /// <exception cref="ParseException">Malformed packet</exception>
        public void ReadAllPackets(NetDataReader reader, object userData)
        {
            while (reader.AvailableBytes > 0)
            {
                ReadPacket(reader, userData);
            }
        }

        /// <summary>
        /// Reads one packet from NetDataReader and calls OnReceive delegate
        /// </summary>
        /// <param name="reader">NetDataReader with packet</param>
        /// <exception cref="ParseException">Malformed packet</exception>
        public void ReadPacket(NetDataReader reader)
        {
            ReadPacket(reader, null);
        }

        public void Send<T>(NetPeer peer, T packet, DeliveryMethod options) where T : class, new()
        {
            _netDataWriter.Reset();
            Write(_netDataWriter, packet);
            peer.Send(_netDataWriter, options);
        }

        public void SendNetSerializable<T>(NetPeer peer, T packet, DeliveryMethod options) where T : INetSerializable
        {
            _netDataWriter.Reset();
            WriteNetSerializable(_netDataWriter, packet);
            peer.Send(_netDataWriter, options);
        }

        public void Send<T>(NetManager manager, T packet, DeliveryMethod options) where T : class, new()
        {
            _netDataWriter.Reset();
            Write(_netDataWriter, packet);
            manager.SendToAll(_netDataWriter, options);
        }

        public void SendNetSerializable<T>(NetManager manager, T packet, DeliveryMethod options) where T : INetSerializable
        {
            _netDataWriter.Reset();
            WriteNetSerializable(_netDataWriter, packet);
            manager.SendToAll(_netDataWriter, options);
        }

        public void Write<T>(NetDataWriter writer, T packet) where T : class, new()
        {
            WriteHash(typeof(T), writer);
            _netSerializer.Serialize(writer, packet);
        }

        public void WriteNetSerializable<T>(NetDataWriter writer, T packet) where T : INetSerializable
        {
            WriteHash(typeof(T), writer);
            packet.Serialize(writer);
        }

        public byte[] Write<T>(T packet) where T : class, new()
        {
            _netDataWriter.Reset();
            WriteHash(typeof(T), _netDataWriter);
            _netSerializer.Serialize(_netDataWriter, packet);
            return _netDataWriter.CopyData();
        }

        public byte[] WriteNetSerializable<T>(T packet) where T : INetSerializable
        {
            _netDataWriter.Reset();
            WriteHash(typeof(T), _netDataWriter);
            packet.Serialize(_netDataWriter);
            return _netDataWriter.CopyData();
        }

        /// <summary>
        /// Reads one packet from NetDataReader and calls OnReceive delegate
        /// </summary>
        /// <param name="reader">NetDataReader with packet</param>
        /// <param name="userData">Argument that passed to OnReceivedEvent</param>
        /// <exception cref="ParseException">Malformed packet</exception>
        public void ReadPacket(NetDataReader reader, object userData)
        {
            GetCallbackFromData(reader)(reader, userData);
        }

        /// <summary>
        /// Register and subscribe to packet receive event
        /// </summary>
        /// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
        /// <param name="packetConstructor">Method that constructs packet intead of slow Activator.CreateInstance</param>
        /// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
        public void Subscribe<T>(Action<T> onReceive, Func<T> packetConstructor) where T : class, new()
        {
            _netSerializer.Register<T>();
            _callbacks[GetHash(typeof(T))] = (reader, userData) =>
            {
                var reference = packetConstructor();
                _netSerializer.Deserialize(reader, reference);
                onReceive(reference);
            };
        }

        /// <summary>
        /// Register and subscribe to packet receive event (with userData)
        /// </summary>
        /// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
        /// <param name="packetConstructor">Method that constructs packet intead of slow Activator.CreateInstance</param>
        /// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
        public void Subscribe<T, TUserData>(Action<T, TUserData> onReceive, Func<T> packetConstructor) where T : class, new()
        {
            _netSerializer.Register<T>();
            _callbacks[GetHash(typeof(T))] = (reader, userData) =>
            {
                var reference = packetConstructor();
                _netSerializer.Deserialize(reader, reference);
                onReceive(reference, (TUserData)userData);
            };
        }

        /// <summary>
        /// Register and subscribe to packet receive event
        /// This metod will overwrite last received packet class on receive (less garbage)
        /// </summary>
        /// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
        /// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
        public void SubscribeReusable<T>(Action<T> onReceive) where T : class, new()
        {
            _netSerializer.Register<T>();
            var reference = new T();
            _callbacks[GetHash(typeof(T))] = (reader, userData) =>
            {
                _netSerializer.Deserialize(reader, reference);
                onReceive(reference);
            };
        }

        /// <summary>
        /// Register and subscribe to packet receive event
        /// This metod will overwrite last received packet class on receive (less garbage)
        /// </summary>
        /// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
        /// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
        public void SubscribeReusable<T, TUserData>(Action<T, TUserData> onReceive) where T : class, new()
        {
            _netSerializer.Register<T>();
            var reference = new T();
            _callbacks[GetHash(typeof(T))] = (reader, userData) =>
            {
                _netSerializer.Deserialize(reader, reference);
                onReceive(reference, (TUserData)userData);
            };
        }

        public void SubscribeNetSerializable<T, TUserData>(
            Action<T, TUserData> onReceive, 
            Func<T> packetConstructor) where T : INetSerializable
        {
            _callbacks[GetHash(typeof(T))] = (reader, userData) =>
            {
                var pkt = packetConstructor();
                pkt.Deserialize(reader);
                onReceive(pkt, (TUserData)userData);
            };
        }

        public void SubscribeNetSerializable<T>(
            Action<T> onReceive,
            Func<T> packetConstructor) where T : INetSerializable
        {
            _callbacks[GetHash(typeof(T))] = (reader, userData) =>
            {
                var pkt = packetConstructor();
                pkt.Deserialize(reader);
                onReceive(pkt);
            };
        }

        public void SubscribeNetSerializable<T, TUserData>(
            Action<T, TUserData> onReceive) where T : INetSerializable, new()
        {
            var reference = new T();
            _callbacks[GetHash(typeof(T))] = (reader, userData) =>
            {
                reference.Deserialize(reader);
                onReceive(reference, (TUserData)userData);
            };
        }

        public void SubscribeNetSerializable<T>(
            Action<T> onReceive) where T : INetSerializable, new()
        {
            var reference = new T();
            _callbacks[GetHash(typeof(T))] = (reader, userData) =>
            {
                reference.Deserialize(reader);
                onReceive(reference);
            };
        }

        /// <summary>
        /// Remove any subscriptions by type
        /// </summary>
        /// <typeparam name="T">Packet type</typeparam>
        /// <returns>true if remove is success</returns>
        public bool RemoveSubscription<T>()
        {
            return _callbacks.Remove(GetHash(typeof(T)));
        }
    }
}
