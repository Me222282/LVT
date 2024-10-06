using System;
using System.Net;
using Zene.GUI;
using Zene.Structs;

namespace lvt
{
    public class Request : Container
    {
        public delegate void Response(IPAddress address, bool accept);
        
        private static TextLayout _layout1 = new TextLayout(5d, 0d);
        private static Layout _layout2 = new Layout(0d, 0d, 1.8, 0.3);
        
        public Request(IPAddress source)
        {
            Graphics.Colour = ColourF.DarkGrey;
            Layout = _layout2;
            LayoutManager = new BlockLayout(5d);
            
            _address = source;
            
            _text = new Label(_layout1);
            _text.TextSize = 15d;
            _text.Text = $"Request from {source}: ";
            
            AddChild(_text);
            
            _accept = new Button(_layout1);
            _accept.Text = "Accept";
            _accept.TextSize = 15d;
            _accept.Click += Accept;
            AddChild(_accept);
            
            _decline = new Button(_layout1);
            _decline.Text = "Decline";
            _decline.TextSize = 15d;
            _decline.Click += Decline;
            AddChild(_decline);
        }
        
        private IPAddress _address;
        
        private Label _text;
        private Button _accept;
        private Button _decline;
        
        public event Response OnButton;
        
        private void Accept(object sender, EventArgs e)
        {
            OnButton?.Invoke(_address, true);
            
            Parent.Children.Remove(this);
        }
        private void Decline(object sender, EventArgs e)
        {
            OnButton?.Invoke(_address, false);
            
            Parent.Children.Remove(this);
        }
    }
}