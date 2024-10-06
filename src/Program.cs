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
using Zene.Structs;

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
            
            // outputs
            SelectBox outDev = new SelectBox();
            outDev.TextSize = 12d;
            outDev.Width = 350d;
            string[] names = new string[Devices.Outputs.Length];
            for (int i = 0; i < names.Length; i++)
            {
                AudioDevice ad = Devices.Outputs[i];
                names[i] = ad.Name;
                if (ad.Name == Devices.DefaultOutput.Name)
                {
                    outDev.Select = i;
                }
            }
            outDev.OnSelect += SetOutput;
            outDev.Options = names;
            _left.AddChild(outDev);
            
            // inputs
            SelectBox inDev = new SelectBox();
            inDev.TextSize = 12d;
            inDev.Width = 350d;
            names = new string[Devices.Inputs.Length];
            for (int i = 0; i < names.Length; i++)
            {
                AudioDevice ad = Devices.Inputs[i];
                names[i] = ad.Name;
                if (ad.Name == Devices.DefaultInput.Name)
                {
                    inDev.Select = i;
                }
            }
            inDev.OnSelect += SetInput;
            inDev.Options = names;
            _left.AddChild(inDev);
            
            _enter = new TextInput(new TextLayout(5d, 0d, (1.6, 0d)));
            _enter.TextSize = 20d;
            _enter.SingleLine = true;
            _left.AddChild(_enter);
            
            _slide = new Slider();
            _slide.MinValue = 0d;
            _slide.MaxValue = 3d;
            _slide.Value = 0.5;
            _slide.SliderPos += Gain;
            _slide.SilderWidth = 200d;
            _slide.Padding = new Vector2(10, 30);
            
            _rb = new ReadBuffer();
            
            _sys = new AudioSystem(Devices.DefaultOutput, 1024);
            _sys.Gain = _slide.Value;
            _sys.Sources.Add(_rb);
            _read = new AudioReader(Devices.DefaultInput, 1024);
            _read.OnAudio += Send;
            
            RootElement.Focus = _enter;
        }
        
        private ConsoleElement _console;
        private Container _left;
        private TextInput _enter;
        private Slider _slide;
        
        private UdpClient _udp;
        private IPEndPoint _ep = new IPEndPoint(IPAddress.Any, _port);
        private ReadBuffer _rb;
        
        private bool _location = false;
        private bool _connection = false;
        private bool _sendingData = false;
        
        private AudioSystem _sys;
        private AudioReader _read;
        
        private bool _render = true;
        
        private void Gain(object s, double v)
        {
            _sys.Gain = v;
        }
        private void SetOutput(object s, int sel)
        {
            bool r = _sys.Running;
            double g = _sys.Gain;
            if (r)
            {
                _sys.Stop();
            }
            _sys.Dispose();
            
            _sys = new AudioSystem(Devices.Outputs[sel], 1024);
            _sys.Gain = g;
            _sys.Sources.Add(_rb);
            _rb.OurSR = _sys.SampleRate;
            if (r)
            {
                _sys.Start();
            }
        }
        private void SetInput(object s, int sel)
        {
            bool r = _read.Running;
            if (r)
            {
                _read.Stop();
            }
            _read.OnAudio -= Send;
            _read.Dispose();
            
            _read = new AudioReader(Devices.Inputs[sel], 1024);
            _read.OnAudio += Send;
            if (r)
            {
                _read.Start();
            }
            
            if (_sendingData)
            {
                byte[] info = new DeviceInfo(_read.SampleRate, _read.Stereo).GetBytes();
                _console.WriteLine("Sending new device info.");
                _udp.Send(info, info.Length, _ep);
            }
        }
        
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
            
            if (_sendingData)
            {
                // send end call
                _udp.Send(new byte[] { 5 }, 1, _ep);
            }
        }
        protected override void OnUpdate(FrameEventArgs e)
        {
            if (_render)
            {
                base.OnUpdate(e);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (e[Keys.D])
            {
                _render = !_render;
                return;
            }
            
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
            
            // add gain silder
            Task.Run(() =>
            {
                _left.AddChild(_slide);
            });
            
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
                
                byte code = data[0];
                
                if (code == 0)
                {
                    // audio data
                    _rb.Write(data.AsSpan().Slice(1));
                }
                if (code == 1)
                {
                    // new device info
                    DeviceInfo di = DeviceInfo.FromBytes(data);
                    _console.WriteLine("Device information updated.");
                    _rb.Info = di;
                    _rb.OurSR = _sys.SampleRate;
                }
                // end call
                if (code == 5)
                {
                    _console.WriteLine("Call was ended.");
                       
                    _sys.Stop();
                    _read.Stop();
                    _ep = new IPEndPoint(IPAddress.Any, _port);
                    
                    _connection = false;
                    _location = false;
                    
                    ListActions la = _left.Children.StartGroupAction();
                    la.Remove(_slide);
                    la.Add(_enter);
                    la.Apply();
                }
            }
        }
        private void Send(Span<float> data, uint channels)
        {
            Span<byte> bytes = MemoryMarshal.AsBytes(data);
            byte[] array = new byte[bytes.Length + 1];
            bytes.CopyTo(array.AsSpan().Slice(1));
            
            // data code
            array[0] = 0;
            
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
