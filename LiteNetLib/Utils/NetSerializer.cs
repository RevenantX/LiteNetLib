using System;
using System.Reflection;
using System.Collections.Generic;

namespace LiteNetLib.Utils
{
    public sealed class NetSerializer
    {
        private class RWDelegates
        {
            public WriteDelegate WriteDelegate;
            public ReadDelegate ReadDelegate;
        }

        private class StructInfo
        {
            public WriteDelegate[] WriteDelegate;
            public ReadDelegate[] ReadDelegate;
            public FieldInfo[] FieldInfos;
            public object Instance;
            public Action<object> OnReceive;
        }

        private Dictionary<ulong, StructInfo> _cache;
        private readonly Dictionary<Type, RWDelegates> _registeredCustomTypes;
        private char[] _hashBuffer = new char[1024];
        private HashSet<Type> _acceptedTypes;
        private NetDataWriter _writer;
        private const int MaxStringLenght = 1024;

        public delegate void WriteDelegate(NetDataWriter writer, object o);
        public delegate object ReadDelegate(NetDataReader reader);

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

        private void MakeDelegate(Type t, out WriteDelegate writeDelegate, out ReadDelegate readDelegate)
        {
            RWDelegates registeredCustomType;

            if (t == typeof(string))
            {
                writeDelegate = (writer, o) => { writer.Put((string)o, MaxStringLenght); };
                readDelegate = reader => reader.GetString(MaxStringLenght);
            }
            else if (t == typeof(byte))
            {
                writeDelegate = (writer, o) => { writer.Put((byte)o); };
                readDelegate = reader => reader.GetByte();
            }
            else if (t == typeof(sbyte))
            {
                writeDelegate = (writer, o) => { writer.Put((sbyte)o); };
                readDelegate = reader => reader.GetSByte();
            }
            else if (t == typeof(short))
            {
                writeDelegate = (writer, o) => { writer.Put((short)o); };
                readDelegate = reader => reader.GetShort();
            }
            else if (t == typeof(ushort))
            {
                writeDelegate = (writer, o) => { writer.Put((ushort)o); };
                readDelegate = reader => reader.GetUShort();
            }
            else if (t == typeof(int))
            {
                writeDelegate = (writer, o) => { writer.Put((int)o); };
                readDelegate = reader => reader.GetInt();
            }
            else if (t == typeof(uint))
            {
                writeDelegate = (writer, o) => { writer.Put((uint)o); };
                readDelegate = reader => reader.GetUInt();
            }
            else if (t == typeof(long))
            {
                writeDelegate = (writer, o) => { writer.Put((long)o); };
                readDelegate = reader => reader.GetLong();
            }
            else if (t == typeof(ulong))
            {
                writeDelegate = (writer, o) => { writer.Put((ulong)o); };
                readDelegate = reader => reader.GetULong();
            }
            else if (t == typeof(float))
            {
                writeDelegate = (writer, o) => { writer.Put((float)o); };
                readDelegate = reader => reader.GetFloat();
            }
            else if (t == typeof(double))
            {
                writeDelegate = (writer, o) => { writer.Put((double)o); };
                readDelegate = reader => reader.GetDouble();
            }
            else if (_registeredCustomTypes.TryGetValue(t, out registeredCustomType))
            {
                writeDelegate = registeredCustomType.WriteDelegate;
                readDelegate = registeredCustomType.ReadDelegate;
            }
            else
            {
                throw new ArgumentException("Unregistered argument type: " + t);
            }
        }

        public void RegisterCustomTypeProcessor<T>(WriteDelegate writeDelegate, ReadDelegate readDelegate)
        {
            RegisterCustomTypeProcessor(typeof(T), writeDelegate, readDelegate);
        }

        public void RegisterCustomTypeProcessor(Type t, WriteDelegate writeDelegate, ReadDelegate readDelegate)
        {
            if(_acceptedTypes.Contains(t))
            {
                return;
            }
            _registeredCustomTypes.Add(t, new RWDelegates { ReadDelegate = readDelegate, WriteDelegate = writeDelegate });
            _acceptedTypes.Add(t);
        }

        private StructInfo Register(Type t, ulong name)
        {
            StructInfo info;
            if (_cache.TryGetValue(name, out info))
            {
                return info;
            }

#if WINRT
            var fields = t.GetRuntimeFields();
#else
            var fields = t.GetFields();
#endif
            List<FieldInfo> acceptedFields = new List<FieldInfo>();
            foreach(var field in fields)
            {
                var type = field.FieldType.IsArray ? field.FieldType.GetElementType() : field.FieldType;
                if (_acceptedTypes.Contains(type))
                {
                    acceptedFields.Add(field);
                }
            }
            if(acceptedFields.Count < 0)
            {
                throw new ArgumentException("Type does not contain acceptable fields");
            }

            info = new StructInfo();
            info.Instance = Activator.CreateInstance(t);
            info.WriteDelegate = new WriteDelegate[acceptedFields.Count];
            info.ReadDelegate = new ReadDelegate[acceptedFields.Count];
            info.FieldInfos = new FieldInfo[acceptedFields.Count];

            _cache[name] = info;
            for(int i = 0; i < acceptedFields.Count; i++)
            {
                info.FieldInfos[i] = acceptedFields[i];
                var fieldType = acceptedFields[i].FieldType;

                if (fieldType.IsArray)
                {
                    WriteDelegate objWrite;
                    ReadDelegate objRead;
                    Type elementType = fieldType.GetElementType();
                    MakeDelegate(elementType, out objWrite, out objRead);
                    info.WriteDelegate[i] = (writer, o) =>
                    {
                        Array arr = (Array)o;
                        writer.Put(arr.Length);
                        for (int idx = 0; idx < arr.Length; idx++)
                        {
                            objWrite(writer, arr.GetValue(idx));
                        }
                    };
                    info.ReadDelegate[i] = reader =>
                    {
                        int elemCount = reader.GetInt();
                        Array arr = Array.CreateInstance(elementType, elemCount);
                        for (int idx = 0; idx < elemCount; idx++)
                        {
                            object value = objRead(reader);
                            arr.SetValue(value, idx);
                        }
                        return arr;
                    };
                }
                else
                {
                    MakeDelegate(fieldType, out info.WriteDelegate[i], out info.ReadDelegate[i]);
                }
            }
            return info;
        }

        public void ProcessData(NetDataReader reader)
        {
            ulong name = reader.GetULong();
            var info = _cache[name];
            
            for(int i = 0; i < info.ReadDelegate.Length; i++)
            {
                info.FieldInfos[i].SetValue(info.Instance, info.ReadDelegate[i](reader));
            }

            if(info.OnReceive != null)
            {
                info.OnReceive(info.Instance);
            }
        }

        public void Subscribe<T>(Action<T> onReceive) where T : struct
        {
            var t = typeof(T);
            var info = Register(t, HashStr(t.Name));
            info.OnReceive = o => { onReceive((T)o); };
        }

        public byte[] Serialize<T>(T obj) where T : struct
        {
            _writer.Reset();

            Type t = typeof(T);
            ulong name = HashStr(t.Name);
            var info = Register(t, name);
            var wd = info.WriteDelegate;
            var fi = info.FieldInfos;

            _writer.Put(name);
            for (int i = 0; i < info.WriteDelegate.Length; i++)
            {
                wd[i](_writer, fi[i].GetValue(obj));
            }

            return _writer.CopyData();
        }
    }
}
