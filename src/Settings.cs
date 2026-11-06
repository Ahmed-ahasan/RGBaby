using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.Win32;
namespace Rgbaby {
    public sealed class DeviceProfile {
        public DeviceKind Kind;
        public int Red=0,Green=255,Blue=255,Brightness=50,RedBalance=100,GreenBalance=100,BlueBalance=100,TargetSlot=-1;
        public bool HasApplied;
        public LightingState ToState(){return new LightingState{Color=Color.FromArgb(Red,Green,Blue),Brightness=Brightness,RedBalance=RedBalance,GreenBalance=GreenBalance,BlueBalance=BlueBalance};}
    }
    public sealed class WindowPlacement {public int X,Y,Width=1200,Height=850;public bool Maximized;}
    public sealed class AppSettings {
        public int Version=2;
        public DeviceProfile[] Devices={new DeviceProfile{Kind=DeviceKind.Keyboard},new DeviceProfile{Kind=DeviceKind.Motherboard,GreenBalance=65},new DeviceProfile{Kind=DeviceKind.Mouse,GreenBalance=70},new DeviceProfile{Kind=DeviceKind.Ram}};
        public WindowPlacement Window;
        public DeviceProfile For(DeviceKind kind){return Devices.Single(d=>d.Kind==kind);}
        public void Remember(DeviceKind kind,LightingState state,int slot){
            var d=For(kind);d.Red=state.Color.R;d.Green=state.Color.G;d.Blue=state.Color.B;d.Brightness=state.Brightness;d.RedBalance=state.RedBalance;d.GreenBalance=state.GreenBalance;d.BlueBalance=state.BlueBalance;d.TargetSlot=slot;d.HasApplied=true;
        }
    }
    public static class SettingsStore {
        public static string DefaultPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RGBABY","settings.json");}}
        static void Validate(AppSettings s){
            if(s==null || (s.Version!=1 && s.Version!=2) || s.Devices==null || s.Devices.Length!=(s.Version==1?3:4) || s.Devices.Any(d=>d==null) || s.Devices.Select(d=>d.Kind).Distinct().Count()!=s.Devices.Length || (s.Version==1 && s.Devices.Any(d=>d.Kind==DeviceKind.Ram)))throw new InvalidDataException("Invalid RGBABY profile structure.");
            foreach(var d in s.Devices){
                if(!Enum.IsDefined(typeof(DeviceKind),d.Kind) || new[]{d.Red,d.Green,d.Blue}.Any(x=>x<0 || x>255) || new[]{d.Brightness,d.RedBalance,d.GreenBalance,d.BlueBalance}.Any(x=>x<0 || x>100) || d.TargetSlot< -1 || d.TargetSlot>125)throw new InvalidDataException("Invalid RGBABY profile values.");
            }
            if(s.Window!=null && (s.Window.Width<300 || s.Window.Height<300 || s.Window.Width>16000 || s.Window.Height>16000 || Math.Abs((long)s.Window.X)>100000 || Math.Abs((long)s.Window.Y)>100000))throw new InvalidDataException("Invalid saved window position.");
        }
        public static AppSettings Load(string path){
            if(!File.Exists(path))return new AppSettings();
            if(new FileInfo(path).Length>65536)throw new InvalidDataException("RGBABY profile is too large.");
            try{var s=new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(path));Validate(s);if(s.Version==1){s.Devices=s.Devices.Concat(new[]{new DeviceProfile{Kind=DeviceKind.Ram}}).ToArray();s.Version=2;}return s;}
            catch(ArgumentException e){throw new InvalidDataException("Cannot read RGBABY profile.",e);}
            catch(InvalidOperationException e){throw new InvalidDataException("Cannot read RGBABY profile.",e);}
        }
        public static void Save(string path,AppSettings settings){
            Validate(settings);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary=path+".tmp";
            try{File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(settings));if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        public static Rectangle FitBounds(Rectangle saved,Rectangle[] areas){
            if(areas==null || areas.Length==0)throw new ArgumentException("No display work area.");
            Rectangle area=areas.OrderByDescending(a=>{var i=Rectangle.Intersect(a,saved);return (long)i.Width*i.Height;}).First();
            int w=Math.Min(Math.Max(760,saved.Width),area.Width),h=Math.Min(Math.Max(560,saved.Height),area.Height);
            return new Rectangle(Math.Max(area.Left,Math.Min(saved.X,area.Right-w)),Math.Max(area.Top,Math.Min(saved.Y,area.Bottom-h)),w,h);
        }
    }
    public static class StartupRegistration {
        const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run",Name="RGBABY";
        public static string CommandLine(string exe){if(String.IsNullOrEmpty(exe) || exe.Contains("\""))throw new ArgumentException("Invalid executable path.");return "\""+Path.GetFullPath(exe)+"\" --startup";}
        public static bool IsOurEntry(string value,string exe){return String.Equals(value,CommandLine(exe),StringComparison.OrdinalIgnoreCase);}
        public static bool Enabled(string exe){using(var key=Registry.CurrentUser.OpenSubKey(Key))return key!=null && IsOurEntry(key.GetValue(Name) as string,exe);}
        public static void SetEnabled(string exe,bool enabled){
            using(var key=Registry.CurrentUser.CreateSubKey(Key)){
                if(key==null)throw new IOException("Cannot access Windows startup settings.");
                if(enabled)key.SetValue(Name,CommandLine(exe),RegistryValueKind.String);
                else if(IsOurEntry(key.GetValue(Name) as string,exe))key.DeleteValue(Name,false);
            }
        }
    }
}
