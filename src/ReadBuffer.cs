using System;
using System.Runtime.InteropServices;
using Zene.Audio;
using Zene.Structs;

namespace lvt
{
    public class ReadBuffer : IAudioSource
    {
        public ReadBuffer(int size, int offset)
        {
            _readPointer = 0;
            _writePointer = offset;
            _writeCount = (ulong)offset;
            _size = size;
            _datablock = new float[size];
        }
        
        private float[] _datablock;
        private double _readPointer;
        private int _writePointer;
        private int _size;
        
        private double _readCount;
        private ulong _writeCount;
        
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
            for (int i = 0; i < floats.Length; i++)
            {
                _datablock[_writePointer] = floats[i];
                _writePointer++;
                _writeCount++;
                if (_writePointer >= _size)
                {
                    _writePointer = 0;
                }
            }
        }

        public Vector2 GetSample(double time)
        {
            if (_readCount >= _writeCount) { return 0d; }
            
            if (Stereo)
            {
                Span<Vector2<float>> vector = MemoryMarshal.Cast<float, Vector2<float>>(_datablock);
                Vector2 v = Get(vector, _readPointer / 2d);
                _readPointer += _scale * 2;
                _readCount += _scale * 2;
                if (_readPointer >= _size)
                {
                    _readPointer -= _size;
                }
                
                return v;
            }
            
            float vM = Get(_datablock, _readPointer);
            _readPointer += _scale;
            _readCount += _scale * 2;
            if (_readPointer >= _size)
            {
                _readPointer -= _size;
            }
            return vM;
        }
        
        private static float Get(ReadOnlySpan<float> data, double index)
        {
            int i1 = (int)Math.Floor(index);
            int i2 = (int)Math.Ceiling(index) % data.Length;
            
            double blend = index - i1;
            
            float a = data[i1];
            float b = data[i2];
            
            return a.Lerp(b, (float)blend);
        }
        private static Vector2 Get(ReadOnlySpan<Vector2<float>> data, double index)
        {
            int i1 = (int)Math.Floor(index);
            int i2 = (int)Math.Ceiling(index) % data.Length;
            
            double blend = index - i1;
            
            Vector2 a = (Vector2)data[i1];
            Vector2 b = (Vector2)data[i2];
            
            return a.Lerp(b, blend);
        }
    }
}