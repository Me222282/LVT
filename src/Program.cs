using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
using System.Threading;

using Zene.Audio;
using System.Runtime.InteropServices;
using System.Linq;
using Zene.GUI;
using Zene.Windowing;

// local voice transfer
namespace lvt
{
    class Program : GUIWindow
    {
        private const int _port = 11000;
        
        private static byte[] _sendKey = Encoding.ASCII.GetBytes("Sending request for VC.");
        private static byte[] _connectKey = Encoding.ASCII.GetBytes("Accept request.");
        private static byte[] _goAwayKey = Encoding.ASCII.GetBytes("Decline request.");
        static void Main(string[] args)
        {
            Core.Init();
            
            Program p = new Program(800, 500, "drgdrg");
            p.Run();
            p.Dispose();
            
            Core.Terminate();
        }
        
        public Program(int width, int height, string title)
            : base(width, height, title)
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
            
            _console = new ConsoleElement(new Layout(0.5, 0d, 0.95, 1.95));
            _console.TextSize = 12d;
            AddChild(_console);
            
            _left = new Container(new Layout(-0.5, 0d, 0.95, 1.95));
            _left.LayoutManager = new BlockLayout(10d);
            AddChild(_left);
            
            _enter = new TextInput(new TextLayout(5d, 0d, (1.6, 0d)));
            _enter.TextSize = 20d;
            _enter.SingleLine = true;
            _left.AddChild(_enter);
            
            _rb = new ReadBuffer();
            
            _sys = new AudioSystem(Devices.DefaultOutput, 1024);
            _sys.Gain = 0.5;
            _read = new AudioReader(Devices.DefaultInput, 1024);
            _read.OnAudio += Send;
            _sys.Sources.Add(_rb);
            
            RootElement.Focus = _enter;
        }
        
        private ConsoleElement _console;
        private Container _left;
        private TextInput _enter;
        
        private UdpClient _udp;
        private IPEndPoint _ep = new IPEndPoint(IPAddress.Any, _port);
        private ReadBuffer _rb;
        
        private bool _location = false;
        private bool _connection = false;
        private bool _sendingData = false;
        
        private AudioSystem _sys;
        private AudioReader _read;
        
        protected override void OnStart(EventArgs e)
        {
            base.OnStart(e);
            
            Task.Run(Receive);
        }
        protected override void OnStop(EventArgs e)
        {
            base.OnStop(e);
            
            _read.Stop();
            _sys.Stop();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            // send request
            if (e[Keys.Enter] && _enter.Focused)
            {
                bool valid = GetIP(_enter.Text);
                if (!valid)
                {
                    _enter.Text = "";
                    return;
                }
                
                _left.RemoveChild(_enter);
                
                if (_location) { return; }
                _location = true;
                
                _console.WriteLine("Sending request.");
                _udp.Send(_sendKey, _sendKey.Length, _ep);
                _console.WriteLine("Awaiting response...");
            }
        }
        
        private void ResolveRequest(IPAddress address, bool accept)
        {   
            IPEndPoint ep = new IPEndPoint(address, _port);
            
            if (!accept)
            {
                _udp.Send(_goAwayKey, _connectKey.Length, ep);
                return;
            }
            
            if (_location)
            {
                _console.WriteLine("Connection already made.");
                return;
            }
            _left.RemoveChild(_enter);
            
            _location = true;
            
            _udp.Send(_connectKey, _connectKey.Length, ep);
            _ep = ep;
            
            _connection = true;
            Task.Run(StartCall);
        }
        private void StartCall()
        {   
            byte[] info = new DeviceInfo(_read.SampleRate, _read.Stereo).GetBytes();
            _console.WriteLine("Exchanging device information.");
            _udp.Send(info, info.Length, _ep);
            
            // byte[] oi = _udp.Receive(ref _ep);
            // DeviceInfo di = DeviceInfo.FromBytes(oi);
            // _console.WriteLine("Device information exchanged.");
            // _rb.Info = di;
            // _rb.OurSR = _sys.SampleRate;
            
            while (!_sendingData)
            {
                Thread.Sleep(10);
            }
            
            // Thread.Sleep(100);
            
            _console.WriteLine("Starting call.");
            
            _read.Start();
            _sys.Start();
        }
        private void Receive()
        {
            while (Running)
            {
                IPEndPoint ep = new IPEndPoint(_ep.Address, _port);
                byte[] data = _udp.Receive(ref ep);
                // request made
                if (!_location)
                {
                    if (Enumerable.SequenceEqual(_sendKey, data))
                    {
                        Request r = new Request(ep.Address);
                        r.OnButton += ResolveRequest;
                        _left.AddChild(r);
                    }
                    continue;
                }
                if (!_connection)
                {
                    if (Enumerable.SequenceEqual(_connectKey, data))
                    {
                        _console.WriteLine("Request received.");
                        _connection = true;
                        Task.Run(StartCall);
                        continue;
                    }
                    
                    if (Enumerable.SequenceEqual(_goAwayKey, data))
                    {
                        _console.WriteLine("Request DENIED.");
                    }
                    continue;
                }
                if (!_sendingData)
                {
                    DeviceInfo di = DeviceInfo.FromBytes(data);
                    _console.WriteLine("Device information exchanged.");
                    _rb.Info = di;
                    _rb.OurSR = _sys.SampleRate;
                    _sendingData = true;
                    continue;
                }
                
                // audio data
                _rb.Write(data);
            }
        }
        private void Send(Span<float> data, uint channels)
        {
            Span<byte> bytes = MemoryMarshal.AsBytes(data);
            byte[] array = bytes.ToArray();
            
            _udp.SendAsync(array, array.Length, _ep);
        }
        private bool GetIP(string ipStr)
        {
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
            
            _ep = new IPEndPoint(
                new IPAddress(new byte[] { (byte)part1, (byte)part2, (byte)part3, (byte)part4 }),
                _port
            );
            return true;
            
        Error:
            _console.WriteLine("Invalid ip address.");
            return false;
        }
    }
}
