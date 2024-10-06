using System;
using System.Runtime.InteropServices;
using Zene.Audio;
using Zene.Structs;

namespace lvt
{
    public class ReadBuffer : IAudioSource
    {
        private class Node
        {
            public float[] Data;
            public Node Next;
        }
        
        public ReadBuffer()
        {
            _readPointer = 0;
        }
        
        private Node _front;
        private Node _back;
        private double _readPointer;
        private bool _waiting;
        private int _bufferSize;
        
        public bool Stereo => Info.Stereo;
        
        public DeviceInfo Info;
        public long OurSR
        {
            set
            {
                _scale = Info.SampleRate / (double)value;
            }
        }
        private double _scale;
        
        public void Write(byte[] block)
        {
            Span<float> floats = MemoryMarshal.Cast<byte, float>(block);
            float[] data = new float[floats.Length];
            floats.CopyTo(data);
            Node n = new Node();
            n.Data = data;
            if (_back != null)
            {
                _back.Next = n;
            }
            if (_front == null)
            {
                _front = n;
            }
            _back = n;
            _bufferSize++;
        }

        public Vector2 GetSample(double time)
        {
            if (_front == null || _front.Next == null)
            {
                _waiting = true;
                return 0d;
            }
            if (_waiting)
            {
                if (_bufferSize < 3)
                {
                    return 0d;
                }
                _waiting = false;
            }
            if (_bufferSize > 7)
            {
                while (_bufferSize > 5)
                {
                    _front = _front.Next;
                    _bufferSize--;
                }
            }
            
            if (Stereo)
            {
                Vector2 v = GetV(_front, _readPointer / 2d);
                _readPointer += _scale * 2d;
                if (_readPointer >= _front.Data.Length)
                {
                    _readPointer -= _front.Data.Length;
                    _front = _front.Next;
                    _bufferSize--;
                }
                return v;
            }
            
            float vM = Get(_front, _readPointer);
            _readPointer += _scale;
            if (_readPointer >= _front.Data.Length)
            {
                _readPointer -= _front.Data.Length;
                _front = _front.Next;
                _bufferSize--;
            }
            return vM;
        }
        
        private static float Get(Node data, double index)
        {
            ReadOnlySpan<float> d1 = data.Data;
            int i1 = (int)Math.Floor(index);
            int i2 = (int)Math.Ceiling(index);
            
            ReadOnlySpan<float> d2 = d1;
            if (i2 >= d1.Length)
            {
                d2 = data.Next.Data;
                i2 -= d1.Length;
            }
            
            double blend = index - i1;
            
            float a = d1[i1];
            float b = d2[i2];
            
            return a.Lerp(b, (float)blend);
        }
        private static Vector2 GetV(Node data, double index)
        {
            Span<Vector2<float>> vector1 = MemoryMarshal.Cast<float, Vector2<float>>(data.Data);
            int i1 = (int)Math.Floor(index);
            int i2 = (int)Math.Ceiling(index);
            
            Span<Vector2<float>> vector2 = vector1;
            if (i2 >= vector1.Length)
            {
                vector2 = MemoryMarshal.Cast<float, Vector2<float>>(data.Next.Data);
                i2 -= vector1.Length;
            }
            
            double blend = index - i1;
            
            Vector2 a = new Vector2(vector1[i1].X, vector1[i1].Y);
            Vector2 b = new Vector2(vector2[i2].X, vector2[i2].Y);
            
            return a.Lerp(b, blend);
        }
    }
}