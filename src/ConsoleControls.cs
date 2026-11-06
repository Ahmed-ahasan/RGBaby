using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
namespace Rgbaby {
    internal sealed class ColorEditor : UserControl {
        public readonly TextBox Hex;readonly Panel chip;public Color Color=Color.Cyan;public event Action Changed;
        public bool Valid {get{Color ignored;return Parse(out ignored);}}
        public ColorEditor(string name){
            BackColor=Theme.Panel;Height=40;Margin=Padding.Empty;
            var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};row.RowStyles.Add(new RowStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,38));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,75));Controls.Add(row);
            chip=new Panel{BackColor=Color,Dock=DockStyle.Fill,Margin=new Padding(0,7,10,7),BorderStyle=BorderStyle.FixedSingle};row.Controls.Add(chip,0,0);
            Hex=new TextBox{Text="#00FFFF",MaxLength=7,Font=new Font("Consolas",12),BackColor=Color.FromArgb(255,253,248),ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,Dock=DockStyle.Fill,Margin=new Padding(0,8,8,0),AccessibleName=name+" hex color"};row.Controls.Add(Hex,1,0);
            var pick=Theme.Button("COLOR",false);pick.Dock=DockStyle.Fill;pick.Margin=Padding.Empty;row.Controls.Add(pick,2,0);
            pick.Click+=delegate{using(var d=new ColorDialog{Color=Color,FullOpen=true})if(d.ShowDialog(this)==DialogResult.OK)SetColor(d.Color);};
            Hex.TextChanged+=delegate{Color c;bool valid=Parse(out c);Hex.ForeColor=valid?Theme.Text:Color.Firebrick;if(valid){Color=c;chip.BackColor=c;}if(Changed!=null)Changed();};
        }
        bool Parse(out Color c){int v;string text=Hex.Text.Trim().TrimStart('#');if(text.Length==6 && Int32.TryParse(text,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out v)){c=Color.FromArgb((v>>16)&255,(v>>8)&255,v&255);return true;}c=Color;return false;}
        public void SetColor(Color color){Hex.Text=String.Format("#{0:X2}{1:X2}{2:X2}",color.R,color.G,color.B);}
    }
    internal sealed class DeviceStrip : UserControl {
        public readonly DeviceKind Kind;public readonly LightingState State=new LightingState();
        public readonly ColorEditor Editor;public readonly RotaryKnob Intensity;public readonly Button Apply,BalanceButton;
        public readonly Label Connection;public readonly ComboBox Target;public bool Ready;public event Action Changed;
        public DeviceStrip(DeviceKind kind,string number,string title,string model,KeyMap map){
            Kind=kind;Dock=DockStyle.Fill;BackColor=Color.Transparent;Margin=Padding.Empty;Padding=new Padding(20,13,20,14);
            if(kind==DeviceKind.Mouse)State.GreenBalance=70;if(kind==DeviceKind.Motherboard)State.GreenBalance=65;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=10,Margin=Padding.Empty};
            foreach(int h in new[]{30,21,24,40,43,20,160,33,33,46})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));Controls.Add(layout);
            var heading=Theme.Label(number+"   "+title,17,Theme.Text);heading.Font=new Font("Segoe UI Semibold",17,FontStyle.Bold);layout.Controls.Add(heading,0,0);
            layout.Controls.Add(Theme.Label(model,8.5f,Theme.Muted),0,1);Connection=Theme.Label("•  Connecting",9,Theme.Muted);layout.Controls.Add(Connection,0,2);
            Editor=new ColorEditor(title){Dock=DockStyle.Fill};layout.Controls.Add(Editor,0,3);Editor.Changed+=delegate{if(Editor.Valid)State.Color=Editor.Color;FireChanged();};
            var swatches=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=7,RowCount=1,Margin=Padding.Empty};swatches.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var colors=new[]{Color.Red,Color.Lime,Color.Blue,Color.Cyan,Color.FromArgb(128,0,255),Color.Yellow,Color.White};var captions=new[]{"R","G","B","C","V","Y","W"};
            for(int i=0;i<7;i++){swatches.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/7));Color c=colors[i];var key=Theme.Button(captions[i],false);key.Dock=DockStyle.Fill;key.Margin=new Padding(0,3,4,3);key.BackColor=Theme.Mix(Theme.Panel,c,.18f);key.AccessibleName=title+" color "+c.Name;key.Click+=delegate{Editor.SetColor(c);};swatches.Controls.Add(key,i,0);}layout.Controls.Add(swatches,0,4);
            var level=Theme.Label("B R I G H T N E S S",8,Theme.Muted);level.TextAlign=ContentAlignment.MiddleCenter;layout.Controls.Add(level,0,5);
            Intensity=new RotaryKnob{Dock=DockStyle.Fill,Value=State.Brightness,AccessibleName=title+" brightness knob",Margin=Padding.Empty};Intensity.ValueChanged+=delegate{State.Brightness=Intensity.Value;FireChanged();};layout.Controls.Add(Intensity,0,6);
            BalanceButton=Theme.Button("",false);BalanceButton.Font=new Font("Segoe UI",8,FontStyle.Bold);BalanceButton.Dock=DockStyle.Fill;BalanceButton.Margin=new Padding(16,0,16,2);layout.Controls.Add(BalanceButton,0,7);RefreshBalanceLabel();
            BalanceButton.Click+=delegate{using(var d=new BalanceDialog(title,State))if(d.ShowDialog(this)==DialogResult.OK){d.CopyTo(State);RefreshBalanceLabel();FireChanged();}};
            if(kind==DeviceKind.Keyboard){Target=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,Font=new Font("Segoe UI",9),Margin=new Padding(16,3,16,3),AccessibleName="Keyboard target"};Target.Items.Add("All 94 keys");foreach(var k in map.keys)Target.Items.Add("Only "+k.label);Target.SelectedIndex=0;layout.Controls.Add(Target,0,8);Target.SelectedIndexChanged+=delegate{FireChanged();};}
            else {var note=Theme.Label(kind==DeviceKind.Mouse?"Logo · wheel · base":kind==DeviceKind.Ram?"All detected Corsair memory sticks":"Motherboard · ARGB headers",8.5f,Theme.Muted);note.TextAlign=ContentAlignment.MiddleCenter;layout.Controls.Add(note,0,8);}
            Apply=Theme.Button("APPLY & SAVE",false);Apply.Dock=DockStyle.Fill;Apply.Margin=new Padding(0,3,0,0);layout.Controls.Add(Apply,0,9);
        }
        void FireChanged(){if(Changed!=null)Changed();}
        void RefreshBalanceLabel(){BalanceButton.Text="BALANCE   "+State.RedBalance+" / "+State.GreenBalance+" / "+State.BlueBalance;}
        public void LoadState(LightingState state){State.RedBalance=state.RedBalance;State.GreenBalance=state.GreenBalance;State.BlueBalance=state.BlueBalance;State.Brightness=state.Brightness;State.Color=state.Color;Editor.SetColor(state.Color);Intensity.Value=state.Brightness;RefreshBalanceLabel();}
    }
}
