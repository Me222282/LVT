using System;
using System.Text;
using Zene.Graphics;
using Zene.GUI;
using Zene.Structs;

namespace lvt
{
    public class ConsoleElement : Element
    {
        public ConsoleElement()
        {
            Graphics = new Renderer(this);
        }
        public ConsoleElement(ILayout layout)
            : base(layout)
        {
            Graphics = new Renderer(this);
        }
        
        public override GraphicsManager Graphics  { get; }
        
        public double BorderWidth { get; set; } = 1;
        public ColourF BorderColour { get; set; } = new ColourF(1f, 1f, 1f);
        public ColourF BackgroundColour { get; set; }
        public double CornerRadius { get; set; } = 0.01;
        
        private int _lines;
        private StringBuilder _output = new StringBuilder();
        
        private int _charSpace = 0;
        public int CharSpace
        {
            get => _charSpace;
            set
            {
                _charSpace = value;

                TriggerChange();
            }
        }
        private int _lineSpace = 0;
        public int LineSpace
        {
            get => _lineSpace;
            set
            {
                _lineSpace = value;

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
        public ColourF TextColour { get; set; } = new ColourF(1f, 1f, 1f);
        private Font _font = Shapes.SampleFont;
        public Font Font
        {
            get => _font;
            set
            {
                _font = value;

                TriggerChange();
            }
        }
        
        public void WriteLine(string text)
        {
            if (_lines > 0)
            {
                _output.Append('\n');
            }
            
            _output.Append(text);
            _lines++;
        }
        
        private class Renderer : GraphicsManager<ConsoleElement>
        {
            public Renderer(ConsoleElement source)
                : base(source)
            {

            }

            public override void OnRender(IDrawingContext context)
            {
                double borderWidth = Math.Max(Source.BorderWidth, 0d);
                Size = Source.Size + borderWidth;

                // No point drawing box
                if (Source.BackgroundColour.A <= 0f && (Source.BorderColour.A <= 0f || borderWidth <= 0))
                {
                    goto DrawText;
                }

                context.DrawBorderBox(new Box(Vector2.Zero, Source.Bounds.Size), Source.BackgroundColour, borderWidth, Source.BorderColour, Source.CornerRadius);

            DrawText:
            
                string output = Source._output.ToString();
                
                if (Source.Font == null || output == null) { return; }
                
                context.Model = Matrix4.CreateScale(Source.TextSize) * Matrix4.CreateTranslation((Source.Bounds.Size / (-2d, 2d)) + (5d, -5d));
                TextRenderer.Colour = Source.TextColour;
                
                TextRenderer.DrawLeftBound(context, output, Source.Font, Source.CharSpace, Source.LineSpace, false);
            }
        }
    }
}