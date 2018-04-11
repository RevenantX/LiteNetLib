using System;
using System.Reflection;
using System.Collections.Generic;
#if NETCORE
using System.Linq;
#endif

namespace LiteNetLib.Utils
{
    public sealed class NetSerializer
    {
        private sealed class NestedType
        {
            public readonly NestedTypeWriter WriteDelegate;
            public readonly NestedTypeReader ReadDelegate;

            public NestedType(NestedTypeWriter writeDelegate, NestedTypeReader readDelegate)
            {
                WriteDelegate = writeDelegate;
                ReadDelegate = readDelegate;
            }
        }

        private delegate void NestedTypeWriter(NetDataWriter writer, object customObj);
        private delegate object NestedTypeReader(NetDataReader reader);

        private sealed class StructInfo
        {
            public readonly Action<NetDataWriter>[] WriteDelegate;
            public readonly Action<NetDataReader>[] ReadDelegate;
            public object Reference;
            private readonly int _membersCount;

            public StructInfo(int membersCount)
            {
                _membersCount = membersCount;
                WriteDelegate = new Action<NetDataWriter>[membersCount];
                ReadDelegate = new Action<NetDataReader>[membersCount];
            }

            public void Write(NetDataWriter writer, object obj)
            {
                Reference = obj;
                for (int i = 0; i < _membersCount; i++)
                {
                    WriteDelegate[i](writer);
                }
            }

            public void Read(NetDataReader reader)
            {
                for (int i = 0; i < _membersCount; i++)
                {
                    ReadDelegate[i](reader);
                }
            }
        }

        private static readonly HashSet<Type> BasicTypes = new HashSet<Type>
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
            typeof(bool),
            typeof(char)
        };

        private readonly NetDataWriter _writer;
        private readonly int _maxStringLength;
        private readonly Dictionary<string, StructInfo> _registeredTypes;
        private readonly Dictionary<Type, NestedType> _registeredNestedTypes;

        public NetSerializer() : this(0)
        {
            
        }

        public NetSerializer(int maxStringLength)
        {
            _maxStringLength = maxStringLength;
            _registeredTypes = new Dictionary<string, StructInfo>();
            _registeredNestedTypes = new Dictionary<Type, NestedType>();
            _writer = new NetDataWriter();
        }

        private bool RegisterNestedTypeInternal<T>(Func<T> constructor) where T : INetSerializable
        {
            var t = typeof(T);
            if (_registeredNestedTypes.ContainsKey(t))
            {
                return false;
            }

            var rwDelegates = new NestedType(
                (writer, obj) =>
                {
                    ((T)obj).Serialize(writer);
                },
                reader =>
                {
                    var instance = constructor();
                    instance.Deserialize(reader);
                    return instance;
                });
            _registeredNestedTypes.Add(t, rwDelegates);
            return true;
        }

        /// <summary>
        /// Register nested property type
        /// </summary>
        /// <typeparam name="T">INetSerializable structure</typeparam>
        /// <returns>True - if register successful, false - if type already registered</returns>
        public bool RegisterNestedType<T>() where T : struct, INetSerializable
        {
            return RegisterNestedTypeInternal(() => new T());
        }

        /// <summary>
        /// Register nested property type
        /// </summary>
        /// <typeparam name="T">INetSerializable class</typeparam>
        /// <returns>True - if register successful, false - if type already registered</returns>
        public bool RegisterNestedType<T>(Func<T> constructor) where T : class, INetSerializable
        {
            return RegisterNestedTypeInternal(constructor);
        }

        /// <summary>
        /// Register nested property type
        /// </summary>
        /// <param name="writeDelegate"></param>
        /// <param name="readDelegate"></param>
        /// <returns>True - if register successful, false - if type already registered</returns>
        public bool RegisterNestedType<T>(Action<NetDataWriter, T> writeDelegate, Func<NetDataReader, T> readDelegate)
        {
            var t = typeof(T);
            if (BasicTypes.Contains(t) || _registeredNestedTypes.ContainsKey(t))
            {
                return false;
            }

            var rwDelegates = new NestedType(
                (writer, obj) => writeDelegate(writer, (T)obj),
                reader => readDelegate(reader));

            _registeredNestedTypes.Add(t, rwDelegates);
            return true;
        }

        private static Delegate CreateDelegate(Type type, MethodInfo info)
        {
#if NETCORE
            return info.CreateDelegate(type);
#else
            return Delegate.CreateDelegate(type, info);
#endif
        }

        private static Func<TClass, TProperty> ExtractGetDelegate<TClass, TProperty>(MethodInfo info)
        {
            return (Func<TClass, TProperty>)CreateDelegate(typeof(Func<TClass, TProperty>), info);
        }

        private static Action<TClass, TProperty> ExtractSetDelegate<TClass, TProperty>(MethodInfo info)
        {
            return (Action<TClass, TProperty>)CreateDelegate(typeof(Action<TClass, TProperty>), info);
        }

        private StructInfo RegisterInternal<T>()
        {
            Type t = typeof(T);
            string typeName = t.FullName;
            StructInfo info;
            if (_registeredTypes.TryGetValue(typeName, out info))
            {
                return info;
            }

#if NETCORE
            var props = t.GetRuntimeProperties().ToArray();
#else
            var props = t.GetProperties(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.GetProperty |
                BindingFlags.SetProperty);
#endif
            int propsCount = props.Length;
            if (props == null || propsCount == 0)
            {
                throw new InvalidTypeException("Type does not contain acceptable fields");
            }

            info = new StructInfo(propsCount);
            for (int i = 0; i < propsCount; i++)
            {
                var property = props[i];
                var propertyType = property.PropertyType;

#if NETCORE
                bool isEnum = propertyType.GetTypeInfo().IsEnum;
                var getMethod = property.GetMethod;
                var setMethod = property.SetMethod;
#else
                bool isEnum = propertyType.IsEnum;
                var getMethod = property.GetGetMethod();
                var setMethod = property.GetSetMethod();
#endif
                if (isEnum)
                {
                    var underlyingType = Enum.GetUnderlyingType(propertyType);
                    if (underlyingType == typeof(byte))
                    {
                        info.ReadDelegate[i] = reader =>
                        {
                            property.SetValue(info.Reference, Enum.ToObject(propertyType, reader.GetByte()), null);
                        };
                        info.WriteDelegate[i] = writer =>
                        {
                            writer.Put((byte)property.GetValue(info.Reference, null));
                        };
                    }
                    else if (underlyingType == typeof(int))
                    {
                        info.ReadDelegate[i] = reader =>
                        {
                            property.SetValue(info.Reference, Enum.ToObject(propertyType, reader.GetInt()), null);
                        };
                        info.WriteDelegate[i] = writer =>
                        {
                            writer.Put((int)property.GetValue(info.Reference, null));
                        };
                    }
                    else
                    {
                        throw new InvalidTypeException("Not supported enum underlying type: " + underlyingType.Name);
                    }
                }
                else if (propertyType == typeof(string))
                {
                    var setDelegate = ExtractSetDelegate<T, string>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, string>(getMethod);
                    if (_maxStringLength <= 0)
                    {
                        info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetString());
                        info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                    }
                    else
                    {
                        info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetString(_maxStringLength));
                        info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference), _maxStringLength);
                    }
                }
                else if (propertyType == typeof(bool))
                {
                    var setDelegate = ExtractSetDelegate<T, bool>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, bool>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetBool());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
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
                else if (propertyType == typeof(char))
                {
                    var setDelegate = ExtractSetDelegate<T, char>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, char>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetChar());
                    info.WriteDelegate[i] = writer => writer.Put(getDelegate((T)info.Reference));
                }
                // Array types
                else if (propertyType == typeof(string[]))
                {
                    var setDelegate = ExtractSetDelegate<T, string[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, string[]>(getMethod);
                    if (_maxStringLength <= 0)
                    {
                        info.ReadDelegate[i] =
                            reader => setDelegate((T) info.Reference, reader.GetStringArray());
                        info.WriteDelegate[i] =
                            writer => writer.PutArray(getDelegate((T) info.Reference));
                    }
                    else
                    {
                        info.ReadDelegate[i] =
                            reader => setDelegate((T)info.Reference, reader.GetStringArray(_maxStringLength));
                        info.WriteDelegate[i] =
                            writer => writer.PutArray(getDelegate((T)info.Reference), _maxStringLength);
                    }
                }
                else if (propertyType == typeof(byte[]))
                {
                    var setDelegate = ExtractSetDelegate<T, byte[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, byte[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetBytesWithLength());
                    info.WriteDelegate[i] = writer => writer.PutBytesWithLength(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(short[]))
                {
                    var setDelegate = ExtractSetDelegate<T, short[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, short[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetShortArray());
                    info.WriteDelegate[i] = writer => writer.PutArray(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(ushort[]))
                {
                    var setDelegate = ExtractSetDelegate<T, ushort[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ushort[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetUShortArray());
                    info.WriteDelegate[i] = writer => writer.PutArray(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(int[]))
                {
                    var setDelegate = ExtractSetDelegate<T, int[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, int[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetIntArray());
                    info.WriteDelegate[i] = writer => writer.PutArray(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(uint[]))
                {
                    var setDelegate = ExtractSetDelegate<T, uint[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, uint[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetUIntArray());
                    info.WriteDelegate[i] = writer => writer.PutArray(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(long[]))
                {
                    var setDelegate = ExtractSetDelegate<T, long[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, long[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetLongArray());
                    info.WriteDelegate[i] = writer => writer.PutArray(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(ulong[]))
                {
                    var setDelegate = ExtractSetDelegate<T, ulong[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ulong[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetULongArray());
                    info.WriteDelegate[i] = writer => writer.PutArray(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(float[]))
                {
                    var setDelegate = ExtractSetDelegate<T, float[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, float[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetFloatArray());
                    info.WriteDelegate[i] = writer => writer.PutArray(getDelegate((T)info.Reference));
                }
                else if (propertyType == typeof(double[]))
                {
                    var setDelegate = ExtractSetDelegate<T, double[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, double[]>(getMethod);
                    info.ReadDelegate[i] = reader => setDelegate((T)info.Reference, reader.GetDoubleArray());
                    info.WriteDelegate[i] = writer => writer.PutArray(getDelegate((T)info.Reference));
                }
                else
                {
                    NestedType registeredNestedType;
                    bool array = false;

                    if (propertyType.IsArray)
                    {
                        array = true;
                        propertyType = propertyType.GetElementType();
                    }

                    if (_registeredNestedTypes.TryGetValue(propertyType, out registeredNestedType))
                    {
                        if (array) //Array type serialize/deserialize
                        {
                            info.ReadDelegate[i] = reader =>
                            {
                                ushort arrLength = reader.GetUShort();
                                Array arr = Array.CreateInstance(propertyType, arrLength);
                                for (int k = 0; k < arrLength; k++)
                                {
                                    arr.SetValue(registeredNestedType.ReadDelegate(reader), k);
                                }

                                property.SetValue(info.Reference, arr, null);
                            };

                            info.WriteDelegate[i] = writer =>
                            {
                                Array arr = (Array)property.GetValue(info.Reference, null);
                                writer.Put((ushort)arr.Length);
                                for (int k = 0; k < arr.Length; k++)
                                {
                                    registeredNestedType.WriteDelegate(writer, arr.GetValue(k));
                                }
                            };
                        }
                        else //Simple
                        {
                            info.ReadDelegate[i] = reader =>
                            {
                                property.SetValue(info.Reference, registeredNestedType.ReadDelegate(reader), null);
                            };

                            info.WriteDelegate[i] = writer =>
                            {
                                registeredNestedType.WriteDelegate(writer, property.GetValue(info.Reference, null));
                            };
                        }
                    }
                    else
                    {
                        throw new InvalidTypeException("Unknown property type: " + propertyType.FullName);
                    }
                }
            }
            _registeredTypes.Add(typeName, info);

            return info;
        }

        /// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
        public void Register<T>()
        {
            RegisterInternal<T>();
        }

        /// <summary>
        /// Reads packet with known type
        /// </summary>
        /// <param name="reader">NetDataReader with packet</param>
        /// <returns>Returns packet if packet in reader is matched type</returns>
        /// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
        public T Deserialize<T>(NetDataReader reader) where T : class, new()
        {
            var info = RegisterInternal<T>();
            info.Reference = new T();
            try
            {
                info.Read(reader);
            }
            catch
            {
                return null;
            }
            return (T)info.Reference;
        }

        /// <summary>
        /// Reads packet with known type (non alloc variant)
        /// </summary>
        /// <param name="reader">NetDataReader with packet</param>
        /// <param name="target">Deserialization target</param>
        /// <returns>Returns true if packet in reader is matched type</returns>
        /// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
        public bool Deserialize<T>(NetDataReader reader, T target) where T : class, new()
        {
            var info = RegisterInternal<T>();
            info.Reference = target;
            try
            {
                info.Read(reader);
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Serialize struct to NetDataWriter (fast)
        /// </summary>
        /// <param name="writer">Serialization target NetDataWriter</param>
        /// <param name="obj">Object to serialize</param>
        /// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
        public void Serialize<T>(NetDataWriter writer, T obj) where T : class, new()
        {
            RegisterInternal<T>().Write(writer, obj);
        }

        /// <summary>
        /// Serialize struct to byte array
        /// </summary>
        /// <param name="obj">Object to serialize</param>
        /// <returns>byte array with serialized data</returns>
        public byte[] Serialize<T>(T obj) where T : class, new()
        {
            _writer.Reset();
            Serialize(_writer, obj);
            return _writer.CopyData();
        }
    }
}
