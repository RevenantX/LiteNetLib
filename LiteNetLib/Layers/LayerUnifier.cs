using System.Net;

namespace LiteNetLib.Layers
{
    public static class LayerUnifier
    {
        public static LayerUnifier<TLayer1, TLayer2> Make<TLayer1, TLayer2>(TLayer1 layer1, TLayer2 layer2)
            where TLayer1 : IPacketLayer
            where TLayer2 : IPacketLayer
        {
            return new LayerUnifier<TLayer1, TLayer2>(layer1, layer2);
        }
        public static LayerUnifier<TLayer1, TLayer2, TLayer3> Make<TLayer1, TLayer2, TLayer3>(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3)
            where TLayer1 : IPacketLayer
            where TLayer2 : IPacketLayer
            where TLayer3 : IPacketLayer
        {
            return new LayerUnifier<TLayer1, TLayer2, TLayer3>(layer1, layer2, layer3);
        }
        public static LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4> Make<TLayer1, TLayer2, TLayer3, TLayer4>(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3, TLayer4 layer4)
            where TLayer1 : IPacketLayer
            where TLayer2 : IPacketLayer
            where TLayer3 : IPacketLayer
            where TLayer4 : IPacketLayer
        {
            return new LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4>(layer1, layer2, layer3, layer4);
        }
        public static LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5> Make<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5>(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3, TLayer4 layer4, TLayer5 layer5)
            where TLayer1 : IPacketLayer
            where TLayer2 : IPacketLayer
            where TLayer3 : IPacketLayer
            where TLayer4 : IPacketLayer
            where TLayer5 : IPacketLayer
        {
            return new LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5>(layer1, layer2, layer3, layer4, layer5);
        }
        public static LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5, TLayer6> Make<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5, TLayer6>(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3, TLayer4 layer4, TLayer5 layer5, TLayer6 layer6)
            where TLayer1 : IPacketLayer
            where TLayer2 : IPacketLayer
            where TLayer3 : IPacketLayer
            where TLayer4 : IPacketLayer
            where TLayer5 : IPacketLayer
            where TLayer6 : IPacketLayer
        {
            return new LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5, TLayer6>(layer1, layer2, layer3, layer4, layer5, layer6);
        }
        public static LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5, TLayer6, TLayer7> Make<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5, TLayer6, TLayer7>(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3, TLayer4 layer4, TLayer5 layer5, TLayer6 layer6, TLayer7 layer7)
            where TLayer1 : IPacketLayer
            where TLayer2 : IPacketLayer
            where TLayer3 : IPacketLayer
            where TLayer4 : IPacketLayer
            where TLayer5 : IPacketLayer
            where TLayer6 : IPacketLayer
            where TLayer7 : IPacketLayer
        {
            return new LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5, TLayer6, TLayer7>(layer1, layer2, layer3, layer4, layer5, layer6, layer7);
        }
    }

    public struct LayerUnifier<TLayer1, TLayer2> : IPacketLayer
        where TLayer1 : IPacketLayer
        where TLayer2 : IPacketLayer
    {
        private readonly TLayer1 _layer1;
        private readonly TLayer2 _layer2;
        private readonly int _extraSize;

        public int ExtraPacketSize
        {
            get { return _extraSize; }
        }

        public LayerUnifier(TLayer1 layer1, TLayer2 layer2)
        {
            _extraSize = 0;
            _layer1 = layer1;           
            _extraSize += layer1.ExtraPacketSize;
            _layer2 = layer2;           
            _extraSize += layer2.ExtraPacketSize;
        }
        
        public void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer1.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer2.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
        }

        public void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer2.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer1.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
        }
    }
  
    public struct LayerUnifier<TLayer1, TLayer2, TLayer3> : IPacketLayer
        where TLayer1 : IPacketLayer
        where TLayer2 : IPacketLayer
        where TLayer3 : IPacketLayer
    {
        private readonly TLayer1 _layer1;
        private readonly TLayer2 _layer2;
        private readonly TLayer3 _layer3;
        private readonly int _extraSize;

        public int ExtraPacketSize
        {
            get { return _extraSize; }
        }

        public LayerUnifier(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3)
        {
            _extraSize = 0;
            _layer1 = layer1;           
            _extraSize += layer1.ExtraPacketSize;
            _layer2 = layer2;           
            _extraSize += layer2.ExtraPacketSize;
            _layer3 = layer3;           
            _extraSize += layer3.ExtraPacketSize;
        }
        
        public void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer1.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer2.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer3.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
        }

        public void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer3.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer2.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer1.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
        }
    }
  
    public struct LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4> : IPacketLayer
        where TLayer1 : IPacketLayer
        where TLayer2 : IPacketLayer
        where TLayer3 : IPacketLayer
        where TLayer4 : IPacketLayer
    {
        private readonly TLayer1 _layer1;
        private readonly TLayer2 _layer2;
        private readonly TLayer3 _layer3;
        private readonly TLayer4 _layer4;
        private readonly int _extraSize;

        public int ExtraPacketSize
        {
            get { return _extraSize; }
        }

        public LayerUnifier(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3, TLayer4 layer4)
        {
            _extraSize = 0;
            _layer1 = layer1;           
            _extraSize += layer1.ExtraPacketSize;
            _layer2 = layer2;           
            _extraSize += layer2.ExtraPacketSize;
            _layer3 = layer3;           
            _extraSize += layer3.ExtraPacketSize;
            _layer4 = layer4;           
            _extraSize += layer4.ExtraPacketSize;
        }
        
        public void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer1.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer2.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer3.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer4.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
        }

        public void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer4.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer3.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer2.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer1.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
        }
    }
  
    public struct LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5> : IPacketLayer
        where TLayer1 : IPacketLayer
        where TLayer2 : IPacketLayer
        where TLayer3 : IPacketLayer
        where TLayer4 : IPacketLayer
        where TLayer5 : IPacketLayer
    {
        private readonly TLayer1 _layer1;
        private readonly TLayer2 _layer2;
        private readonly TLayer3 _layer3;
        private readonly TLayer4 _layer4;
        private readonly TLayer5 _layer5;
        private readonly int _extraSize;

        public int ExtraPacketSize
        {
            get { return _extraSize; }
        }

        public LayerUnifier(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3, TLayer4 layer4, TLayer5 layer5)
        {
            _extraSize = 0;
            _layer1 = layer1;           
            _extraSize += layer1.ExtraPacketSize;
            _layer2 = layer2;           
            _extraSize += layer2.ExtraPacketSize;
            _layer3 = layer3;           
            _extraSize += layer3.ExtraPacketSize;
            _layer4 = layer4;           
            _extraSize += layer4.ExtraPacketSize;
            _layer5 = layer5;           
            _extraSize += layer5.ExtraPacketSize;
        }
        
        public void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer1.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer2.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer3.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer4.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer5.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
        }

        public void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer5.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer4.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer3.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer2.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer1.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
        }
    }
  
    public struct LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5, TLayer6> : IPacketLayer
        where TLayer1 : IPacketLayer
        where TLayer2 : IPacketLayer
        where TLayer3 : IPacketLayer
        where TLayer4 : IPacketLayer
        where TLayer5 : IPacketLayer
        where TLayer6 : IPacketLayer
    {
        private readonly TLayer1 _layer1;
        private readonly TLayer2 _layer2;
        private readonly TLayer3 _layer3;
        private readonly TLayer4 _layer4;
        private readonly TLayer5 _layer5;
        private readonly TLayer6 _layer6;
        private readonly int _extraSize;

        public int ExtraPacketSize
        {
            get { return _extraSize; }
        }

        public LayerUnifier(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3, TLayer4 layer4, TLayer5 layer5, TLayer6 layer6)
        {
            _extraSize = 0;
            _layer1 = layer1;           
            _extraSize += layer1.ExtraPacketSize;
            _layer2 = layer2;           
            _extraSize += layer2.ExtraPacketSize;
            _layer3 = layer3;           
            _extraSize += layer3.ExtraPacketSize;
            _layer4 = layer4;           
            _extraSize += layer4.ExtraPacketSize;
            _layer5 = layer5;           
            _extraSize += layer5.ExtraPacketSize;
            _layer6 = layer6;           
            _extraSize += layer6.ExtraPacketSize;
        }
        
        public void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer1.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer2.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer3.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer4.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer5.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer6.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
        }

        public void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer6.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer5.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer4.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer3.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer2.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer1.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
        }
    }
  
    public struct LayerUnifier<TLayer1, TLayer2, TLayer3, TLayer4, TLayer5, TLayer6, TLayer7> : IPacketLayer
        where TLayer1 : IPacketLayer
        where TLayer2 : IPacketLayer
        where TLayer3 : IPacketLayer
        where TLayer4 : IPacketLayer
        where TLayer5 : IPacketLayer
        where TLayer6 : IPacketLayer
        where TLayer7 : IPacketLayer
    {
        private readonly TLayer1 _layer1;
        private readonly TLayer2 _layer2;
        private readonly TLayer3 _layer3;
        private readonly TLayer4 _layer4;
        private readonly TLayer5 _layer5;
        private readonly TLayer6 _layer6;
        private readonly TLayer7 _layer7;
        private readonly int _extraSize;

        public int ExtraPacketSize
        {
            get { return _extraSize; }
        }

        public LayerUnifier(TLayer1 layer1, TLayer2 layer2, TLayer3 layer3, TLayer4 layer4, TLayer5 layer5, TLayer6 layer6, TLayer7 layer7)
        {
            _extraSize = 0;
            _layer1 = layer1;           
            _extraSize += layer1.ExtraPacketSize;
            _layer2 = layer2;           
            _extraSize += layer2.ExtraPacketSize;
            _layer3 = layer3;           
            _extraSize += layer3.ExtraPacketSize;
            _layer4 = layer4;           
            _extraSize += layer4.ExtraPacketSize;
            _layer5 = layer5;           
            _extraSize += layer5.ExtraPacketSize;
            _layer6 = layer6;           
            _extraSize += layer6.ExtraPacketSize;
            _layer7 = layer7;           
            _extraSize += layer7.ExtraPacketSize;
        }
        
        public void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer1.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer2.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer3.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer4.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer5.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer6.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
            _layer7.ProcessOutBoundPacket(endPoint, ref data, ref offset, ref length);
        }

        public void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
        {
            _layer7.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer6.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer5.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer4.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer3.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer2.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
            _layer1.ProcessInboundPacket(endPoint,ref data, ref offset, ref length);
        }
    }
  
}