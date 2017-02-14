using System;
using System.Reflection;
using System.Collections.Generic;

namespace LiteNetLib.Utils
{
    public sealed class NetSerializer
    {
        private class RWDelegates
        {
            public readonly CustomTypeWrite WriteDelegate;
            public readonly CustomTypeRead ReadDelegate;

            public RWDelegates(CustomTypeWrite writeDelegate, CustomTypeRead readDelegate)
            {
                WriteDelegate = writeDelegate;
                ReadDelegate = readDelegate;
            }
        }

        public delegate void CustomTypeWrite(NetDataWriter writer, object customObj);
        public delegate object CustomTypeRead(NetDataReader reader);

        private abstract class AbstractStructRefrence
        {
            public abstract ValueType GetStruct();
        }

        private class StructReference<T> : AbstractStructRefrence where T : struct
        {
            public T Structure;

            public StructReference()
            {
                Structure = default(T);
            }

            public override ValueType GetStruct()
            {
                return Structure;
            }
        }

        private class StructInfo
        {
            public readonly Action<NetDataWriter>[] WriteDelegate;
            public readonly Action<NetDataReader>[] ReadDelegate;
            public AbstractStructRefrence Reference;
            public Action<ValueType> OnReceive;

            public StructInfo(int membersCount)
            {
                WriteDelegate = new Action<NetDataWriter>[membersCount];
                ReadDelegate = new Action<NetDataReader>[membersCount];
            }
        }

        private readonly Dictionary<ulong, StructInfo> _cache;
        private readonly Dictionary<string, ulong> _hashCache;
        private readonly Dictionary<Type, RWDelegates> _registeredCustomTypes;
        private readonly char[] _hashBuffer = new char[1024];
        private readonly HashSet<Type> _acceptedTypes;
        private readonly NetDataWriter _writer;
        private const int MaxStringLenght = 1024;

        public NetSerializer()
        {
            _cache = new Dictionary<ulong, StructInfo>();
            _hashCache = new Dictionary<string, ulong>();
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

        private ulong HashStr(string str)
        {
            ulong hash;
            if (_hashCache.TryGetValue(str, out hash))
            {
                return hash;
            }
            hash = 14695981039346656037UL; //offset
            int len = str.Length;
            str.CopyTo(0, _hashBuffer, 0, len);      
            for (var i = 0; i < len; i++)
            {
                hash = hash ^ _hashBuffer[i];
                hash *= 1099511628211UL; //prime
            }
            _hashCache.Add(str, hash);
            return hash;
        }

        private delegate TProperty GetMethodDelegate<TStruct, out TProperty>(ref TStruct obj) where TStruct : struct;
        private delegate void SetMethodDelegate<TStruct, in TProperty>(ref TStruct obj, TProperty property) where TStruct : struct;

        private GetMethodDelegate<TStruct, TProperty> ExtractGetDelegate<TStruct, TProperty>(MethodInfo info) where TStruct : struct
        {
#if WINRT || NETCORE
            return (GetMethodDelegate<TStruct, TProperty>)info.CreateDelegate(typeof(GetMethodDelegate<TStruct, TProperty>));
#else
            return (GetMethodDelegate<TStruct, TProperty>)Delegate.CreateDelegate(typeof(GetMethodDelegate<TStruct, TProperty>), info);
#endif
        }

        private SetMethodDelegate<TStruct, TProperty> ExtractSetDelegate<TStruct, TProperty>(MethodInfo info) where TStruct : struct
        {
#if WINRT || NETCORE
            return (SetMethodDelegate<TStruct, TProperty>)info.CreateDelegate(typeof(SetMethodDelegate<TStruct, TProperty>));
#else
            return (SetMethodDelegate<TStruct, TProperty>)Delegate.CreateDelegate(typeof(SetMethodDelegate<TStruct, TProperty>), info);
#endif
        }

        private void MakeDelegate<T>(
            PropertyInfo propInfo,
            MethodInfo getMethod, 
            MethodInfo setMethod, 
            StructInfo info, 
            int idx,
            StructReference<T> sref) where T : struct
        {
            var propertyType = propInfo.PropertyType;

            if (propertyType == typeof(string))
            {
                var setDelegate = ExtractSetDelegate<T, string>(setMethod);
                var getDelegate = ExtractGetDelegate<T, string>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetString(MaxStringLenght));
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure), MaxStringLenght);
            }
            else if (propertyType == typeof(byte))
            {
                var setDelegate = ExtractSetDelegate<T, byte>(setMethod);
                var getDelegate = ExtractGetDelegate<T, byte>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetByte());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(sbyte))
            {
                var setDelegate = ExtractSetDelegate<T, sbyte>(setMethod);
                var getDelegate = ExtractGetDelegate<T, sbyte>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetSByte());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(short))
            {
                var setDelegate = ExtractSetDelegate<T, short>(setMethod);
                var getDelegate = ExtractGetDelegate<T, short>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetShort());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(ushort))
            {
                var setDelegate = ExtractSetDelegate<T, ushort>(setMethod);
                var getDelegate = ExtractGetDelegate<T, ushort>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetUShort());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(int))
            {
                var setDelegate = ExtractSetDelegate<T, int>(setMethod);
                var getDelegate = ExtractGetDelegate<T, int>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetInt());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(uint))
            {
                var setDelegate = ExtractSetDelegate<T, uint>(setMethod);
                var getDelegate = ExtractGetDelegate<T, uint>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetUInt());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(long))
            {
                var setDelegate = ExtractSetDelegate<T, long>(setMethod);
                var getDelegate = ExtractGetDelegate<T, long>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetLong());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(ulong))
            {
                var setDelegate = ExtractSetDelegate<T, ulong>(setMethod);
                var getDelegate = ExtractGetDelegate<T, ulong>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetULong());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(float))
            {
                var setDelegate = ExtractSetDelegate<T, float>(setMethod);
                var getDelegate = ExtractGetDelegate<T, float>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetFloat());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(double))
            {
                var setDelegate = ExtractSetDelegate<T, double>(setMethod);
                var getDelegate = ExtractGetDelegate<T, double>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetDouble());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            // Array types
            else if (propertyType == typeof(string[]))
            {
                var setDelegate = ExtractSetDelegate<T, string[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, string[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetStringArray(MaxStringLenght));
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure), MaxStringLenght);
            }
            else if (propertyType == typeof(byte[]))
            {
                var setDelegate = ExtractSetDelegate<T, byte[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, byte[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetBytes());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(short[]))
            {
                var setDelegate = ExtractSetDelegate<T, short[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, short[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetShortArray());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(ushort[]))
            {
                var setDelegate = ExtractSetDelegate<T, ushort[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, ushort[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetUShortArray());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(int[]))
            {
                var setDelegate = ExtractSetDelegate<T, int[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, int[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetIntArray());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(uint[]))
            {
                var setDelegate = ExtractSetDelegate<T, uint[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, uint[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetUIntArray());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(long[]))
            {
                var setDelegate = ExtractSetDelegate<T, long[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, long[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetLongArray());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(ulong[]))
            {
                var setDelegate = ExtractSetDelegate<T, ulong[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, ulong[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetULongArray());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(float[]))
            {
                var setDelegate = ExtractSetDelegate<T, float[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, float[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetFloatArray());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else if (propertyType == typeof(double[]))
            {
                var setDelegate = ExtractSetDelegate<T, double[]>(setMethod);
                var getDelegate = ExtractGetDelegate<T, double[]>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, reader.GetDoubleArray());
                info.WriteDelegate[idx] = writer => writer.Put(getDelegate(ref sref.Structure));
            }
            else
            {
                RWDelegates registeredCustomType;
                if (_registeredCustomTypes.TryGetValue(propertyType, out registeredCustomType))
                {
                    info.ReadDelegate[idx] = reader =>
                    {
                        object boxedStruct = sref.Structure;
                        propInfo.SetValue(boxedStruct, registeredCustomType.ReadDelegate(reader), null);
                        sref.Structure = (T)boxedStruct;
                    };

                    info.WriteDelegate[idx] = writer =>
                    {
                        registeredCustomType.WriteDelegate(writer, propInfo.GetValue(sref.Structure, null));
                    };
                }
                else
                {
                    throw new ArgumentException("Unregistered argument type: " + propertyType);
                }
            }
        }

        public void RegisterCustomType<T>(Action<NetDataWriter, T> writeDelegate, Func<NetDataReader, T> readDelegate) where T : struct
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

        private StructInfo Register<T>(Type t, ulong nameHash) where T : struct
        {
            StructInfo info;
            if (_cache.TryGetValue(nameHash, out info))
            {
                return info;
            }

#if WINRT || NETCORE
            var props = t.GetRuntimeProperties();
#else
            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);
#endif
            List<PropertyInfo> accepted = new List<PropertyInfo>();
            foreach(var prop in props)
            {
                var type = prop.PropertyType.IsArray ? prop.PropertyType.GetElementType() : prop.PropertyType;
                if (_acceptedTypes.Contains(type))
                {
                    accepted.Add(prop);
                }
            }
            if(accepted.Count < 0)
            {
                throw new ArgumentException("Type does not contain acceptable fields");
            }

            info = new StructInfo(accepted.Count);
            var structureReference = new StructReference<T>();
            info.Reference = structureReference;

            for(int i = 0; i < accepted.Count; i++)
            {
#if WINRT || NETCORE
                var getMethod = accepted[i].GetMethod;
                var setMethod = accepted[i].SetMethod;
#else
                var getMethod = accepted[i].GetGetMethod();
                var setMethod = accepted[i].GetSetMethod();
#endif
                MakeDelegate( 
                    accepted[i], 
                    getMethod, 
                    setMethod, 
                    info, 
                    i, 
                    structureReference);
            }
            _cache.Add(nameHash, info);

            return info;
        }

        public void ProcessData(NetDataReader reader)
        {
            ulong name = reader.GetULong();
            var info = _cache[name];
            
            for(int i = 0; i < info.ReadDelegate.Length; i++)
            {
                info.ReadDelegate[i](reader);
            }

            if(info.OnReceive != null)
            {
                info.OnReceive(info.Reference.GetStruct());
            }
        }

        public void Subscribe<T>(Action<T> onReceive) where T : struct
        {
            var t = typeof(T);
            var info = Register<T>(t, HashStr(t.Name));
            info.OnReceive = o => { onReceive((T)o); };
        }

        public void Serialize<T>(NetDataWriter writer, T obj) where T : struct 
        {
            Type t = typeof(T);
            ulong nameHash = HashStr(t.Name);
            var structInfo = Register<T>(t, nameHash);
            var wd = structInfo.WriteDelegate;
            var wdlen = structInfo.WriteDelegate.Length;
            var sref = (StructReference<T>)structInfo.Reference;
            sref.Structure = obj;

            writer.Put(nameHash);
            for (int i = 0; i < wdlen; i++)
            {
                wd[i](writer);
            }
        }

        public byte[] Serialize<T>(T obj) where T : struct
        {
            _writer.Reset();
            Serialize(_writer, obj);
            return _writer.CopyData();
        }
    }
}
