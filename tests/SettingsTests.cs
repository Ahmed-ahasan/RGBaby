using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Rgbaby;
class SettingsTests {
    static int failures;
    static void Check(bool x,string m){if(!x)throw new Exception(m);}
    static void Test(string n,Action a){try{a();Console.WriteLine("PASS "+n);}catch(Exception e){failures++;Console.WriteLine("FAIL "+n+": "+e.Message);}}
    static void Reject(Action a){try{a();}catch(InvalidDataException){return;}throw new Exception("Bad profile accepted");}
    static int Main(){
        string path=Path.GetFullPath("build/settings-test-"+Guid.NewGuid()+".json");
        try {
            Test("Color and brightness sync are independent",delegate {
                var a=new LightingState {Color=Color.Red,Brightness=21,GreenBalance=65};var b=new LightingState {Color=Color.Blue,Brightness=93,GreenBalance=70};
                LightingState.ShareColor(new[]{a,b},Color.Yellow);Check(a.Brightness==21 && b.Brightness==93,"Color sync changed brightness");
                LightingState.ShareBrightness(new[]{a,b},44);Check(a.Color==Color.Yellow && b.Color==Color.Yellow && a.GreenBalance==65 && b.GreenBalance==70,"Brightness sync changed hue or calibration");
            });
            Test("Profile round trip preserves colors calibration intensity target and window",delegate {
                var p=new AppSettings();p.Remember(DeviceKind.Keyboard,new LightingState{Color=Color.Yellow,Brightness=37,GreenBalance=81},9);
                p.Remember(DeviceKind.Mouse,new LightingState{Color=Color.Cyan,Brightness=72,GreenBalance=70},-1);
                p.Window=new WindowPlacement{X=20,Y=35,Width=1200,Height=810,Maximized=true};
                SettingsStore.Save(path,p);var q=SettingsStore.Load(path);
                Check(q.For(DeviceKind.Keyboard).ToState().Color.ToArgb()==Color.Yellow.ToArgb() && q.For(DeviceKind.Keyboard).Brightness==37 && q.For(DeviceKind.Keyboard).GreenBalance==81 && q.For(DeviceKind.Keyboard).TargetSlot==9,"Keyboard state lost");
                Check(q.For(DeviceKind.Mouse).Brightness==72 && !q.For(DeviceKind.Motherboard).HasApplied,"Device state leaked");
                Check(q.Window.X==20 && q.Window.Maximized,"Window position lost");
                q.Remember(DeviceKind.Mouse,new LightingState{Brightness=0},-1);SettingsStore.Save(path,q);
                Check(SettingsStore.Load(path).For(DeviceKind.Mouse).Brightness==0 && File.Exists(path+".bak"),"Atomic update/blackout not saved");
            });
            Test("Legacy three-device profile gains RAM without losing saved lighting",delegate {
                File.WriteAllText(path,"{\"Version\":1,\"Devices\":[{\"Kind\":0,\"Brightness\":77,\"HasApplied\":true},{\"Kind\":1},{\"Kind\":2}]}");
                var p=SettingsStore.Load(path);
                Check(p.For(DeviceKind.Keyboard).Brightness==77 && p.For(DeviceKind.Keyboard).HasApplied,"Existing profile changed");
                Check(!p.For((DeviceKind)3).HasApplied,"New RAM channel must not auto-apply an unsaved color");
                p.Remember((DeviceKind)3,new LightingState{Color=Color.Purple,Brightness=64},-1);
                SettingsStore.Save(path,p);
                Check(SettingsStore.Load(path).For((DeviceKind)3).Brightness==64,"RAM profile not persisted");
            });
            Test("Saved successful state is detached from subsequent staged edits",delegate {
                var p=new AppSettings();var state=new LightingState{Brightness=25};p.Remember(DeviceKind.Mouse,state,-1);state.Brightness=90;
                Check(p.For(DeviceKind.Mouse).Brightness==25,"Saved state mutated before apply");
            });
            Test("Reject corrupt schema and out-of-range profile values",delegate {
                File.WriteAllText(path,"{oops");Reject(()=>SettingsStore.Load(path));
                var p=new AppSettings();p.For(DeviceKind.Mouse).GreenBalance=101;Reject(()=>SettingsStore.Save(path,p));
                p=new AppSettings();p.Devices[1].Kind=DeviceKind.Keyboard;Reject(()=>SettingsStore.Save(path,p));
                p=new AppSettings();p.Version=99;Reject(()=>SettingsStore.Save(path,p));
            });
            Test("Window placement stays on a visible monitor",delegate {
                var area=new Rectangle(0,0,1366,728);
                var fit=SettingsStore.FitBounds(new Rectangle(5000,-500,1800,1000),new[]{area});
                Check(area.Contains(fit) && fit.Width==1366 && fit.Height==728,"Off-screen/oversized bounds not repaired");
                var saved=new Rectangle(30,40,1000,650);Check(SettingsStore.FitBounds(saved,new[]{area})==saved,"Valid position changed");
            });
            Test("Startup command quotes path and is restricted to this application",delegate {
                string exe="C:\\My Apps\\RGBABY.exe";string command=StartupRegistration.CommandLine(exe);
                Check(command=="\""+exe+"\" --startup","Unsafe startup command quoting");
                Check(StartupRegistration.IsOurEntry(command,exe) && !StartupRegistration.IsOurEntry("other.exe",exe),"Startup identity check failed");
            });
            Test("Startup restores late devices once and skips unsaved devices",delegate {
                var p=new AppSettings();p.Remember(DeviceKind.Keyboard,new LightingState{Color=Color.Yellow,Brightness=22},9);p.Remember(DeviceKind.Mouse,new LightingState{Brightness=73,GreenBalance=70},-1);
                int scans=0,keys=0,mice=0;
                int result=StartupRestore.Run(p,()=>Task.FromResult(++scans==1?new[]{DeviceKind.Keyboard}:new[]{DeviceKind.Keyboard,DeviceKind.Mouse}),
                    (kind,state,slot)=>{if(kind==DeviceKind.Keyboard){keys++;Check(slot==9 && state.Brightness==22,"Saved key state lost");}else {mice++;Check(state.Brightness==73 && state.GreenBalance==70,"Saved mouse state lost");}return Task.FromResult(true);},ms=>Task.FromResult(0),()=>false).GetAwaiter().GetResult();
                Check(result==2 && scans==2 && keys==1 && mice==1,"Startup repeated applied devices or missed late device");
            });
            Test("Startup retry is bounded and does not overwrite missing device settings",delegate {
                var p=new AppSettings();p.Remember(DeviceKind.Motherboard,new LightingState{Brightness=88},-1);p.Remember(DeviceKind.Mouse,new LightingState{Brightness=65},-1);
                int scans=0,attempts=0,delays=0;
                int result=StartupRestore.Run(p,()=>{scans++;return Task.FromResult(new[]{DeviceKind.Mouse});},(kind,state,slot)=>Task.FromResult(++attempts>1),ms=>{delays++;return Task.FromResult(0);},()=>false).GetAwaiter().GetResult();
                Check(result==1 && attempts==2 && scans==6 && delays==5,"Retry was not bounded or failed write not retried");
                Check(p.For(DeviceKind.Motherboard).Brightness==88 && p.For(DeviceKind.Motherboard).HasApplied,"Missing device lost profile");
            });
        }finally{foreach(string f in new[]{path,path+".bak",path+".tmp"})if(File.Exists(f))File.Delete(f);}
        return failures==0?0:1;
    }
}
