using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
using System.Threading;

using Zene.Audio;
using System.Runtime.InteropServices;
using System.Linq;

// local voice transfer
namespace lvt
{
    class Program
    {
        private const int _port = 11000;
        
        private static UdpClient _udp;
        private static IPEndPoint _ep;
        private static ReadBuffer _rb;
        private static bool _running = true;
        
        private static bool _connection = false;
        private static byte[] _sendKey = Encoding.ASCII.GetBytes("Sending request for VC.");
        private static byte[] _connectKey = Encoding.ASCII.GetBytes("Accept request.");
        
        private static IAsyncResult _ar;
        private static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
            
            _ar = _udp.BeginReceive(FindRequest, null);
            ChooseIP();
            
            AudioSystem sys = new AudioSystem(Devices.DefaultOutput, 1024);
            sys.Gain = 0.5;
            AudioReader read = new AudioReader(Devices.DefaultInput, 1024);
            
            // send audio information
            byte[] info = new DeviceInfo(read.SampleRate, read.Stereo).GetBytes();
            Console.WriteLine("Sending device information.");
            _udp.Send(info, info.Length);
            
            byte[] oi = _udp.Receive(ref _ep);
            DeviceInfo di = DeviceInfo.FromBytes(oi);
            Console.WriteLine("Received device information.");
            
            _rb = new ReadBuffer(1024 * 8, 1024 * 2);
            _rb.Info = di;
            _rb.OurSR = sys.SampleRate;
            read.OnAudio += Send;
            sys.Sources.Add(_rb);
            
            Thread.Sleep(100);
            
            Console.WriteLine("Starting call.");
            
            Task.Run(Receive);
            read.Start();
            sys.Start();
            
            string r = null;
            while (r != "Q")
            {
                r = Console.ReadLine();
                bool dv = double.TryParse(r, out double g);
                if (dv)
                {
                    sys.Gain = g;
                }
            }
            
            sys.Stop();
            read.Stop();
            _running = false;
            _udp.Close();
        }
        
        private static void Send(Span<float> data, uint channels)
        {
            Span<byte> bytes = MemoryMarshal.AsBytes(data);
            byte[] array = bytes.ToArray();
            
            _udp.SendAsync(array, array.Length);
        }
        
        private static void Receive()
        {
            Console.WriteLine("Task setup.");
            
            while (_running)
            {
                byte[] data = _udp.Receive(ref _ep);
                _rb.Write(data);
            }
        }
        
        private static void FindRequest(IAsyncResult ar)
        {
            if (_connection) { return; }
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, _port);
            byte[] check = _udp.EndReceive(ar, ref ep);
            
            if (Enumerable.SequenceEqual(_sendKey, check))
            {
                Console.Write($"Request from {ep.Address} (Y/N): ");
                char k = char.ToUpper(Console.ReadLine()[0]);
                if (k == 'Y')
                {
                    _connection = true;
                    
                    _udp.Close();
                    _udp = new UdpClient();
                    _udp.Connect(ep);
                    
                    _udp.Send(_connectKey, _connectKey.Length);
                    _ep = ep;
                    return;
                }
            }
            
            _ar = _udp.BeginReceive(FindRequest, null);
        }
        private static void ChooseIP()
        {
            IPAddress ip = new IPAddress(GetIP());
            if (_connection) { return; }
            _connection = true;
            _ep = new IPEndPoint(ip, _port);
            // end search
            _udp.Client.Close();
            _ar = null;
            
            _udp = new UdpClient();
            _udp.Connect(_ep);
            
            Console.WriteLine("Sending request.");
            _udp.Send(_sendKey, _sendKey.Length);
            Console.WriteLine("Awaiting response...");
            while (true)
            {
                byte[] confirm = _udp.Receive(ref _ep);
                if (Enumerable.SequenceEqual(_connectKey, confirm))
                {
                    break;
                }
            }
            Console.WriteLine("Request received.");
        }
        private static byte[] GetIP()
        {
            while (true)
            {
                Console.WriteLine("Enter ip address:");
                string ipStr = Console.ReadLine();
                if (_connection) { return new byte[] { 0, 0, 0, 0 }; }
                
                // p1
                int dot = ipStr.IndexOf('.');
                if (dot < 1) { goto Error; }
                
                bool pass = int.TryParse(ipStr.Remove(dot), out int part1);
                if (!pass || part1 < 0 || part1 > 255)
                {
                    goto Error;
                }
                ipStr = ipStr.Remove(0, dot + 1);
                
                // p2
                dot = ipStr.IndexOf('.');
                if (dot < 1) { goto Error; }
                
                pass = int.TryParse(ipStr.Remove(dot), out int part2);
                if (!pass || part2 < 0 || part2 > 255)
                {
                    goto Error;
                }
                ipStr = ipStr.Remove(0, dot + 1);
                
                // p3
                dot = ipStr.IndexOf('.');
                if (dot < 1) { goto Error; }
                
                pass = int.TryParse(ipStr.Remove(dot), out int part3);
                if (!pass || part3 < 0 || part3 > 255)
                {
                    goto Error;
                }
                ipStr = ipStr.Remove(0, dot + 1);
                
                // p4
                pass = int.TryParse(ipStr, out int part4);
                if (!pass || part4 < 0 || part4 > 255)
                {
                    goto Error;
                }
                
                return new byte[] { (byte)part1, (byte)part2, (byte)part3, (byte)part4 };
                
            Error:
                Console.WriteLine("Invalid ip address.");
            }
        }
    }
}
