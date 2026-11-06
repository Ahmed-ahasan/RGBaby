using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace Rgbaby {
    internal static class Theme {
        internal static readonly Color Background=Color.FromArgb(227,222,210),Panel=Color.FromArgb(245,241,232),Line=Color.FromArgb(182,175,160),Text=Color.FromArgb(54,51,44),Muted=Color.FromArgb(112,106,94),Accent=Color.FromArgb(167,77,38);
        internal static Label Label(string text,float size,Color color){return new Label{Text=text,ForeColor=color,Font=new Font("Segoe UI",size),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.Transparent};}
        internal static Button Button(string text,bool primary){return new TestButton{Text=text,BackColor=primary?Accent:Panel,ForeColor=primary?Color.White:Text,Font=new Font("Segoe UI",9,FontStyle.Bold),Height=44,Cursor=Cursors.Hand,Margin=new Padding(0,2,8,4),UseVisualStyleBackColor=false};}
        internal static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        internal static Color Mix(Color a,Color b,float t){return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
        internal static Color Surface(Control c){while(c!=null){if(c.BackColor.A==255)return c.BackColor;c=c.Parent;}return Panel;}
    }
    internal sealed class TestButton : Button {
        bool pressed,hover;
        public TestButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;Invalidate();}base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e){pressed=false;Invalidate();base.OnMouseCaptureChanged(e);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);}
        protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
        protected override void OnPaint(PaintEventArgs e){
            e.Graphics.Clear(Theme.Surface(Parent));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            if(Width<10 || Height<10)return;float offset=pressed?3:0;
            var shell=new RectangleF(1,4,Width-3,Height-6);
            using(var path=Theme.Round(shell,5))using(var b=new SolidBrush(Color.FromArgb(149,142,126)))e.Graphics.FillPath(b,path);
            var face=new RectangleF(1,1+offset,Width-3,Height-8);
            Color baseColor=Enabled?BackColor:Theme.Mix(BackColor,Theme.Background,.65f);
            using(var path=Theme.Round(face,5))using(var b=new LinearGradientBrush(face,Theme.Mix(baseColor,Color.White,hover?.45f:.3f),Theme.Mix(baseColor,Color.Black,.08f),90))using(var p=new Pen(Theme.Mix(baseColor,Color.Black,.25f))){e.Graphics.FillPath(b,path);e.Graphics.DrawPath(p,path);}
            var cap=RectangleF.Inflate(face,-4,-3);
            using(var path=Theme.Round(cap,3))using(var p=new Pen(Color.FromArgb(90,Color.White)))e.Graphics.DrawPath(p,path);
            var textRect=Rectangle.Round(face);TextRenderer.DrawText(e.Graphics,Text,Font,textRect,Enabled?ForeColor:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(textRect,-8,-7),ForeColor,baseColor);
        }
    }
    internal sealed class InstrumentPanel : Panel {
        public InstrumentPanel(){DoubleBuffered=true;BackColor=Theme.Panel;Padding=new Padding(20);}
        protected override void OnPaintBackground(PaintEventArgs e){
            e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            var r=new RectangleF(1,1,Width-3,Height-4);if(r.Width<1 || r.Height<1)return;
            using(var p=Theme.Round(r,9))using(var b=new LinearGradientBrush(r,Color.FromArgb(252,249,242),Theme.Panel,90))using(var line=new Pen(Theme.Line)){e.Graphics.FillPath(b,p);e.Graphics.DrawPath(line,p);}
            foreach(int x in new[]{9,Width-12})foreach(int y in new[]{10,Height-14}){using(var b=new SolidBrush(Color.FromArgb(206,199,184)))e.Graphics.FillEllipse(b,x-2,y-2,5,5);using(var p=new Pen(Theme.Muted))e.Graphics.DrawLine(p,x-1,y+1,x+1,y-1);}
        }
    }
}
