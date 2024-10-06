using System;
using Zene.Graphics;
using Zene.GUI;
using Zene.Structs;
using Zene.Windowing;

namespace lvt
{
    public class SelectBox : Element<SelectBox>, ILayout
    {
        public SelectBox()
        {
            Graphics = new Render(this);
            Layout = this;
        }
        
        public event EventHandler Change;
        public event EventHandler<int> OnSelect;
        
        public override GraphicsManager Graphics { get; }
        private string[] _options;
        public string[] Options
        {
            get => _options;
            set
            {
                _options = value;
                
                TriggerChange();
            }
        }
        
        private double _textSize = 10d;
        public double TextSize
        {
            get => _textSize;
            set
            {
                _textSize = value;
                
                TriggerChange();
            }
        }
        
        private double _width = 200d;
        public double Width
        {
            get => _width;
            set
            {
                _width = value;
                
                TriggerChange();
            }
        }
        
        private double _margin = 5d;
        public double Margin
        {
            get => _margin;
            set
            {
                _margin = value;
                
                TriggerChange();
            }
        }
        
        private int _hover = -1;
        private int _select = -1;
        
        public int Select
        {
            get => _select;
            set
            {
                _select = value;
                OnSelect?.Invoke(this, value);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            double top = Bounds.Height / 2d - _margin;
            
            double y = top - e.Y;
            _hover = Math.Clamp((int)(y / _textSize), 0, _options.Length - 1);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            
            _select = _hover;
            
            OnSelect?.Invoke(this, _select);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            
            _hover = -1;
        }

        private class Render : GraphicsManager<SelectBox>
        {
            public Render(SelectBox source)
                : base(source)
            {
            }

            public override void OnRender(IDrawingContext context)
            {
                double m = Source._margin;
                
                double left = (Source._width / -2d) + m;
                double top = Source.Bounds.Height / 2d - m;
                double size = Source._textSize;
                
                context.Framebuffer.Clear(new ColourF(0.2f, 0.2f, 0.2f));
                
                string[] op = Source._options;
                for (int i = 0; i < op.Length; i++)
                {
                    if (i == Source._hover)
                    {
                        context.Model = null;
                        context.DrawBox(new Box((0d, top - ((i + 0.5) * size)), (Source.Bounds.Width, size)), ColourF.Grey);
                    }
                    else if (i == Source._select)
                    {
                        context.Model = null;
                        context.DrawBox(new Box((0, top - ((i + 0.5) * size)), (Source.Bounds.Width, size)), ColourF.DimGrey);
                    }
                    
                    context.Model = new STMatrix(size, (left, top - (i * size)));
                    TextRenderer.DrawLeftBound(context, op[i], Shapes.SampleFont, 0, 0, false);
                }
            }
        }

        public Box GetBounds(LayoutArgs args)
        {
            return new Box(0d, (_width + (_margin * 2d), (_textSize * _options.Length) + (_margin * 2d)));
        }
    }
}