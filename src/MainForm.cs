using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace Rgbaby {
    public sealed class MainForm : Form {
        readonly bool previewOnly,startupLaunch;
        readonly KeyMap map=KeyMap.Load();
        readonly DeviceStrip[] strips;
        readonly Button syncColors,syncBrightness,applyAll,rescan,off;
        readonly CheckBox startup;
        readonly TextBox log;
        readonly Label status;
        readonly Panel deck;
        readonly AppSettings settings;
        readonly string settingsPath;
        readonly ResumeRestore resumeRestore=new ResumeRestore();
        bool profileWritable=true;
        string lastSaveError;
        bool busy,closing,initializing=true,restoring,applying,closeAfterApply;
        public MainForm(bool previewOnly):this(previewOnly,false){}
        public MainForm(bool previewOnly,bool startupLaunch):this(previewOnly,startupLaunch,SettingsStore.DefaultPath){}
        public MainForm(bool previewOnly,bool startupLaunch,string profilePath){
            settingsPath=profilePath;
            this.previewOnly=previewOnly;this.startupLaunch=startupLaunch;
            string loadError=null;
            try{settings=previewOnly?new AppSettings():SettingsStore.Load(settingsPath);}catch(Exception e){settings=new AppSettings();profileWritable=false;loadError="Saved settings could not be loaded; original file preserved until you Apply & Save: "+e.Message;}
            Text="RGBABY / Studio console";BackColor=Theme.Background;ForeColor=Theme.Text;Font=new Font("Segoe UI",10);
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(1440,840);MinimumSize=new Size(760,560);StartPosition=FormStartPosition.CenterScreen;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))using(var icon=new Icon(stream))Icon=(Icon)icon.Clone();
            var viewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true};Controls.Add(viewport);
            var root=new TableLayoutPanel{Size=ClientSize,MinimumSize=new Size(1390,830),ColumnCount=1,RowCount=5,Padding=new Padding(26,17,26,16),Margin=Padding.Empty};viewport.Controls.Add(root);
            foreach(int h in new[]{86,90,484})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,27));
            viewport.SizeChanged+=delegate{root.Size=new Size(Math.Max(root.MinimumSize.Width,viewport.ClientSize.Width),Math.Max(root.MinimumSize.Height,viewport.ClientSize.Height));};
            var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,270));root.Controls.Add(header,0,0);
            var brand=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};brand.RowStyles.Add(new RowStyle(SizeType.Absolute,52));brand.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
            var wordmark=Theme.Label("rgbaby",34,Theme.Text);wordmark.Font=new Font("Arial",34,FontStyle.Bold);brand.Controls.Add(wordmark,0,0);brand.Controls.Add(Theme.Label("P E R S O N A L   L I G H T I N G   C O N T R O L L E R",8.5f,Theme.Muted),0,1);header.Controls.Add(brand,0,0);
            var edition=Theme.Label("STUDIO SERIES\nFOUR CHANNELS  /  MODEL 05",9,Theme.Muted);edition.TextAlign=ContentAlignment.MiddleRight;header.Controls.Add(edition,1,0);
            var toolbar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,Margin=new Padding(0,3,0,12)};toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,212));toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute,49));toolbar.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.Controls.Add(toolbar,0,1);
            syncColors=Theme.Button("SYNC COLORS",false);syncBrightness=Theme.Button("SYNC BRIGHTNESS",false);applyAll=Theme.Button("APPLY & SAVE ALL",true);
            syncColors.Dock=syncBrightness.Dock=applyAll.Dock=DockStyle.Fill;applyAll.Margin=new Padding(0,2,0,4);toolbar.Controls.Add(syncColors,0,0);toolbar.Controls.Add(syncBrightness,1,0);toolbar.Controls.Add(applyAll,3,0);
            var syncHint=Theme.Label("Sync copies from Keyboard. Color and brightness stay separate.",8.5f,Theme.Muted);toolbar.Controls.Add(syncHint,0,1);toolbar.SetColumnSpan(syncHint,3);
            var saveHint=Theme.Label("Apply remembers your settings.",8.5f,Theme.Muted);saveHint.TextAlign=ContentAlignment.MiddleRight;toolbar.Controls.Add(saveHint,3,1);
            deck=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,BackColor=Theme.Background};root.Controls.Add(deck,0,2);
            strips=new[]{new DeviceStrip(DeviceKind.Keyboard,"01","Keyboard","REDRAGON  /  K636CLO-RGB",map),new DeviceStrip(DeviceKind.Motherboard,"02","Motherboard","GIGABYTE  /  X870 GAMING WIFI6",map),new DeviceStrip(DeviceKind.Mouse,"03","Mouse","ASUS  /  GLADIUS II ORIGIN",map),new DeviceStrip(DeviceKind.Ram,"04","RAM","CORSAIR  /  VENGEANCE RGB",map)};
            var channels=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=strips.Length,Margin=Padding.Empty};for(int i=0;i<strips.Length;i++)channels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/strips.Length));deck.Controls.Add(channels);
            for(int i=0;i<strips.Length;i++){var shell=new InstrumentPanel{Dock=DockStyle.Fill,Padding=new Padding(1,1,1,3),Margin=new Padding(i==0?0:6,0,i==strips.Length-1?0:6,0)};shell.Controls.Add(strips[i]);channels.Controls.Add(shell,i,0);var strip=strips[i];strip.Changed+=RefreshControls;strip.Apply.Click+=async delegate{await ApplyDevices(new[]{strip},false,true);};}
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=new Padding(2,13,0,3)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,332));footer.RowStyles.Add(new RowStyle(SizeType.Absolute,37));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.Controls.Add(footer,0,3);
            startup=new CheckBox{Text="Start with Windows and restore saved lighting",AutoSize=false,Dock=DockStyle.Fill,ForeColor=Theme.Text,Font=new Font("Segoe UI",10),AccessibleName="Start with Windows and restore saved lighting"};footer.Controls.Add(startup,0,0);
            var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));rescan=Theme.Button("RESCAN",false);off=Theme.Button("LIGHTS OFF",false);rescan.Dock=off.Dock=DockStyle.Fill;rescan.Margin=new Padding(0,0,8,0);off.Margin=Padding.Empty;actions.Controls.Add(rescan,0,0);actions.Controls.Add(off,1,0);footer.Controls.Add(actions,1,0);
            log=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,BackColor=Theme.Background,ForeColor=Theme.Muted,Font=new Font("Segoe UI",8.5f),Margin=new Padding(0,7,0,0),AccessibleName="Activity and save status"};footer.Controls.Add(log,0,1);footer.SetColumnSpan(log,2);
            status=Theme.Label("Ready when you are.",9,Theme.Muted);root.Controls.Add(status,0,4);
            foreach(var s in strips){var saved=settings.For(s.Kind);s.LoadState(saved.ToState());}
            SetTarget(settings.For(DeviceKind.Keyboard).TargetSlot);
            if(!previewOnly)try{startup.Checked=StartupRegistration.Enabled(Application.ExecutablePath);}catch(Exception e){AddLog("Startup setting unavailable: "+e.Message);}
            startup.CheckedChanged+=delegate{
                if(initializing || previewOnly)return;
                try{StartupRegistration.SetEnabled(Application.ExecutablePath,startup.Checked);SaveProfile();AddLog(startup.Checked?"Windows startup enabled. Apply & Save the lighting you want restored.":"Windows startup disabled.");}
                catch(Exception e){initializing=true;startup.Checked=!startup.Checked;initializing=false;AddLog("Startup could not be changed: "+e.Message);}
            };
            syncColors.Click+=delegate{if(!strips[0].Editor.Valid)return;LightingState.ShareColor(strips.Select(s=>s.State),strips[0].State.Color);foreach(var s in strips)s.Editor.SetColor(s.State.Color);AddLog("Colors synced from Keyboard. Brightness and balance kept. Apply & Save All to send.");};
            syncBrightness.Click+=delegate{LightingState.ShareBrightness(strips.Select(s=>s.State),strips[0].State.Brightness);foreach(var s in strips)s.Intensity.Value=s.State.Brightness;AddLog("Brightness synced from Keyboard. Colors and balance kept. Apply & Save All to send.");};
            applyAll.Click+=async delegate{await ApplyDevices(strips,false,true);};off.Click+=async delegate{await ApplyDevices(strips,true,true);};rescan.Click+=async delegate{await Scan();};
            Shown+=async delegate{RestoreWindow();if(!previewOnly)await InitializeHardware();};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){
                if(applying && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;closeAfterApply=true;status.Text="Finishing the current update before closing…";return;}
                if(!previewOnly && profileWritable && !SaveProfile() && e.CloseReason==CloseReason.UserClosing)MessageBox.Show(this,"Could not save your settings or window position:\n\n"+lastSaveError+"\n\nThe previous saved profile has been kept.","RGBABY save error",MessageBoxButtons.OK,MessageBoxIcon.Error);
                closing=true;
            };
            initializing=false;
            if(loadError!=null)AddLog(loadError);AddLog("Drag a knob up/down, scroll while focused, or use arrow keys. Apply & Save keeps your look.");RefreshControls();
        }
        void SetTarget(int slot){int index=slot<0?-1:Array.FindIndex(map.keys,k=>k.ledSlot==slot);strips[0].Target.SelectedIndex=index+1;if(slot>=0 && index<0){settings.For(DeviceKind.Keyboard).HasApplied=false;AddLog("Saved keyboard target was invalid; automatic keyboard restore disabled.");}}
        int TargetSlot {get{return strips[0].Target.SelectedIndex<=0?-1:map.keys[strips[0].Target.SelectedIndex-1].ledSlot;}}
        void RestoreWindow(){
            Rectangle bounds;
            if(!previewOnly && settings.Window!=null){var w=settings.Window;bounds=new Rectangle(w.X,w.Y,w.Width,w.Height);}
            else bounds=Bounds;
            Bounds=SettingsStore.FitBounds(bounds,Screen.AllScreens.Select(s=>s.WorkingArea).ToArray());
            if(!previewOnly && settings.Window!=null && settings.Window.Maximized)WindowState=FormWindowState.Maximized;
        }
        bool SaveProfile(){
            if(previewOnly)return true;
            if(!profileWritable)return false;
            var b=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;settings.Window=new WindowPlacement{X=b.X,Y=b.Y,Width=b.Width,Height=b.Height,Maximized=WindowState==FormWindowState.Maximized};
            try{SettingsStore.Save(settingsPath,settings);lastSaveError=null;return true;}catch(Exception e){lastSaveError=e.Message;AddLog("Settings NOT saved: "+e.Message);if(!closing)status.Text="Lighting may be applied, but settings could not be saved.";return false;}
        }
        async Task InitializeHardware(){
            if(!startupLaunch){await Scan();return;}
            restoring=true;RefreshControls();int expected=settings.Devices.Count(d=>d.HasApplied);
            try{
                int restored=await StartupRestore.Run(settings,async delegate{await Scan();return strips.Where(s=>s.Ready).Select(s=>s.Kind).ToArray();},async delegate(DeviceKind kind,LightingState state,int slot){var s=strips.Single(d=>d.Kind==kind);s.LoadState(state);if(kind==DeviceKind.Keyboard)SetTarget(slot);return await ApplyDevices(new[]{s},false,false)==1;},Task.Delay,()=>closing);
                if(!closing){status.Text=expected==0?"No saved lighting yet. Choose your settings and Apply & Save.":"Startup restore: "+restored+" of "+expected+" saved devices applied.";AddLog(status.Text);}
            }catch(Exception e){AddLog("Startup restore failed: "+e.Message);}
            finally{restoring=false;if(!closing)RefreshControls();}
        }
        void AddLog(string message){if(log!=null && !closing)log.AppendText(DateTime.Now.ToString("HH:mm")+"  "+message+Environment.NewLine);}
        protected override void WndProc(ref Message message){
            // PBT_APMRESUMEAUTOMATIC is sent once for every wake. Do not also handle
            // PBT_APMRESUMESUSPEND, which can follow it for the same wake.
            bool resumed=message.Msg==0x0218 && message.WParam.ToInt64()==0x12;
            base.WndProc(ref message);
            if(resumed && !previewOnly && !closing && IsHandleCreated)BeginInvoke(new Action(RestoreAfterResume));
        }
        async void RestoreAfterResume(){
            if(closing || previewOnly || resumeRestore.Running)return;
            bool hasSavedBoard=settings.For(DeviceKind.Motherboard).HasApplied;
            try{
                var work=resumeRestore.Run(settings,()=>busy || restoring,async delegate{
                    try{return await Task.Run(delegate{OpenRgbBackend.EnsureRunning();using(var sdk=new OpenRgbClient())OpenRgbCodec.Select(sdk.Devices(),DeviceKind.Motherboard);return new[]{DeviceKind.Motherboard};});}
                    catch(Exception e){AddLog("Wake discovery: "+e.Message);return new DeviceKind[0];}
                },async delegate(DeviceKind kind,LightingState state,int slot){
                    try{await Task.Run(delegate{using(var sdk=new OpenRgbClient())sdk.Apply(kind,state.OutputColor);});return true;}
                    catch(Exception e){AddLog("Wake restore: "+e.Message);return false;}
                },Task.Delay,()=>closing);
                RefreshControls();int restored=await work;
                if(!closing && hasSavedBoard){status.Text=restored==1?"Motherboard lighting restored after wake.":"Motherboard wake restore failed. Rescan and Apply to retry.";AddLog(status.Text);}
            }catch(Exception e){AddLog("Wake restore failed: "+e.Message);}
            finally{if(!closing)RefreshControls();}
        }
        void RefreshControls(){
            if(initializing || strips==null || status==null)return;bool editable=!busy && !restoring && !resumeRestore.Running;
            deck.Enabled=editable;syncColors.Enabled=editable && strips[0].Editor.Valid;syncBrightness.Enabled=editable;startup.Enabled=editable && !previewOnly;
            bool usable=editable && !previewOnly;foreach(var s in strips)s.Apply.Enabled=usable && s.Ready && s.Editor.Valid;
            applyAll.Enabled=usable && strips.Any(s=>s.Ready) && strips.Where(s=>s.Ready).All(s=>s.Editor.Valid);off.Enabled=usable && strips.Any(s=>s.Ready);rescan.Enabled=usable;
        }
        async Task Scan(){
            if(busy || closing)return;busy=true;status.Text="Connecting to your lighting devices…";RefreshControls();
            var messages=new List<string>();DeviceDescriptor keyboard=null;List<OpenRgbDevice> devices=null;
            foreach(var s in strips){s.Ready=false;s.Connection.Text="•  Connecting";s.Connection.ForeColor=Theme.Muted;}
            try{await Task.Run(delegate{try{keyboard=Protocol.Select(Hid.Enumerate());}catch(Exception e){messages.Add("Keyboard: "+e.Message);}try{OpenRgbBackend.EnsureRunning();using(var sdk=new OpenRgbClient())devices=sdk.Devices();}catch(Exception e){messages.Add("Lighting engine: "+e.Message);}});
                if(closing)return;strips[0].Ready=keyboard!=null;
                foreach(var s in strips.Skip(1))if(devices!=null)try{OpenRgbCodec.Select(devices,s.Kind);s.Ready=true;}catch(Exception e){messages.Add(e.Message);}
                foreach(var s in strips){s.Connection.Text=s.Ready?"●  Connected":"○  Not available";s.Connection.ForeColor=s.Ready?Color.FromArgb(75,111,77):Theme.Accent;}
                foreach(string m in messages)AddLog(m);status.Text=strips.Count(s=>s.Ready)+" of "+strips.Length+" channels connected. Settings are staged until Apply.";
            }finally{busy=false;if(!closing)RefreshControls();}
        }
        async Task<int> ApplyDevices(DeviceStrip[] requested,bool black,bool persist){
            if(busy || previewOnly || closing)return 0;var available=requested.Where(s=>s.Ready).ToArray();if(available.Length==0 || (!black && available.Any(s=>!s.Editor.Valid)))return 0;
            try{LightingOwnership.EnsureAvailable();}catch(IOException e){AddLog(e.Message);status.Text="Lighting paused: exit SignalRGB, then Rescan.";return 0;}
            var states=available.Select(s=>s.State.Snapshot()).ToArray();int target=black?-1:TargetSlot;int[] slots=target<0?map.keys.Select(k=>k.ledSlot).ToArray():new[]{target};
            busy=applying=true;status.Text=black?"Turning lighting off…":"Applying your settings…";RefreshControls();int sent=0;
            foreach(var s in requested.Where(s=>!s.Ready))AddLog(s.Kind+": not connected; saved settings kept.");
            try{
                for(int i=0;i<available.Length && !closing;i++){
                    var strip=available[i];var state=states[i];if(black)state.Brightness=0;
                    try{await Task.Run(delegate{if(strip.Kind==DeviceKind.Keyboard){var d=Protocol.Select(Hid.Enumerate());using(var c=new KeyboardConnection(d))c.Send(Protocol.Build(slots,state.Color.R,state.Color.G,state.Color.B,state.Brightness,state.RedBalance,state.GreenBalance,state.BlueBalance));}else using(var sdk=new OpenRgbClient())sdk.Apply(strip.Kind,state.OutputColor);});
                        sent++;if(persist){settings.Remember(strip.Kind,state,strip.Kind==DeviceKind.Keyboard?target:-1);profileWritable=true;}
                        AddLog(strip.Kind+": "+(black?"off":strip.Editor.Hex.Text+" at "+state.Brightness+"%")+" applied.");
                    }catch(Exception e){if(!closing){strip.Ready=false;strip.Connection.Text="○  Rescan needed";strip.Connection.ForeColor=Theme.Accent;AddLog(strip.Kind+": "+e.Message);}}
                }
                bool saved=!persist || sent==0 || SaveProfile();if(!closing)status.Text=sent+" of "+requested.Length+" devices applied"+(persist && sent>0?(saved?" and saved.":"; settings could not be saved."):".");
            }finally{busy=applying=false;if(!closing){RefreshControls();if(closeAfterApply)BeginInvoke(new Action(Close));}}
            return sent;
        }
    }
}
