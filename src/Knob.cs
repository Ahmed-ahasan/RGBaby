using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace Rgbaby {
    public sealed class RotaryKnob : Control {
        int value=50,startY,startValue;bool dragging;
        public event EventHandler ValueChanged;
        public int Value {get{return value;}set{int next=Math.Max(0,Math.Min(100,value));if(this.value==next)return;this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}
        public RotaryKnob(){DoubleBuffered=true;SetStyle(ControlStyles.Selectable,true);TabStop=true;Size=new Size(180,174);BackColor=Theme.Panel;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.Slider;AccessibleName="Brightness knob";AccessibleDescription="Drag up or down, scroll, or use arrow keys. Home is zero; End is 100.";}
        protected override bool IsInputKey(Keys keyData){var key=keyData&Keys.KeyCode;return key==Keys.Left || key==Keys.Right || key==Keys.Up || key==Keys.Down || key==Keys.Home || key==Keys.End || base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Up || e.KeyCode==Keys.Right)Value++;else if(e.KeyCode==Keys.Down || e.KeyCode==Keys.Left)Value--;else if(e.KeyCode==Keys.PageUp)Value+=10;else if(e.KeyCode==Keys.PageDown)Value-=10;else if(e.KeyCode==Keys.Home)Value=0;else if(e.KeyCode==Keys.End)Value=100;else return;e.Handled=true;}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();dragging=true;startY=e.Y;startValue=Value;Capture=true;}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Value=startValue+(startY-e.Y);}
        protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e){dragging=false;base.OnMouseCaptureChanged(e);}
        protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);if(Focused)Value+=e.Delta/120;}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        static PointF At(float x,float y,float radius,double angle){double a=angle*Math.PI/180;return new PointF(x+(float)Math.Cos(a)*radius,y+(float)Math.Sin(a)*radius);}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            float radius=Math.Min(Width/2f-32,(Height-36)/2f-15),cx=Width/2f,cy=radius+22;radius=Math.Max(12,radius);
            using(var pen=new Pen(Theme.Muted,1.2f))for(int i=0;i<=20;i++){double angle=135+i*13.5;g.DrawLine(pen,At(cx,cy,radius+9,angle),At(cx,cy,radius+(i%2==0?16:12),angle));}
            var shadow=new RectangleF(cx-radius-3,cy-radius+3,2*radius+6,2*radius+6);using(var b=new SolidBrush(Color.FromArgb(80,110,101,82)))g.FillEllipse(b,shadow);
            var ring=new RectangleF(cx-radius-2,cy-radius-2,2*radius+4,2*radius+4);using(var b=new SolidBrush(Color.FromArgb(172,164,146)))g.FillEllipse(b,ring);
            var face=new RectangleF(cx-radius,cy-radius,2*radius,2*radius);
            using(var path=new GraphicsPath()){path.AddEllipse(face);using(var b=new PathGradientBrush(path)){b.CenterPoint=new PointF(cx-radius*.3f,cy-radius*.5f);b.CenterColor=Color.FromArgb(255,254,250);b.SurroundColors=new[]{Color.FromArgb(207,200,184)};g.FillPath(b,path);}}
            using(var pen=new Pen(Color.FromArgb(180,172,154)))for(int i=0;i<48;i++)g.DrawLine(pen,At(cx,cy,radius*.84f,i*7.5),At(cx,cy,radius*.94f,i*7.5));
            using(var b=new LinearGradientBrush(face,Color.FromArgb(255,254,249),Color.FromArgb(229,223,209),90))g.FillEllipse(b,cx-radius*.78f,cy-radius*.78f,radius*1.56f,radius*1.56f);
            using(var p=new Pen(Color.White,1.5f))g.DrawArc(p,cx-radius*.76f,cy-radius*.76f,radius*1.52f,radius*1.52f,185,150);
            using(var p=new Pen(Enabled?Theme.Text:Theme.Muted,4)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLine(p,At(cx,cy,radius*.46f,135+Value*2.7),At(cx,cy,radius*.7f,135+Value*2.7));}
            using(var font=new Font("Segoe UI",12,FontStyle.Bold))TextRenderer.DrawText(g,Value+"%",font,new Rectangle(0,Height-28,Width,26),Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            if(Focused && ShowFocusCues)using(var p=new Pen(Theme.Accent,1.5f){DashStyle=DashStyle.Dot})g.DrawEllipse(p,cx-radius-20,cy-radius-20,radius*2+40,radius*2+40);
        }
    }
}
