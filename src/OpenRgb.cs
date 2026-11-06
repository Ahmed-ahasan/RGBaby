using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Principal;
namespace Rgbaby {
    public sealed class OpenRgbMode {public string Name; public uint[] Fields; public uint[] Colors;}
    public sealed class OpenRgbZone {public string Name; public uint Index,Type,Minimum,Maximum,Count;}
    public sealed class OpenRgbDevice {
        public uint Index; public int Type,ColorCount; public string Name,Vendor,Location;
        public List<OpenRgbMode> Modes=new List<OpenRgbMode>();
        public List<OpenRgbZone> Zones=new List<OpenRgbZone>();
    }
    public static class OpenRgbCodec {
        public const int MaxPacket=1024*1024;
        static string ReadString(BinaryReader r) {
            int n=r.ReadUInt16(); if(n<1 || n>4096 || n>r.BaseStream.Length-r.BaseStream.Position) throw new InvalidDataException("Invalid SDK string length.");
            byte[] b=r.ReadBytes(n); if(b[n-1]!=0) throw new InvalidDataException("Unterminated SDK string.");return Encoding.UTF8.GetString(b,0,n-1);
        }
        static int Count(BinaryReader r) {int n=r.ReadUInt16();if(n>4096) throw new InvalidDataException("SDK collection too large.");return n;}
        public static OpenRgbDevice ParseDevice(byte[] data,uint index) {
            try {
                if(data==null || data.Length<4 || data.Length>MaxPacket) throw new InvalidDataException("Invalid SDK device size.");
                using(var r=new BinaryReader(new MemoryStream(data))) {
                    if(r.ReadUInt32()!=data.Length) throw new InvalidDataException("SDK device length mismatch.");
                    var d=new OpenRgbDevice {Index=index,Type=r.ReadInt32(),Name=ReadString(r),Vendor=ReadString(r)};
                    ReadString(r);ReadString(r);ReadString(r);d.Location=ReadString(r);
                    int modes=Count(r);r.ReadInt32();
                    for(int i=0;i<modes;i++) {
                        var m=new OpenRgbMode {Name=ReadString(r),Fields=new uint[12]};
                        for(int f=0;f<12;f++) m.Fields[f]=r.ReadUInt32();
                        m.Colors=new uint[Count(r)];for(int c=0;c<m.Colors.Length;c++) m.Colors[c]=r.ReadUInt32();d.Modes.Add(m);
                    }
                    int zones=Count(r);
                    for(int z=0;z<zones;z++) {
                        d.Zones.Add(new OpenRgbZone {Index=(uint)z,Name=ReadString(r),Type=r.ReadUInt32(),Minimum=r.ReadUInt32(),Maximum=r.ReadUInt32(),Count=r.ReadUInt32()});
                        int matrix=r.ReadUInt16();if(r.ReadBytes(matrix).Length!=matrix) throw new InvalidDataException("Truncated SDK matrix.");
                        int segments=Count(r);for(int s=0;s<segments;s++){ReadString(r);r.ReadUInt32();r.ReadUInt32();r.ReadUInt32();}
                    }
                    int leds=Count(r);for(int i=0;i<leds;i++){ReadString(r);r.ReadUInt32();}
                    d.ColorCount=Count(r);for(int i=0;i<d.ColorCount;i++)r.ReadUInt32();
                    if(d.ColorCount!=leds || r.BaseStream.Position!=data.Length) throw new InvalidDataException("Unexpected SDK device layout.");
                    return d;
                }
            }catch(EndOfStreamException e){throw new InvalidDataException("Truncated SDK device.",e);}
        }
        public static OpenRgbDevice Select(IEnumerable<OpenRgbDevice> devices,DeviceKind kind) {
            if(kind==DeviceKind.Ram)return SelectRam(devices)[0];
            if(kind!=DeviceKind.Motherboard && kind!=DeviceKind.Mouse)throw new InvalidDataException("Unsupported OpenRGB channel.");
            string name=kind==DeviceKind.Motherboard?"X870 GAMING WIFI6":"ASUS ROG Gladius II Origin";
            string usb=kind==DeviceKind.Motherboard?"VID_048D&PID_5711":"VID_0B05&PID_1877";
            int type=kind==DeviceKind.Motherboard?0:6;
            var matches=devices.Where(d=>d.Type==type && d.Name==name && d.Location.IndexOf(usb,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            if(kind==DeviceKind.Keyboard || matches.Length!=1) throw new InvalidDataException("Expected exactly one "+name+". Found "+matches.Length+". Rescan after reconnecting it.");
            return matches[0];
        }
        public static OpenRgbDevice[] SelectRam(IEnumerable<OpenRgbDevice> devices) {
            var matches=devices.Where(d=>d.Type==1 && String.Equals(d.Vendor,"Corsair",StringComparison.OrdinalIgnoreCase) && !String.IsNullOrWhiteSpace(d.Location)).ToArray();
            if(matches.Length==0)throw new InvalidDataException("Corsair RAM not detected. Install the signed PawnIO driver and run the OpenRGB engine as administrator, then Rescan. See README for setup.");
            if(matches.Select(d=>d.Location).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=matches.Length)throw new InvalidDataException("Ambiguous Corsair RAM locations. Rescan before applying.");
            return matches;
        }
        static uint Packed(Color c) {return (uint)(c.R|(c.G<<8)|(c.B<<16));}
        static void WriteString(BinaryWriter w,string s){byte[] b=Encoding.UTF8.GetBytes(s+"\0");w.Write((ushort)b.Length);w.Write(b);}
        static byte[] Sized(Action<BinaryWriter> write) {
            using(var ms=new MemoryStream()) using(var w=new BinaryWriter(ms)) {w.Write(0);write(w);w.Flush();ms.Position=0;w.Write((uint)ms.Length);return ms.ToArray();}
        }
        public static byte[] ModePayload(int index,OpenRgbMode mode,Color color) {
            return Sized(w=>{
                w.Write(index);WriteString(w,mode.Name);
                var fields=(uint[])mode.Fields.Clone();fields[9]=fields[5]; // Full hardware brightness: intensity is already in RGB.
                for(int f=0;f<12;f++)w.Write(fields[f]);
                w.Write((ushort)mode.Colors.Length);foreach(uint ignored in mode.Colors)w.Write(Packed(color));
            });
        }
        public static byte[] LedsPayload(int count,Color color) {
            if(count<1 || count>4096) throw new InvalidDataException("Invalid SDK LED count.");
            return Sized(w=>{w.Write((ushort)count);for(int i=0;i<count;i++)w.Write(Packed(color));});
        }
    }
    public sealed class OpenRgbClient : IDisposable {
        readonly TcpClient tcp; readonly NetworkStream stream;
        readonly Func<string[]> runningPrograms;
        public OpenRgbClient() : this(6742) {}
        public OpenRgbClient(int port):this(port,LightingOwnership.RunningPrograms){}
        public OpenRgbClient(int port,Func<string[]> runningPrograms) {
            this.runningPrograms=runningPrograms;
            tcp=new TcpClient();
            try {
                var connect=tcp.BeginConnect("127.0.0.1",port,null,null);
                using(connect.AsyncWaitHandle) if(!connect.AsyncWaitHandle.WaitOne(1500)) throw new IOException("OpenRGB local server is not available.");
                tcp.EndConnect(connect);tcp.NoDelay=true;stream=tcp.GetStream();stream.ReadTimeout=3000;stream.WriteTimeout=3000;
                byte[] v=Request(0,40,BitConverter.GetBytes((uint)4));
                if(v.Length!=4 || BitConverter.ToUInt32(v,0)<4) throw new InvalidDataException("OpenRGB SDK version 4 or newer is required.");
                Send(0,50,Encoding.UTF8.GetBytes("RGBABY CRT Console\0"));
            }catch {tcp.Close();throw;}
        }
        byte[] Exact(int size) {
            if(size<0 || size>OpenRgbCodec.MaxPacket) throw new InvalidDataException("SDK packet exceeds limit.");
            byte[] b=new byte[size];int p=0;var deadline=Stopwatch.StartNew();while(p<size){if(deadline.ElapsedMilliseconds>=3000)throw new IOException("OpenRGB packet timed out.");stream.ReadTimeout=(int)Math.Max(1,3000-deadline.ElapsedMilliseconds);int n=stream.Read(b,p,size-p);if(n==0)throw new IOException("OpenRGB disconnected.");p+=n;}return b;
        }
        void Send(uint index,uint id,byte[] payload) {
            using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms)) {
                w.Write(Encoding.ASCII.GetBytes("ORGB"));w.Write(index);w.Write(id);w.Write((uint)payload.Length);w.Write(payload);
                byte[] packet=ms.ToArray();stream.Write(packet,0,packet.Length);
            }
        }
        byte[] Request(uint index,uint id,byte[] payload) {
            Send(index,id,payload);byte[] h=Exact(16);
            if(Encoding.ASCII.GetString(h,0,4)!="ORGB") throw new InvalidDataException("Invalid OpenRGB packet header.");
            uint received=BitConverter.ToUInt32(h,8);
            if(received==100) throw new IOException("The OpenRGB device list changed. Rescan and try again.");
            if(received!=id || BitConverter.ToUInt32(h,4)!=index) throw new InvalidDataException("Unexpected OpenRGB reply.");
            return Exact(BitConverter.ToInt32(h,12));
        }
        public List<OpenRgbDevice> Devices() {
            byte[] response=Request(0,0,new byte[0]);
            if(response.Length!=4) throw new InvalidDataException("Invalid controller count.");
            uint count=BitConverter.ToUInt32(response,0);if(count>128)throw new InvalidDataException("Too many controllers.");
            var result=new List<OpenRgbDevice>();for(uint i=0;i<count;i++) result.Add(ReadDevice(i));return result;
        }
        OpenRgbDevice ReadDevice(uint index){return OpenRgbCodec.ParseDevice(Request(index,1,BitConverter.GetBytes((uint)4)),index);}
        public void Apply(DeviceKind kind,Color color) {
            LightingOwnership.EnsureAvailable(runningPrograms());
            // Never reuse indices from an earlier connection or scan.
            var devices=Devices();
            var selectedDevices=kind==DeviceKind.Ram?OpenRgbCodec.SelectRam(devices):new[]{OpenRgbCodec.Select(devices,kind)};
            foreach(var selected in selectedDevices)ApplySelected(selected,kind,color);
        }
        void ApplySelected(OpenRgbDevice selected,DeviceKind kind,Color color) {
            var current=ReadDevice(selected.Index);
            if(current.Name!=selected.Name || current.Location!=selected.Location || current.Type!=selected.Type || current.Vendor!=selected.Vendor) throw new IOException("Controller changed during apply. Rescan.");
            if(kind==DeviceKind.Motherboard) {
                // Static effects are broadcast by the hardware to the whole header.
                // One logical LED enables an empty header; no physical strip length is assumed.
                foreach(var zone in current.Zones.Where(z=>z.Name.StartsWith("ARGB_V2_",StringComparison.Ordinal) && z.Count==0 && z.Minimum==0 && z.Maximum>=1)) {
                    Send(current.Index,1000,BitConverter.GetBytes(zone.Index).Concat(BitConverter.GetBytes((uint)1)).ToArray());
                }
                current=ReadDevice(selected.Index);
                if(current.Name!=selected.Name || current.Location!=selected.Location) throw new IOException("Controller changed during header setup. Rescan.");
            }
            string modeName=kind==DeviceKind.Mouse || (kind==DeviceKind.Ram && current.Modes.Any(m=>m.Name=="Direct"))?"Direct":"Static";
            var mode=current.Modes.SingleOrDefault(m=>m.Name==modeName);
            if(mode==null) throw new InvalidDataException("This device has no "+modeName+" mode.");
            if(stream.DataAvailable) throw new IOException("OpenRGB has a pending device change. Rescan.");
            Send(current.Index,1101,OpenRgbCodec.ModePayload(current.Modes.IndexOf(mode),mode,color));
            if(mode.Fields[11]==1) Send(current.Index,1050,OpenRgbCodec.LedsPayload(current.ColorCount,color));
            // A round trip confirms the SDK consumed the commands, not the physical color.
            ReadDevice(current.Index);
        }
        public void Dispose(){tcp.Close();}
    }
    public static class LightingOwnership {
        public static void EnsureAvailable(){EnsureAvailable(RunningPrograms());}
        public static void EnsureAvailable(IEnumerable<string> programs) {
            if(programs.Any(p=>String.Equals(p,"SignalRgb",StringComparison.OrdinalIgnoreCase)))throw new IOException("SignalRGB is running and can overwrite colors or cause flashing. Exit SignalRGB and disable its Windows startup, then Rescan in RGBABY.");
        }
        public static string[] RunningPrograms() {
            var names=new List<string>();
            foreach(var process in Process.GetProcesses())using(process)try{names.Add(process.ProcessName);}catch(InvalidOperationException){}
            return names.ToArray();
        }
    }
    internal static class OpenRgbBackend {
        static bool StartConfiguredRamBackend() {
            string task="RGBABY OpenRGB "+WindowsIdentity.GetCurrent().User.Value;
            using(var query=Process.Start(new ProcessStartInfo("schtasks.exe","/Query /TN \""+task+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})) {
                query.OutputDataReceived+=delegate{};query.ErrorDataReceived+=delegate{};query.BeginOutputReadLine();query.BeginErrorReadLine();
                if(!query.WaitForExit(5000))throw new IOException("Timed out checking the RAM backend task.");
                if(query.ExitCode!=0)return false;
            }
            using(var start=Process.Start(new ProcessStartInfo("schtasks.exe","/Run /TN \""+task+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})) {
                start.OutputDataReceived+=delegate{};start.ErrorDataReceived+=delegate{};start.BeginOutputReadLine();start.BeginErrorReadLine();
                if(!start.WaitForExit(5000) || start.ExitCode!=0)throw new IOException("Could not start the configured RAM backend. Run setup-ram.ps1 as administrator to repair it.");
            }
            return true;
        }
        static string FindExecutable() {
            string root=AppDomain.CurrentDomain.BaseDirectory;
            string[] paths={Path.Combine(root,"backend","OpenRGB.exe"),Path.GetFullPath(Path.Combine(root,"..","runtime","OpenRGB","OpenRGB Windows 64-bit","OpenRGB.exe"))};
            return paths.FirstOrDefault(File.Exists);
        }
        internal static void EnsureRunning() {
            LightingOwnership.EnsureAvailable();
            try{using(var c=new OpenRgbClient())return;}catch(SocketException){}catch(IOException){}
            string exe=FindExecutable();if(exe==null)throw new FileNotFoundException("OpenRGB backend missing. Keep the backend folder beside RGBABY.exe.");
            string config=Path.Combine(Path.GetDirectoryName(exe),"rgbaby-config");Directory.CreateDirectory(config);
            if(!StartConfiguredRamBackend())Process.Start(new ProcessStartInfo(exe,"--server --server-host 127.0.0.1 --server-port 6742 --startminimized --config \""+config+"\"") {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(exe)});
            bool connected=false;
            for(int i=0;i<30;i++){Thread.Sleep(500);try{using(var c=new OpenRgbClient()){connected=true;var devices=c.Devices();OpenRgbCodec.Select(devices,DeviceKind.Motherboard);OpenRgbCodec.Select(devices,DeviceKind.Mouse);return;}}catch(SocketException){}catch(IOException){}}
            if(connected)return; // Let the scan report individual missing devices after startup settles.
            throw new IOException("OpenRGB did not start. Close conflicting RGB apps, then Rescan.");
        }
    }
}
