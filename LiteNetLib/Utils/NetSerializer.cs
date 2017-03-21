using System;
using System.Reflection;
using System.Collections.Generic;

namespace LiteNetLib.Utils
{
    public abstract class NetSerializerHasher
    {
        public abstract ulong GetHash(string type);
        public abstract void WriteHash(string type, NetDataWriter writer);
        public abstract ulong ReadHash(NetDataReader reader);
    }

    public sealed class FNVHasher : NetSerializerHasher
    {
        private readonly Dictionary<string, ulong> _hashCache = new Dictionary<string, ulong>();
        private readonly char[] _hashBuffer = new char[1024];

        public override ulong GetHash(string type)
        {
            ulong hash;
            if (_hashCache.TryGetValue(type, out hash))
            {
                return hash;
            }
            hash = 14695981039346656037UL; //offset
            int len = type.Length;
            type.CopyTo(0, _hashBuffer, 0, len);
            for (var i = 0; i < len; i++)
            {
                hash = hash ^ _hashBuffer[i];
                hash *= 1099511628211UL; //prime
            }
            _hashCache.Add(type, hash);
            return hash;
        }

        public override ulong ReadHash(NetDataReader reader)
        {
            return reader.GetULong();
        }

        public override void WriteHash(string type, NetDataWriter writer)
        {
            writer.Put(GetHash(type));
        }
    }

    public sealed class NetSerializer
    {
        private sealed class RWDelegates
        {
            public readonly CustomTypeWrite WriteDelegate;
            public readonly CustomTypeRead ReadDelegate;

            public RWDelegates(CustomTypeWrite writeDelegate, CustomTypeRead readDelegate)
            {
                WriteDelegate = writeDelegate;
                ReadDelegate = readDelegate;
            }
        }

        private delegate void CustomTypeWrite(NetDataWriter writer, object customObj);
        private delegate object CustomTypeRead(NetDataReader reader);

        private sealed class StructInfo
        {
            public readonly Action<NetDataWriter>[] WriteDelegate;
            public readonly Action<NetDataReader>[] ReadDelegate;
            public object Reference;
            public Func<object> CreatorFunc;
            public Action<object> OnReceive;

            public StructInfo(int membersCount)
            {
                WriteDelegate = new Action<NetDataWriter>[membersCount];
                ReadDelegate = new Action<NetDataReader>[membersCount];
            }
        }

        private readonly Dictionary<ulong, StructInfo> _cache;
        private readonly Dictionary<Type, RWDelegates> _registeredCustomTypes;
        private readonly HashSet<Type> _acceptedTypes;
        private readonly NetDataWriter _writer;
        private readonly NetSerializerHasher _hasher;
        private const int MaxStringLenght = 1024;

        public NetSerializer() : this(new FNVHasher())
        {
        }

        public NetSerializer(NetSerializerHasher hasher)
        {
            _hasher = hasher;
            _cache = new Dictionary<ulong, StructInfo>();
            _registeredCustomTypes = new Dictionary<Type, RWDelegates>();
            _writer = new NetDataWriter();
            _acceptedTypes = new HashSet<Type>
            {
                typeof(int),
                typeof(uint),
                typeof(byte),
                typeof(sbyte),
                typeof(short),
                typeof(ushort),
                typeof(long),
                typeof(ulong),
                typeof(string),
                typeof(float),
                typeof(double),
                typeof(int[]),
                typeof(uint[]),
                typeof(byte[]),
                typeof(short[]),
                typeof(ushort[]),
                typeof(long[]),
                typeof(ulong[]),
                typeof(string[]),
                typeof(float[]),
                typeof(double[])
            };
        }

        private static Func<TClass, TProperty> ExtractGetDelegate<TClass, TProperty>(MethodInfo info)
        {
#if WINRT || NETCORE
            return (Func<TClass, TProperty>)info.CreateDelegate(typeof(Func<TClass, TProperty>));
#else
            return (Func<TClass, TProperty>)Delegate.CreateDelegate(typeof(Func<TClass, TProperty>), info);
#endif
        }

        private static Action<TClass, TProperty> ExtractSetDelegate<TClass, TProperty>(MethodInfo info)
        {
#if WINRT || NETCORE
            return (Action<TClass, TProperty>)info.CreateDelegate(typeof(Action<TClass, TProperty>));
#else
            return (Action<TClass, TProperty>)Delegate.CreateDelegate(typeof(Action<TClass, TProperty>), info);
#endif
        }

        /// <summary>
        /// Register custom property type
        /// </summary>
        /// <param name="writeDelegate"></param>
        /// <param name="readDelegate"></param>
        public void RegisterCustomType<T>(Action<NetDataWriter, T> writeDelegate, Func<NetDataReader, T> readDelegate) 
        {
            var t = typeof(T);
            if(_acceptedTypes.Contains(t))
            {
                return;
            }

            var rwDelegates = new RWDelegates(
                (writer, obj) => writeDelegate(writer, (T) obj),
                reader => readDelegate(reader));

            _registeredCustomTypes.Add(t, rwDelegates);
            _acceptedTypes.Add(t);
        }

        private StructInfo Register<T>(Type t, ulong nameHash) where T : class 
        {
            StructInfo info;
            if (_cache.TryGetValue(nameHash, out info))
            {
                return info;
            }

#if WINRT || NETCORE
            var props = t.GetRuntimeProperties();
#else
            var props = t.GetProperties(
                BindingFlags.Instance | 
                BindingFlags.Public | 
                BindingFlags.GetProperty | 
                BindingFlags.SetProperty);
#endif
            List<PropertyInfo> accepted = new List<PropertyInfo>();
            foreach(var prop in props)
            {
                if (_acceptedTypes.Contains(prop.PropertyType) ||
                   (prop.PropertyType.IsArray && _registeredCustomTypes.ContainsKey(prop.PropertyType.GetElementType())))
                {
                    accepted.Add(prop);
                }
            }
            if(accepted.Count < 0)
            {
                throw new ArgumentException("Type does not contain acceptable fields");
            }

            info = new StructInfo(accepted.Count);

            for(int i = 0; i < accepted.Count; i++)
            {
                var property = accepted[i];
                var propertyType = property.PropertyType;
#if WINRT || NETCORE
                var getMethod = property.GetMethod;
                var setMethod = property.SetMethod;
#else
                var getMethod = property.GetGetMethod();
                var setMethod = property.GetSetMethod();
#endif
                if (propertyType == typeof(string))
                {
                    var setDelegate = ExtractSetDelegate<T, string>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, string>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetString(MaxStringLenght));
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference), MaxStringLenght);
                }
                else if (propertyType == typeof(byte))
                {
                    var setDelegate = ExtractSetDelegate<T, byte>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, byte>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetByte());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(sbyte))
                {
                    var setDelegate = ExtractSetDelegate<T, sbyte>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, sbyte>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetSByte());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(short))
                {
                    var setDelegate = ExtractSetDelegate<T, short>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, short>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetShort());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(ushort))
                {
                    var setDelegate = ExtractSetDelegate<T, ushort>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ushort>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetUShort());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(int))
                {
                    var setDelegate = ExtractSetDelegate<T, int>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, int>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetInt());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(uint))
                {
                    var setDelegate = ExtractSetDelegate<T, uint>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, uint>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetUInt());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(long))
                {
                    var setDelegate = ExtractSetDelegate<T, long>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, long>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetLong());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(ulong))
                {
                    var setDelegate = ExtractSetDelegate<T, ulong>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ulong>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetULong());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(float))
                {
                    var setDelegate = ExtractSetDelegate<T, float>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, float>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetFloat());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(double))
                {
                    var setDelegate = ExtractSetDelegate<T, double>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, double>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetDouble());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                // Array types
                else if (propertyType == typeof(string[]))
                {
                    var setDelegate = ExtractSetDelegate<T, string[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, string[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetStringArray(MaxStringLenght));
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference), MaxStringLenght);
                }
                else if (propertyType == typeof(byte[]))
                {
                    var setDelegate = ExtractSetDelegate<T, byte[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, byte[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetBytes());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(short[]))
                {
                    var setDelegate = ExtractSetDelegate<T, short[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, short[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetShortArray());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(ushort[]))
                {
                    var setDelegate = ExtractSetDelegate<T, ushort[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ushort[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetUShortArray());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(int[]))
                {
                    var setDelegate = ExtractSetDelegate<T, int[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, int[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetIntArray());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(uint[]))
                {
                    var setDelegate = ExtractSetDelegate<T, uint[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, uint[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetUIntArray());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(long[]))
                {
                    var setDelegate = ExtractSetDelegate<T, long[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, long[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetLongArray());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(ulong[]))
                {
                    var setDelegate = ExtractSetDelegate<T, ulong[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ulong[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetULongArray());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(float[]))
                {
                    var setDelegate = ExtractSetDelegate<T, float[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, float[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetFloatArray());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(double[]))
                {
                    var setDelegate = ExtractSetDelegate<T, double[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, double[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetDoubleArray());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                else
                {
                    RWDelegates registeredCustomType;
                    bool array = false;

                    if (propertyType.IsArray)
                    {
                        array = true;
                        propertyType = propertyType.GetElementType();
                    }

                    if (_registeredCustomTypes.TryGetValue(propertyType, out registeredCustomType))
                    {
                        if (array) //Array type serialize/deserialize
                        {
                            info.ReadDelegate[i] = reader =>
                            { 
                                ushort arrLength = reader.GetUShort();
                                Array arr = Array.CreateInstance(propertyType, arrLength);
                                for (int k = 0; k < arrLength; k++)
                                {
                                    arr.SetValue(registeredCustomType.ReadDelegate(reader), k);
                                }

                                property.SetValue(info.Reference, arr, null);
                            };

                            info.WriteDelegate[i] = writer =>
                            {
                                Array arr = (Array)property.GetValue(info.Reference, null);
                                writer.Put((ushort)arr.Length);
                                for (int k = 0; k < arr.Length; k++)
                                {
                                    registeredCustomType.WriteDelegate(writer, arr.GetValue(k));
                                }
                            };
                        }
                        else //Simple
                        {
                            info.ReadDelegate[i] = reader =>
                            {
                                property.SetValue(info.Reference, registeredCustomType.ReadDelegate(reader), null);
                            };

                            info.WriteDelegate[i] = writer =>
                            {
                                registeredCustomType.WriteDelegate(writer, property.GetValue(info.Reference, null));
                            };
                        }

                    }
                    else
                    {
                        throw new ArgumentException("Unregistered argument type: " + propertyType);
                    }
                }
            }
            _cache.Add(nameHash, info);

            return info;
        }

        /// <summary>
        /// Reads all available data from NetDataReader and calls OnReceive delegates
        /// </summary>
        /// <param name="reader">NetDataReader with packets data</param>
        /// /// <param name="reusePacket">Reuse packet will overwrite last received packet class (less garbage)</param>
        public void ReadAllPackets(NetDataReader reader, bool reusePacket)
        {
            while (reader.AvailableBytes > 0)
            {
                ReadPacket(reader, reusePacket);
            }
        }

        /// <summary>
        /// Reads one packet from NetDataReader and calls OnReceive delegate
        /// </summary>
        /// <param name="reader">NetDataReader with packet</param>
        /// <param name="reusePacket">Reuse packet will overwrite last received packet class (less garbage)</param>
        public void ReadPacket(NetDataReader reader, bool reusePacket)
        {
            ulong name = _hasher.ReadHash(reader);
            var info = _cache[name];

            if (!reusePacket || info.Reference == null)
            {
                info.Reference = info.CreatorFunc();
            }

            for(int i = 0; i < info.ReadDelegate.Length; i++)
            {
                info.ReadDelegate[i](reader);
            }

            if(info.OnReceive != null)
            {
                info.OnReceive(info.Reference);
            }
        }

        /// <summary>
        /// Just register class for nested support
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void Register<T>() where T : class, new()
        {
            var t = typeof(T);
            Register<T>(t, _hasher.GetHash(t.Name));
        }

        /// <summary>
        /// Register and subscribe to packet receive event
        /// </summary>
        /// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
        public void Subscribe<T>(Action<T> onReceive) where T : class, new()
        {
            Subscribe(onReceive, null);
        }

        /// <summary>
        /// Register and subscribe to packet receive event
        /// </summary>
        /// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
        /// <param name="packetConstructor">Method that constructs packet intead of slow Activator.CreateInstance</param>
        public void Subscribe<T>(Action<T> onReceive, Func<T> packetConstructor) where T : class, new()
        {
            var t = typeof(T);
            var info = Register<T>(t, _hasher.GetHash(t.Name));
            if (packetConstructor == null)
            {
                info.CreatorFunc = Activator.CreateInstance<T>;
            }
            else
            {
                info.CreatorFunc = () => packetConstructor();
            }
            info.OnReceive = o => { onReceive((T)o); };
        }

        /// <summary>
        /// Serialize struct to NetDataWriter (fast)
        /// </summary>
        /// <param name="writer">Serialization target NetDataWriter</param>
        /// <param name="obj">Struct to serialize</param>
        public void Serialize<T>(NetDataWriter writer, T obj) where T : class, new()
        {
            Type t = typeof(T);
            ulong nameHash = _hasher.GetHash(t.Name);
            var structInfo = Register<T>(t, nameHash);
            var wd = structInfo.WriteDelegate;
            var wdlen = wd.Length;
            structInfo.Reference = obj;
            _hasher.WriteHash(t.Name, writer);
            for (int i = 0; i < wdlen; i++)
            {
                wd[i](writer);
            }
        }

        /// <summary>
        /// Serialize struct to byte array
        /// </summary>
        /// <param name="obj">Struct to serialize</param>
        /// <returns>byte array with serialized data</returns>
        public byte[] Serialize<T>(T obj) where T : class, new()
        {
            _writer.Reset();
            Serialize(_writer, obj);
            return _writer.CopyData();
        }
    }
}
