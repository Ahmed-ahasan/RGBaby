using System;
using System.Drawing;
using System.Windows.Forms;
namespace Rgbaby {
    internal sealed class BalanceDialog : Form {
        public readonly NumericUpDown Red,Green,Blue;
        public BalanceDialog(string device,LightingState state) {
            Text=device+" / Color balance";BackColor=Theme.Background;ForeColor=Theme.Text;Font=new Font("Consolas",10);
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(430,284);
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=2,RowCount=6};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            foreach(int h in new[]{35,38,38,38,52,43})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));Controls.Add(root);
            var title=Theme.Label(device.ToUpperInvariant()+" / RGB TRIM",13,Theme.Accent);root.Controls.Add(title,0,0);root.SetColumnSpan(title,2);
            Red=Input(device+" red balance",state.RedBalance);Green=Input(device+" green balance",state.GreenBalance);Blue=Input(device+" blue balance",state.BlueBalance);
            root.Controls.Add(Theme.Label("RED / %",10,Theme.Text),0,1);root.Controls.Add(Red,1,1);
            root.Controls.Add(Theme.Label("GREEN / %",10,Theme.Text),0,2);root.Controls.Add(Green,1,2);
            root.Controls.Add(Theme.Label("BLUE / %",10,Theme.Text),0,3);root.Controls.Add(Blue,1,3);
            var hint=Theme.Label("Yellow too green? Lower GREEN.\nKeeps your chosen hue and intensity.\nClick Apply on the device to send.",8.5f,Theme.Muted);root.Controls.Add(hint,0,4);root.SetColumnSpan(hint,2);
            var buttons=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,Margin=Padding.Empty};for(int i=0;i<3;i++)buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));root.Controls.Add(buttons,0,5);root.SetColumnSpan(buttons,2);
            var reset=Theme.Button("RESET 100",false);var cancel=Theme.Button("CANCEL",false);var done=Theme.Button("USE TRIM",true);
            foreach(var b in new[]{reset,cancel,done}){b.Dock=DockStyle.Fill;b.Margin=new Padding(0,0,7,0);}
            buttons.Controls.Add(reset,0,0);buttons.Controls.Add(cancel,1,0);buttons.Controls.Add(done,2,0);
            reset.Click+=delegate{Red.Value=Green.Value=Blue.Value=100;};cancel.DialogResult=DialogResult.Cancel;done.DialogResult=DialogResult.OK;
            AcceptButton=done;CancelButton=cancel;
        }
        static NumericUpDown Input(string name,int value){return new NumericUpDown {Minimum=0,Maximum=100,Increment=1,Value=value,Dock=DockStyle.Fill,Margin=new Padding(0,3,0,0),BackColor=Theme.Panel,ForeColor=Theme.Text,AccessibleName=name};}
        public void CopyTo(LightingState state){state.RedBalance=(int)Red.Value;state.GreenBalance=(int)Green.Value;state.BlueBalance=(int)Blue.Value;}
    }
}
