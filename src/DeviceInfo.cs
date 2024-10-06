using System;
using System.Runtime.InteropServices;

namespace lvt
{
    public struct DeviceInfo
    {
        public DeviceInfo(long sr, bool s)
        {
            _code = 1;
            SampleRate = sr;
            Stereo = s;
        }

#pragma warning disable CS0414
        private byte _code;
#pragma warning restore CS0414
        public long SampleRate { get; }
        public bool Stereo { get; }
        
        public byte[] GetBytes()
        {
            int size = Marshal.SizeOf(this);
            byte[] arr = new byte[size];

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(this, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return arr;
        }
        public static DeviceInfo FromBytes(byte[] arr)
        {
            DeviceInfo str = new DeviceInfo();

            int size = Marshal.SizeOf(str);
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);

                Marshal.Copy(arr, 0, ptr, size);

                str = (DeviceInfo)Marshal.PtrToStructure(ptr, str.GetType());
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return str;
        }
    }
}