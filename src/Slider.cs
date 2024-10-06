using System;
using Zene.GUI;
using Zene.Graphics;
using Zene.Structs;
using Zene.Windowing;

namespace lvt
{
    public class Slider : Element, ILayout
    {
        public Slider()
        {
            Graphics = new LocalGraphics(this, OnRender);
            Layout = this;
        }
        
        private double _sliderPos;
        public double SliderPosition
        {
            get => _sliderPos;
            set
            {
                _sliderPos = Math.Clamp(value, 0d, 1d);
                
                SliderPos?.Invoke(this, Value);
            }
        }
        public ColourF SliderColour { get; set; } = new ColourF(0.6f, 0.6f, 0.6f);
        public ColourF SelectColour { get; set; } = new Colour(157, 159, 196);
        public ColourF BarColour { get; set; } = new Colour(157, 192, 196);
        
        private bool _relative = true;
        public bool Relative
        {
            get => _relative;
            set
            {
                if (_relative == value) { return; }
                
                _relative = value;
                
                Change?.Invoke(this, EventArgs.Empty);
            }
        }
        
        private Vector2 _pos;
        public Vector2 Position
        {
            get => _pos;
            set
            {
                if (_pos == value) { return; }
                
                _pos = value;
                
                Change?.Invoke(this, EventArgs.Empty);
            }
        }
        
        private Vector2 _padding;
        public Vector2 Padding
        {
            get => _padding;
            set
            {
                if (_padding == value) { return; }
                
                _padding = value;
                
                Change?.Invoke(this, EventArgs.Empty);
            }
        }
        
        private double _sliderWidth = 100d;
        public double SilderWidth
        {
            get => _sliderWidth;
            set
            {
                if (_sliderWidth == value) { return; }
                
                _sliderWidth = value;
                
                Change?.Invoke(this, EventArgs.Empty);
            }
        }
        
        public double MaxValue { get; set; }= 1000d;
        public double MinValue { get; set; }= 50d;
        public double Value
        {
            get => MinValue.Lerp(MaxValue, _sliderPos);
            set => SliderPosition = (value - MinValue) / (MaxValue - MinValue);
        }
        
        private readonly Font _font = Shapes.SampleFont;
        public override GraphicsManager Graphics { get; }

        private const double _barHeight = 20d;
        private const double _barWidth = 10d;
        private const double _sliderHeight = 10d;
        
        public event EventHandler<double> SliderPos;
        
        public event EventHandler Change;
        public Box GetBounds(LayoutArgs args)
        {
            Vector2 pos = _pos;
            
            if (_relative)
            {
                pos *= args.Size;
            }
            
            return new Box(pos, (_sliderWidth + _padding.X, _barHeight + _padding.Y));
        }
        
        private Box GetBar()
        {
            return new Box(((_sliderPos - 0.5) * _sliderWidth, 0), (_barWidth, _barHeight));
        }
        
        protected override void OnUpdate(EventArgs e)
        {
            base.OnUpdate(e);
            
            if (Focused && _fromMouse)
            {
                SliderPosition = (MouseLocation.X / _sliderWidth) + 0.5;
            }
        }
        private void OnRender(object sender, RenderArgs e)
        {
            //e.Context.Framebuffer.Clear(new Colour(240, 120, 90));
            
            // SLIDER
            e.Context.DrawRoundedBox(
                new Box(Vector2.Zero, (_sliderWidth, _sliderHeight)),
                SliderColour,
                0.5);
            
            // BAR
            e.Context.DrawRoundedBox(
                GetBar(),
                Focused ? SelectColour : BarColour,
                0.2);
            
            e.TextRenderer.Colour = new ColourF(1f, 1f, 1f);
            e.Context.Model = Matrix4.CreateScale(15) * Matrix4.CreateTranslation(0, (_barHeight + 15) * -0.5);
            e.TextRenderer.DrawCentred(e.Context, $"{Value:N1}", _font, 0, 0);
        }
        
        private bool _fromMouse;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            _fromMouse = true;
            
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            _fromMouse = false;
            
            base.OnMouseDown(e);
        }
        
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            double offset = 0.05;
            if (e[Mods.Shift])
            {
                offset = 0.01;
            }
            
            if (!_fromMouse && e[Keys.Left])
            {
                SliderPosition -= offset;
                return;
            }
            if (!_fromMouse && e[Keys.Right])
            {
                SliderPosition += offset;
                return;
            }
        }
    }
}
