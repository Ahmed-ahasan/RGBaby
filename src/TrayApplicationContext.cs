using System;
using System.Windows.Forms;
namespace Rgbaby {
    internal sealed class TrayApplicationContext : ApplicationContext {
        readonly MainForm console;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        public TrayApplicationContext(MainForm console) {
            this.console=console;
            menu=new ContextMenuStrip();
            menu.Items.Add("Open RGBABY",null,delegate{console.OpenFromTray();});
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit RGBABY",null,delegate{console.ExitFromTray();});
            tray=new NotifyIcon{Icon=console.Icon,Text="RGBABY - click to open",ContextMenuStrip=menu,Visible=true};
            tray.MouseClick+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left)console.OpenFromTray();};
            console.FormClosed+=delegate{tray.Visible=false;ExitThread();};
            try{console.StartInTray();}catch{Dispose();throw;}
        }
        protected override void Dispose(bool disposing) {
            if(disposing){tray.Visible=false;tray.Dispose();menu.Dispose();console.Dispose();}
            base.Dispose(disposing);
        }
    }
}
