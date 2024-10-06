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
            AddChild(_console);
            
            _left = new Container(new Layout(-0.5, 0d, 0.95, 1.95));
            _left.LayoutManager = new BlockLayout(10d);
            AddChild(_left);
            
            _enter = new TextInput(new TextLayout(5d, 0d, (1.6, 0d)));
            _enter.TextSize = 20d;
            _enter.SingleLine = true;
            _left.AddChild(_enter);
            
            _sys = new AudioSystem(Devices.DefaultOutput, 1024);
            _sys.Gain = 0.5;
            _read = new AudioReader(Devices.DefaultInput, 1024);
            
            _rb = new ReadBuffer(1024 * 8, 1024 * 2);
        }
        
        private ConsoleElement _console;
        private Container _left;
        private TextInput _enter;
        
        private UdpClient _udp;
        private IPEndPoint _ep;
        private ReadBuffer _rb;
        
        private bool _location = false;
        private bool _connection = false;
        
        private IAsyncResult _ar;
        
        private AudioSystem _sys;
        private AudioReader _read;
        
        protected override void OnStart(EventArgs e)
        {
            base.OnStart(e);
            
            _ar = _udp.BeginReceive(FindRequest, null);
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
                // end search
                _udp.Client.Close();
                _ar = null;
                
                Task.Run(() =>
                {
                    _udp = new UdpClient();
                    _udp.Connect(_ep);
                    
                    _console.WriteLine("Sending request.");
                    _udp.Send(_sendKey, _sendKey.Length);
                    _console.WriteLine("Awaiting response...");
                    
                    _ar = _udp.BeginReceive(RequestResult, null);
                });
            }
        }
        
        private void RequestResult(IAsyncResult ar)
        {
            byte[] confirm = _udp.EndReceive(ar, ref _ep);
            if (Enumerable.SequenceEqual(_connectKey, confirm))
            {
                _console.WriteLine("Request received.");
                StartCall();
                return;
            }
            
            if (Enumerable.SequenceEqual(_goAwayKey, confirm))
            {
                _console.WriteLine("Request DENIED.");
                return;
            }
            
            _ar = _udp.BeginReceive(RequestResult, null);
        }
        private void FindRequest(IAsyncResult ar)
        {
            if (_location) { return; }
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, _port);
            byte[] check = _udp.EndReceive(ar, ref ep);
            
            // request made
            if (Enumerable.SequenceEqual(_sendKey, check))
            {
                Request r = new Request(ep.Address);
                r.OnButton += ResolveRequest;
                _left.AddChild(r);
            }
            
            _ar = _udp.BeginReceive(FindRequest, null);
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
            
            _udp.Close();
            _udp = new UdpClient();
            _udp.Connect(ep);
            
            _udp.Send(_connectKey, _connectKey.Length);
            _ep = ep;
            
            Task.Run(StartCall);
        }
        private void StartCall()
        {
            byte[] info = new DeviceInfo(_read.SampleRate, _read.Stereo).GetBytes();
            _console.WriteLine("Sending device information.");
            _udp.Send(info, info.Length);
            
            byte[] oi = _udp.Receive(ref _ep);
            DeviceInfo di = DeviceInfo.FromBytes(oi);
            _console.WriteLine("Received device information.");
            _rb.Info = di;
            _rb.OurSR = _sys.SampleRate;
            
            Thread.Sleep(100);
            
            _console.WriteLine("Starting call.");
        
            Task.Run(Receive);
            _read.Start();
            _sys.Start();
        }
        private void Receive()
        {
            while (Running)
            {
                byte[] data = _udp.Receive(ref _ep);
                _rb.Write(data);
            }
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
