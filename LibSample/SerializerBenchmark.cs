using LiteNetLib.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace LibSample
{
    class SerializerBenchmark : IExample
    {
        const int LoopLength = 100000;

        [Serializable] //Just for test binary formatter
        private struct SampleNetSerializable : INetSerializable
        {
            public int Value;

            public void Serialize(NetDataWriter writer)
            {
                writer.Put(Value);
            }

            public void Deserialize(NetDataReader reader)
            {
                Value = reader.GetInt();
            }
        }

        [Serializable] //Just for test binary formatter
        private class SamplePacket
        {
            public string SomeString { get; set; }
            public float SomeFloat { get; set; }
            public int[] SomeIntArray { get; set; }
            public SomeVector2 SomeVector2 { get; set; }
            public SomeVector2[] SomeVectors { get; set; }
            public string EmptyString { get; set; }
            public SampleNetSerializable TestObj { get; set; }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("SomeString: " + SomeString);
                sb.AppendLine("SomeFloat: " + SomeFloat);
                sb.AppendLine("SomeIntArray: ");
                for (int i = 0; i < SomeIntArray.Length; i++)
                {
                    sb.AppendLine(" " + SomeIntArray[i]);
                }
                sb.AppendLine("SomeVector2 X: " + SomeVector2);
                sb.AppendLine("SomeVectors: ");
                for (int i = 0; i < SomeVectors.Length; i++)
                {
                    sb.AppendLine(" " + SomeVectors[i]);
                }
                sb.AppendLine("EmptyString: " + EmptyString);
                sb.AppendLine("TestObj value: " + TestObj.Value);
                return sb.ToString();
            }
        }

        [Serializable] //Just for test binary formatter
        private struct SomeVector2
        {
            public int X;
            public int Y;

            public SomeVector2(int x, int y)
            {
                X = x;
                Y = y;
            }

            public override string ToString()
            {
                return "X: " + X + ", Y: " + Y;
            }

            public static void Serialize(NetDataWriter writer, SomeVector2 vector)
            {
                writer.Put(vector.X);
                writer.Put(vector.Y);
            }

            public static SomeVector2 Deserialize(NetDataReader reader)
            {
                SomeVector2 res = new SomeVector2();
                res.X = reader.GetInt();
                res.Y = reader.GetInt();
                return res;
            }
        }

        private void NetSerializerTest(NetSerializer serializer, NetDataWriter netDataWriter, Stopwatch stopwatch, SamplePacket samplePacket)
        {
            netDataWriter.Reset();
            stopwatch.Restart();
            for (int i = 0; i < LoopLength; i++)
                serializer.Serialize(netDataWriter, samplePacket);
            stopwatch.Stop();
            Console.WriteLine($"NetSerializer time: {stopwatch.ElapsedMilliseconds} ms, size: { netDataWriter.Length / LoopLength} bytes");
        }

        private void DataWriterTest(NetDataWriter netDataWriter, Stopwatch stopwatch, SamplePacket samplePacket)
        {
            netDataWriter.Reset();
            stopwatch.Restart();
            for (int i = 0; i < LoopLength; i++)
            {
                netDataWriter.Put(samplePacket.SomeString);
                netDataWriter.Put(samplePacket.SomeFloat);
                netDataWriter.PutArray(samplePacket.SomeIntArray);
                SomeVector2.Serialize(netDataWriter, samplePacket.SomeVector2);
                netDataWriter.Put((ushort)samplePacket.SomeVectors.Length);
                for (int j = 0; j < samplePacket.SomeVectors.Length; j++)
                {
                    SomeVector2.Serialize(netDataWriter, samplePacket.SomeVectors[j]);
                }
                netDataWriter.Put(samplePacket.EmptyString);
                netDataWriter.Put(samplePacket.TestObj);
            }
            stopwatch.Stop();
            Console.WriteLine($"Raw time: {stopwatch.ElapsedMilliseconds} ms, size: { netDataWriter.Length / LoopLength} bytes");
        }

        public void Run()
        {
            Console.WriteLine("=== Serializer benchmark ===");
            
            
            //Test serializer performance
            Stopwatch stopwatch = new Stopwatch();
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream();
            NetDataWriter netDataWriter = new NetDataWriter();

            SamplePacket samplePacket = new SamplePacket
            {
                SomeFloat = 0.3f,
                SomeString = "TEST",
                SomeIntArray = new [] { 1, 2, 3 },
                SomeVector2 = new SomeVector2(1, 2),
                SomeVectors = new SomeVector2[20]
            };
            for (int i = 0; i < samplePacket.SomeVectors.Length; i++)
            {
                samplePacket.SomeVectors[i] = new SomeVector2(i, i);
            }

            var netSerializer = new NetSerializer();
            netSerializer.RegisterNestedType<SampleNetSerializable>();
            netSerializer.RegisterNestedType( SomeVector2.Serialize, SomeVector2.Deserialize );

            //Prewarm cpu
            for (int i = 0; i < 10000000; i++)
            {
                double c = Math.Sin(i);
            }

            //Test binary formatter
            stopwatch.Start();
            for (int i = 0; i < LoopLength; i++)
                binaryFormatter.Serialize(memoryStream, samplePacket);
            stopwatch.Stop();
            Console.WriteLine("BinaryFormatter time: " + stopwatch.ElapsedMilliseconds + " ms");
            Console.WriteLine("BinaryFormatter size: " + memoryStream.Position / LoopLength);

            DataWriterTest(netDataWriter, stopwatch, samplePacket);
            NetSerializerTest(netSerializer, netDataWriter, stopwatch, samplePacket);

            DataWriterTest(netDataWriter, stopwatch, samplePacket);
            NetSerializerTest(netSerializer, netDataWriter, stopwatch, samplePacket);

            DataWriterTest(netDataWriter, stopwatch, samplePacket);
            NetSerializerTest(netSerializer, netDataWriter, stopwatch, samplePacket);

            Console.ReadKey();
        }
    }
}
