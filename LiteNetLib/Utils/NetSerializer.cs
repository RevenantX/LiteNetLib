using System;
using System.Reflection;
using System.Collections.Generic;

namespace LiteNetLib.Utils
{
    public sealed class NetSerializer
    {
        private class RWDelegates
        {
            public CustomTypeWrite WriteDelegate;
            public CustomTypeRead ReadDelegate;
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
            public override ValueType GetStruct()
            {
                return Structure;
            }
        }

        private class StructInfo
        {
            public Action[] WriteDelegate;
            public Action<NetDataReader>[] ReadDelegate;
            public AbstractStructRefrence StructReference;
            public Action<ValueType> OnReceive;
        }

        private readonly Dictionary<ulong, StructInfo> _cache;
        private readonly Dictionary<Type, RWDelegates> _registeredCustomTypes;
        private readonly char[] _hashBuffer = new char[1024];
        private readonly HashSet<Type> _acceptedTypes;
        private readonly NetDataWriter _writer;
        private const int MaxStringLenght = 1024;

        public NetSerializer()
        {
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
                typeof(double)
            };
        }

        private ulong HashStr(string str)
        {
            str.CopyTo(0, _hashBuffer, 0, str.Length);
            ulong hash = 14695981039346656037UL; //offset
            for (var i = 0; i < str.Length; i++)
            {
                hash = hash ^ _hashBuffer[i];
                hash *= 1099511628211UL; //prime
            }
            return hash;
        }

        private delegate TProperty GetGenericDelegate<TStruct, out TProperty>(ref TStruct obj) where TStruct : struct;
        private delegate void SetGenericDelegate<TStruct, in TProperty>(ref TStruct obj, TProperty property) where TStruct : struct;

        private GetGenericDelegate<TStruct, TProperty> ExtractGetDelegate<TStruct, TProperty>(MethodInfo info) where TStruct : struct
        {
#if WINRT || NETCORE
            return (Func<TStruct, TProperty>)info.CreateDelegate(typeof(Func<TStruct, TProperty>));
#else
            return (GetGenericDelegate<TStruct, TProperty>)Delegate.CreateDelegate(typeof(GetGenericDelegate<TStruct, TProperty>), info);
#endif
        }

        private SetGenericDelegate<TStruct, TProperty> ExtractSetDelegate<TStruct, TProperty>(MethodInfo info) where TStruct : struct
        {
#if WINRT || NETCORE
            return (Action<TStruct, T>)info.CreateDelegate(typeof(Action<TStruct, TProperty>));
#else
            return (SetGenericDelegate<TStruct, TProperty>)Delegate.CreateDelegate(typeof(SetGenericDelegate<TStruct, TProperty>), info);
#endif
        }

        private void MakeDelegate<T>(Type t, MethodInfo getMethod, MethodInfo setMethod, StructInfo info, int idx) where T : struct
        {
            RWDelegates registeredCustomType;
            StructReference<T> sref = (StructReference<T>)info.StructReference;

            if (t == typeof(string))
            {
                var setDelegate = ExtractSetDelegate<T, string>(setMethod);
                var getDelegate = ExtractGetDelegate<T, string>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetString(MaxStringLenght)); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure), MaxStringLenght); };
            }
            else if (t == typeof(byte))
            {
                var setDelegate = ExtractSetDelegate<T, byte>(setMethod);
                var getDelegate = ExtractGetDelegate<T, byte>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetByte()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (t == typeof(sbyte))
            {
                var setDelegate = ExtractSetDelegate<T, sbyte>(setMethod);
                var getDelegate = ExtractGetDelegate<T, sbyte>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetSByte()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (t == typeof(short))
            {
                var setDelegate = ExtractSetDelegate<T, short>(setMethod);
                var getDelegate = ExtractGetDelegate<T, short>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetShort()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (t == typeof(ushort))
            {
                var setDelegate = ExtractSetDelegate<T, ushort>(setMethod);
                var getDelegate = ExtractGetDelegate<T, ushort>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetUShort()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (t == typeof(int))
            {
                var setDelegate = ExtractSetDelegate<T, int>(setMethod);
                var getDelegate = ExtractGetDelegate<T, int>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetInt()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (t == typeof(uint))
            {
                var setDelegate = ExtractSetDelegate<T, uint>(setMethod);
                var getDelegate = ExtractGetDelegate<T, uint>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetUInt()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (t == typeof(long))
            {
                var setDelegate = ExtractSetDelegate<T, long>(setMethod);
                var getDelegate = ExtractGetDelegate<T, long>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetLong()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (t == typeof(ulong))
            {
                var setDelegate = ExtractSetDelegate<T, ulong>(setMethod);
                var getDelegate = ExtractGetDelegate<T, ulong>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetULong()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (t == typeof(float))
            {
                var setDelegate = ExtractSetDelegate<T, float>(setMethod);
                var getDelegate = ExtractGetDelegate<T, float>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetFloat()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (t == typeof(double))
            {
                var setDelegate = ExtractSetDelegate<T, double>(setMethod);
                var getDelegate = ExtractGetDelegate<T, double>(getMethod);
                info.ReadDelegate[idx] = reader => { setDelegate(ref sref.Structure, reader.GetDouble()); };
                info.WriteDelegate[idx] = () => { _writer.Put(getDelegate(ref sref.Structure)); };
            }
            else if (_registeredCustomTypes.TryGetValue(t, out registeredCustomType))
            {
                var setDelegate = ExtractSetDelegate<T, object>(setMethod);
                var getDelegate = ExtractGetDelegate<T, object>(getMethod);
                info.ReadDelegate[idx] = reader => setDelegate(ref sref.Structure, registeredCustomType.ReadDelegate(reader));
                info.WriteDelegate[idx] = () => registeredCustomType.WriteDelegate(_writer, getDelegate(ref sref.Structure));
            }
            else
            {
                throw new ArgumentException("Unregistered argument type: " + t);
            }
        }

        public void RegisterCustomTypeProcessor<T>(CustomTypeWrite writeDelegate, CustomTypeRead readDelegate) where T : struct
        {
            var t = typeof(T);
            if(_acceptedTypes.Contains(t))
            {
                return;
            }
            _registeredCustomTypes.Add(t, new RWDelegates { ReadDelegate = readDelegate, WriteDelegate = writeDelegate });
            _acceptedTypes.Add(t);
        }

        private StructInfo Register<T>(Type t, ulong name) where T : struct
        {
            StructInfo info;
            if (_cache.TryGetValue(name, out info))
            {
                return info;
            }

#if WINRT || NETCORE
            var props = t.GetRuntimeProperties();
#else
            var props = t.GetProperties();
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

            info = new StructInfo
            {
                StructReference = new StructReference<T> { Structure = Activator.CreateInstance<T>() },
                WriteDelegate = new Action[accepted.Count],
                ReadDelegate = new Action<NetDataReader>[accepted.Count]
            };

            _cache[name] = info;
            for(int i = 0; i < accepted.Count; i++)
            {
#if WINRT || NETCORE
                var getMethod = accepted[i].GetMethod;
                var setMethod = accepted[i].SetMethod;
#else
                var getMethod = accepted[i].GetGetMethod();
                var setMethod = accepted[i].GetSetMethod();
#endif
                MakeDelegate<T>( accepted[i].PropertyType, getMethod, setMethod, info, i);
            }
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
                info.OnReceive(info.StructReference.GetStruct());
            }
        }

        public void Subscribe<T>(Action<T> onReceive) where T : struct
        {
            var t = typeof(T);
            var info = Register<T>(t, HashStr(t.Name));
            info.OnReceive = o => { onReceive((T)o); };
        }

        public byte[] Serialize<T>(T obj) where T : struct
        {
            _writer.Reset();

            Type t = typeof(T);
            ulong nameHash = HashStr(t.Name);
            var structInfo = Register<T>(t, nameHash);
            var wd = structInfo.WriteDelegate;
            var wdlen = structInfo.WriteDelegate.Length;
            var sref = (StructReference<T>)structInfo.StructReference;
            sref.Structure = obj;

            _writer.Put(nameHash);
            for (int i = 0; i < wdlen; i++)
            {
                wd[i]();
            }

            return _writer.CopyData();
        }
    }
}
