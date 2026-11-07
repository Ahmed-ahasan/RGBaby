using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Rgbaby {
    internal static class Program {
        [STAThread] static int Main(string[] args) {
            try {
                if(args.Length==2 && args[0]=="--diagnose") {
                    var devices=Hid.Enumerate(); var selected=Protocol.Select(devices);
                    System.Collections.Generic.List<OpenRgbDevice> controllers;
                    using(var sdk=new OpenRgbClient()) controllers=sdk.Devices();
                    OpenRgbCodec.Select(controllers,DeviceKind.Motherboard);OpenRgbCodec.Select(controllers,DeviceKind.Mouse);
                    var output=new { verifiedInterface=selected, discovered=devices, openRgbControllers=controllers, keyCount=KeyMap.Load().keys.Length, lightingReportsSent=0 };
                    File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(output));return 0;
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                if(args.Length==2 && args[0]=="--render") {
                    using(var form=new MainForm(true)) {
                        form.ShowInTaskbar=false; form.Opacity=0; form.Show(); Application.DoEvents(); form.PerformLayout();
                        using(var image=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size)); image.Save(args[1],ImageFormat.Png);}
                    }
                    return 0;
                }
                bool startup=args.Length==1 && args[0]=="--startup";
                if(args.Length!=0 && !startup) throw new ArgumentException("Supported options: --startup, --diagnose output.json or --render preview.png");
                bool owns;
                using(var mutex=new Mutex(true,"Local\\RGBABY.KeyboardTest",out owns)) {
                    if(!owns) {if(!startup)MessageBox.Show("RGBABY is already running. Click its icon in the system tray to open it.","RGBABY");return 0;}
                    try {using(var context=new TrayApplicationContext(new MainForm(false,startup)))Application.Run(context);} finally {mutex.ReleaseMutex();}
                }
                return 0;
            } catch(Exception ex) {
                if(args.Length>=2) {File.WriteAllText(args[1]+".error.txt",ex.ToString());return 1;}
                MessageBox.Show(ex.Message,"RGBABY",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
            }
        }
    }
}
