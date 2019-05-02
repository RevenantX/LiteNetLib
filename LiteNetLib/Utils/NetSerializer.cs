using System;
using System.Reflection;
using System.Collections.Generic;
using System.Net;

#if NETSTANDARD2_0 || NETCOREAPP2_0
using System.Linq;
#endif

namespace LiteNetLib.Utils
{
    public class InvalidTypeException : ArgumentException
    {
        public InvalidTypeException()
        {
        }

        public InvalidTypeException(string message) : base(message)
        {
        }

        public InvalidTypeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public InvalidTypeException(string message, string paramName) : base(message, paramName)
        {
        }

        public InvalidTypeException(string message, string paramName, Exception innerException) : base(message, paramName, innerException)
        {
        }
    }

    public class ParseException : Exception
    {
        public ParseException()
        {
        }

        public ParseException(string message) : base(message)
        {
        }

        public ParseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
    
    public sealed class NetSerializer
    {
        private sealed class NestedType
        {
            public readonly NestedTypeWriter WriteDelegate;
            public readonly NestedTypeReader ReadDelegate;
            public readonly NestedTypeWriter ArrayWriter;
            public readonly NestedTypeReader ArrayReader;

            public NestedType(NestedTypeWriter writeDelegate, NestedTypeReader readDelegate, NestedTypeWriter arrayWriter, NestedTypeReader arrayReader)
            {
                WriteDelegate = writeDelegate;
                ReadDelegate = readDelegate;
                ArrayWriter = arrayWriter;
                ArrayReader = arrayReader;
            }
        }

        private delegate void NestedTypeWriter(NetDataWriter writer, object customObj);
        private delegate object NestedTypeReader(NetDataReader reader);

        private sealed class ClassInfo<T>
        {
            public static ClassInfo<T> Instance;
            public readonly Action<T, NetDataWriter>[] WriteDelegate;
            public readonly Action<T, NetDataReader>[] ReadDelegate;
            private readonly int _membersCount;

            public ClassInfo(int membersCount)
            {
                _membersCount = membersCount;
                WriteDelegate = new Action<T, NetDataWriter>[membersCount];
                ReadDelegate = new Action<T, NetDataReader>[membersCount];
            }

            public void Write(T obj, NetDataWriter writer)
            {
                for (int i = 0; i < _membersCount; i++)
                    WriteDelegate[i](obj, writer);
            }

            public void Read(T obj, NetDataReader reader)
            {
                for (int i = 0; i < _membersCount; i++)
                    ReadDelegate[i](obj, reader);
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
            typeof(char),
            typeof(IPEndPoint)
        };

        private readonly NetDataWriter _writer;
        private readonly int _maxStringLength;
        private readonly Dictionary<Type, NestedType> _registeredNestedTypes;

        public NetSerializer() : this(0)
        {
            
        }

        public NetSerializer(int maxStringLength)
        {
            _maxStringLength = maxStringLength;
            _registeredNestedTypes = new Dictionary<Type, NestedType>();
            _writer = new NetDataWriter();
        }

        private bool RegisterNestedTypeInternal<T>(Func<T> constructor) where T : INetSerializable
        {
            var t = typeof(T);
            if (_registeredNestedTypes.ContainsKey(t))
                return false;
            NestedType nestedType;
            NestedTypeWriter nestedTypeWriter = (writer, obj) => ((T) obj).Serialize(writer);
            NestedTypeWriter nestedTypeArrayWriter = (writer, arr) =>
            {
                var typedArr = (T[]) arr;
                writer.Put((ushort) typedArr.Length);
                for (int i = 0; i < typedArr.Length; i++)
                    typedArr[i].Serialize(writer);
            };

            //struct
            if (constructor == null)
            {
                nestedType = new NestedType(
                    nestedTypeWriter,
                    reader =>
                    {
                        var instance = default(T);
                        instance.Deserialize(reader);
                        return instance;
                    },
                    nestedTypeArrayWriter,
                    reader =>
                    {
                        var typedArr = new T[reader.GetUShort()];
                        for (int i = 0; i < typedArr.Length; i++)
                            typedArr[i].Deserialize(reader);
                        return typedArr;
                    });
            }
            else //class
            {
                nestedType = new NestedType(
                    nestedTypeWriter,
                    reader =>
                    {
                        var instance = constructor();
                        instance.Deserialize(reader);
                        return instance;
                    },
                    nestedTypeArrayWriter,
                    reader =>
                    {
                        var typedArr = new T[reader.GetUShort()];
                        for (int i = 0; i < typedArr.Length; i++)
                        {
                            typedArr[i] = constructor();
                            typedArr[i].Deserialize(reader);
                        }
                        return typedArr;
                    });
            }
            _registeredNestedTypes.Add(t, nestedType);
            return true;
        }

        /// <summary>
        /// Register nested property type
        /// </summary>
        /// <typeparam name="T">INetSerializable structure</typeparam>
        /// <returns>True - if register successful, false - if type already registered</returns>
        public bool RegisterNestedType<T>() where T : struct, INetSerializable
        {
            return RegisterNestedTypeInternal<T>(null);
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
                return false;

            var rwDelegates = new NestedType(
                (writer, obj) => writeDelegate(writer, (T)obj),
                reader => readDelegate(reader),
                (writer, arr) =>
                {
                    var typedArr = (T[])arr;
                    writer.Put((ushort)typedArr.Length);
                    for (int i = 0; i < typedArr.Length; i++)
                        writeDelegate(writer, typedArr[i]);
                },
                reader =>
                {
                    var typedArr = new T[reader.GetUShort()];
                    for (int i = 0; i < typedArr.Length; i++)
                        typedArr[i] = readDelegate(reader);
                    return typedArr;
                });

            _registeredNestedTypes.Add(t, rwDelegates);
            return true;
        }

        private static Delegate CreateDelegate(Type type, MethodInfo info)
        {
#if NETSTANDARD2_0 || NETCOREAPP2_0
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

        private ClassInfo<T> RegisterInternal<T>()
        {
            if (ClassInfo<T>.Instance != null)
                return ClassInfo<T>.Instance;

            Type t = typeof(T);
#if NETSTANDARD2_0 || NETCOREAPP2_0
            var props = t.GetRuntimeProperties().ToArray();
#else
            var props = t.GetProperties(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.GetProperty |
                BindingFlags.SetProperty);
#endif
            int propsCount = props.Length;
            if (props == null)
                throw new InvalidTypeException("Type does not contain acceptable fields");

            var info = new ClassInfo<T>(propsCount);
            for (int i = 0; i < propsCount; i++)
            {
                var property = props[i];
                var propertyType = property.PropertyType;

#if NETSTANDARD2_0 || NETCOREAPP2_0
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
                        info.ReadDelegate[i] = (inf, r) =>
                        {
                            property.SetValue(inf, Enum.ToObject(propertyType, r.GetByte()), null);
                        };
                        info.WriteDelegate[i] = (inf, w) =>
                        {
                            w.Put((byte)property.GetValue(inf, null));
                        };
                    }
                    else if (underlyingType == typeof(int))
                    {
                        info.ReadDelegate[i] = (inf, r) =>
                        {
                            property.SetValue(inf, Enum.ToObject(propertyType, r.GetInt()), null);
                        };
                        info.WriteDelegate[i] = (inf, w) =>
                        {
                            w.Put((int)property.GetValue(inf, null));
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
                        info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetString());
                        info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                    }
                    else
                    {
                        info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetString(_maxStringLength));
                        info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf), _maxStringLength);
                    }
                }
                else if (propertyType == typeof(bool))
                {
                    var setDelegate = ExtractSetDelegate<T, bool>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, bool>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetBool());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(byte))
                {
                    var setDelegate = ExtractSetDelegate<T, byte>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, byte>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetByte());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(sbyte))
                {
                    var setDelegate = ExtractSetDelegate<T, sbyte>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, sbyte>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetSByte());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(short))
                {
                    var setDelegate = ExtractSetDelegate<T, short>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, short>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetShort());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(ushort))
                {
                    var setDelegate = ExtractSetDelegate<T, ushort>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ushort>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetUShort());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(int))
                {
                    var setDelegate = ExtractSetDelegate<T, int>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, int>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetInt());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(uint))
                {
                    var setDelegate = ExtractSetDelegate<T, uint>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, uint>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetUInt());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(long))
                {
                    var setDelegate = ExtractSetDelegate<T, long>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, long>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetLong());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(ulong))
                {
                    var setDelegate = ExtractSetDelegate<T, ulong>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ulong>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetULong());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(float))
                {
                    var setDelegate = ExtractSetDelegate<T, float>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, float>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetFloat());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(double))
                {
                    var setDelegate = ExtractSetDelegate<T, double>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, double>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetDouble());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(char))
                {
                    var setDelegate = ExtractSetDelegate<T, char>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, char>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetChar());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                else if (propertyType == typeof(IPEndPoint))
                {
                    var setDelegate = ExtractSetDelegate<T, IPEndPoint>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, IPEndPoint>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetNetEndPoint());
                    info.WriteDelegate[i] = (inf, w) => w.Put(getDelegate(inf));
                }
                // Array types
                else if (propertyType == typeof(string[]))
                {
                    var setDelegate = ExtractSetDelegate<T, string[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, string[]>(getMethod);
                    if (_maxStringLength <= 0)
                    {
                        info.ReadDelegate[i] = (inf, r) => setDelegate( inf, r.GetStringArray());
                        info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate( inf));
                    }
                    else
                    {
                        info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetStringArray(_maxStringLength));
                        info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf), _maxStringLength);
                    }
                }
                else if (propertyType == typeof(bool[]))
                {
                    var setDelegate = ExtractSetDelegate<T, bool[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, bool[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetBoolArray());
                    info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf));
                }
                else if (propertyType == typeof(byte[]))
                {
                    var setDelegate = ExtractSetDelegate<T, byte[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, byte[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetBytesWithLength());
                    info.WriteDelegate[i] = (inf, w) => w.PutBytesWithLength(getDelegate(inf));
                }
                else if (propertyType == typeof(short[]))
                {
                    var setDelegate = ExtractSetDelegate<T, short[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, short[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetShortArray());
                    info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf));
                }
                else if (propertyType == typeof(ushort[]))
                {
                    var setDelegate = ExtractSetDelegate<T, ushort[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ushort[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetUShortArray());
                    info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf));
                }
                else if (propertyType == typeof(int[]))
                {
                    var setDelegate = ExtractSetDelegate<T, int[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, int[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetIntArray());
                    info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf));
                }
                else if (propertyType == typeof(uint[]))
                {
                    var setDelegate = ExtractSetDelegate<T, uint[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, uint[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetUIntArray());
                    info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf));
                }
                else if (propertyType == typeof(long[]))
                {
                    var setDelegate = ExtractSetDelegate<T, long[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, long[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetLongArray());
                    info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf));
                }
                else if (propertyType == typeof(ulong[]))
                {
                    var setDelegate = ExtractSetDelegate<T, ulong[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, ulong[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetULongArray());
                    info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf));
                }
                else if (propertyType == typeof(float[]))
                {
                    var setDelegate = ExtractSetDelegate<T, float[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, float[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetFloatArray());
                    info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf));
                }
                else if (propertyType == typeof(double[]))
                {
                    var setDelegate = ExtractSetDelegate<T, double[]>(setMethod);
                    var getDelegate = ExtractGetDelegate<T, double[]>(getMethod);
                    info.ReadDelegate[i] = (inf, r) => setDelegate(inf, r.GetDoubleArray());
                    info.WriteDelegate[i] = (inf, w) => w.PutArray(getDelegate(inf));
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
                            info.ReadDelegate[i] = (inf, r) => property.SetValue(inf, registeredNestedType.ArrayReader(r), null);
                            info.WriteDelegate[i] = (inf, w) => registeredNestedType.ArrayWriter(w, property.GetValue(inf, null));
                        }
                        else //Simple
                        {
                            info.ReadDelegate[i] = (inf, r) => property.SetValue(inf, registeredNestedType.ReadDelegate(r), null);
                            info.WriteDelegate[i] = (inf, w) => registeredNestedType.WriteDelegate(w, property.GetValue(inf, null));
                        }
                    }
                    else
                    {
                        throw new InvalidTypeException("Unknown property type: " + propertyType.FullName);
                    }
                }
            }
            ClassInfo<T>.Instance = info;
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
            var result = new T();
            try
            {
                info.Read(result, reader);
            }
            catch
            {
                return null;
            }
            return result;
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
            try
            {
                info.Read(target, reader);
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
            RegisterInternal<T>().Write(obj, writer);
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
