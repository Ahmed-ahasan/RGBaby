using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using Rgbaby;
class LightingTests {
    static int failures;
    static void Check(bool value,string message) {if(!value) throw new Exception(message);}
    static void Test(string name,Action test) {try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static void Reject(Action test) {try{test();}catch(InvalidDataException){return;}throw new Exception("Malformed data accepted");}
    static int Main() {
        Test("Shared color preserves independent brightness and calibration",delegate {
            var a=new LightingState {Brightness=20,RedBalance=70}; var b=new LightingState {Brightness=80};
            LightingState.ShareColor(new[]{a,b},Color.Yellow);
            Check(a.Brightness==20 && b.Brightness==80 && a.RedBalance==70,"Shared color reset intensity/balance");
            Check(a.Color==Color.Yellow && b.Color==Color.Yellow,"Color not shared");
            Check(a.OutputColor.R==36 && a.OutputColor.G==51 && b.OutputColor.R==204,"Output intensity incorrect");
        });
        Test("Parse sanitized motherboard and mouse descriptor fixtures",delegate {
            var board=OpenRgbCodec.ParseDevice(File.ReadAllBytes("research/openrgb-device-0.bin"),0);
            var mouse=OpenRgbCodec.ParseDevice(File.ReadAllBytes("research/openrgb-device-1.bin"),1);
            Check(board.Name=="X870 GAMING WIFI6" && board.ColorCount==1,"Motherboard parser");
            Check(mouse.Name=="ASUS ROG Gladius II Origin" && mouse.ColorCount>0,"Mouse parser");
            Console.WriteLine("  Board: "+board.ColorCount+" LEDs; mouse: "+mouse.ColorCount+" LEDs");
            foreach(var d in new[]{board,mouse}) foreach(var m in d.Modes) Console.WriteLine("  "+d.Name+" "+m.Name+" colorMode="+m.Fields[11]+" colors="+m.Colors.Length+" brightness="+m.Fields[9]+" max="+m.Fields[5]);
            Check(board.Modes.Any(m=>m.Name=="Static") && mouse.Modes.Any(m=>m.Name=="Direct"),"Solid color modes missing");
        });
        Test("Reject truncated, oversized and trailing device data",delegate {
            var data=File.ReadAllBytes("research/openrgb-device-0.bin");
            foreach(int length in new[]{0,3,20,data.Length-1}) Reject(()=>OpenRgbCodec.ParseDevice(data.Take(length).ToArray(),0));
            var bad=(byte[])data.Clone(); bad[8]=255;bad[9]=255;Reject(()=>OpenRgbCodec.ParseDevice(bad,0));
            Reject(()=>OpenRgbCodec.ParseDevice(data.Concat(new byte[]{0}).ToArray(),0));
        });
        Test("Only exact unique hardware may be selected",delegate {
            var board=OpenRgbCodec.ParseDevice(File.ReadAllBytes("research/openrgb-device-0.bin"),0);
            Check(OpenRgbCodec.Select(new[]{board},DeviceKind.Motherboard)==board,"Expected target missing");
            Reject(()=>OpenRgbCodec.Select(new[]{board,board},DeviceKind.Motherboard));
            Reject(()=>OpenRgbCodec.Select(new[]{board},DeviceKind.Mouse));
            board.Location="HID: VID_1234&PID_5711"; Reject(()=>OpenRgbCodec.Select(new[]{board},DeviceKind.Motherboard));
        });
        Test("RAM selection excludes other vendors and rejects duplicate addresses",delegate {
            var ram=new OpenRgbDevice{Type=1,Vendor="Corsair",Name="Corsair Vengeance RGB",Location="I2C: 0x58"};
            var other=new OpenRgbDevice{Type=1,Vendor="Other",Name="RAM",Location="I2C: 0x59"};
            Check(OpenRgbCodec.SelectRam(new[]{ram,other}).Single()==ram,"Non-Corsair memory was selected");
            Reject(()=>OpenRgbCodec.SelectRam(new[]{other}));
            Reject(()=>OpenRgbCodec.SelectRam(new[]{ram,ram}));
        });
        Test("Competing SignalRGB blocks lighting writes before SDK commands",delegate {
            using(var server=new FakeSdk(false)) {
                bool blocked=false;
                try{using(var client=new OpenRgbClient(server.Port,()=>new[]{"SignalRgb"}))client.Apply(DeviceKind.Mouse,Color.Red);}catch(IOException e){blocked=e.Message.Contains("SignalRGB");}
                server.Finish();Check(blocked && server.Writes.Count==0,"Competing controller was allowed to write");
            }
        });
        Test("Static mode and LED payload use scaled RGB without extra dimming",delegate {
            var d=OpenRgbCodec.ParseDevice(File.ReadAllBytes("research/openrgb-device-0.bin"),0);
            var m=d.Modes.Single(x=>x.Name=="Static");
            var p=OpenRgbCodec.ModePayload(d.Modes.IndexOf(m),m,Color.FromArgb(51,102,153));
            Check(BitConverter.ToUInt32(p,0)==p.Length,"Mode length wrong");
            Check(BitConverter.ToUInt32(p,p.Length-4)==0x996633,"Mode RGB order wrong");
            var leds=OpenRgbCodec.LedsPayload(3,Color.FromArgb(51,102,153));
            Check(leds.Length==18 && BitConverter.ToUInt16(leds,4)==3 && BitConverter.ToUInt32(leds,6)==0x996633,"LED encoding wrong");
        });
        Test("Local SDK transport sends three header enables and independent solid colors",delegate {
            using(var server=new FakeSdk(false)) {
                using(var client=new OpenRgbClient(server.Port))client.Apply(DeviceKind.Motherboard,Color.FromArgb(10,20,30));
                server.Finish();
                Check(server.Writes.Count(x=>x.Item1==1000)==3,"Zero-size ARGB headers not enabled");
                Check(server.Writes.Count(x=>x.Item1==1101)==1 && server.Writes.All(x=>x.Item1!=1050),"Board must use hardware static effect");
                byte[] mode=server.Writes.Single(x=>x.Item1==1101).Item2;
                Check(BitConverter.ToUInt32(mode,mode.Length-4)==0x1E140A,"Board color changed in transit");
            }
            using(var server=new FakeSdk(false)) {
                using(var client=new OpenRgbClient(server.Port))client.Apply(DeviceKind.Mouse,Color.FromArgb(40,50,60));
                server.Finish();
                Check(server.Writes.Count==2 && server.Writes[0].Item1==1101 && server.Writes[1].Item1==1050,"Mouse command order incorrect");
                var leds=server.Writes[1].Item2;
                Check(BitConverter.ToUInt16(leds,4)==3 && BitConverter.ToUInt32(leds,6)==0x3C3228,"Mouse zones/intensity incorrect");
            }
        });
        Test("RAM apply reaches both Corsair modules and leaves other controllers alone",delegate {
            using(var server=new FakeSdk(false,true)) {
                using(var client=new OpenRgbClient(server.Port))client.Apply((DeviceKind)3,Color.FromArgb(40,50,60));
                server.Finish();
                Check(server.Writes.Count==4,"Expected mode and LED color for each RAM module");
                Check(server.WriteDevices.SequenceEqual(new uint[]{2,2,3,3}),"RAM apply targeted the wrong controller");
                foreach(var write in server.Writes.Where(x=>x.Item1==1050))Check(BitConverter.ToUInt16(write.Item2,4)==10 && BitConverter.ToUInt32(write.Item2,6)==0x3C3228,"RAM LED color incorrect");
            }
        });
        Test("Reject device-list changes before any lighting write",delegate {
            using(var server=new FakeSdk(true)) {
                bool rejected=false;
                try{using(var client=new OpenRgbClient(server.Port))client.Apply(DeviceKind.Mouse,Color.Red);}catch(IOException){rejected=true;}
                server.Finish();Check(rejected && server.Writes.Count==0,"A changed list allowed a lighting write");
            }
        });
        Console.WriteLine("Lighting failures: "+failures);return failures==0?0:1;
    }
    sealed class FakeSdk : IDisposable {
        readonly TcpListener listener;readonly Task worker;public readonly int Port;
        public readonly List<Tuple<uint,byte[]>> Writes=new List<Tuple<uint,byte[]>>();
        public readonly List<uint> WriteDevices=new List<uint>();
        static byte[] RamDescriptor(uint index) {
            using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms)) {
                Action<string> str=s=>{var b=System.Text.Encoding.UTF8.GetBytes(s+"\0");w.Write((ushort)b.Length);w.Write(b);};
                w.Write((uint)0);w.Write(1);str("Corsair Vengeance RGB DDR5");str("Corsair");str("DRAM");str("1");str("");str("I2C: test, address "+index);
                w.Write((ushort)1);w.Write(0);str("Direct");
                foreach(uint f in new uint[]{0,32,0,0,0,0,0,0,0,0,0,1})w.Write(f);
                w.Write((ushort)0);w.Write((ushort)1);str("DRAM");w.Write((uint)1);w.Write((uint)10);w.Write((uint)10);w.Write((uint)10);w.Write((ushort)0);w.Write((ushort)0);
                w.Write((ushort)10);for(int i=0;i<10;i++){str("LED "+i);w.Write((uint)i);}
                w.Write((ushort)10);for(int i=0;i<10;i++)w.Write((uint)0);
                w.Flush();ms.Position=0;w.Write((uint)ms.Length);return ms.ToArray();
            }
        }
        public FakeSdk(bool changeList):this(changeList,false){}
        public FakeSdk(bool changeList,bool ram) {
            listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;
            worker=Task.Run(delegate {
                using(var tcp=listener.AcceptTcpClient())using(var stream=tcp.GetStream())using(var r=new BinaryReader(stream))using(var w=new BinaryWriter(stream)) {
                    stream.ReadTimeout=4000;stream.WriteTimeout=4000;
                    while(true) {
                        byte[] magic=r.ReadBytes(4);if(magic.Length==0)break;Check(System.Text.Encoding.ASCII.GetString(magic)=="ORGB","Bad request header");
                        uint device=r.ReadUInt32(),id=r.ReadUInt32(),size=r.ReadUInt32();Check(size<10000,"Oversized request");byte[] payload=r.ReadBytes((int)size),reply=null;
                        if(id==40){Check(BitConverter.ToUInt32(payload,0)==4,"Protocol not negotiated");reply=BitConverter.GetBytes((uint)6);}
                        else if(id==0)reply=BitConverter.GetBytes((uint)(ram?4:2));
                        else if(id==1){if(changeList){id=100;reply=new byte[0];}else reply=device>=2?RamDescriptor(device):File.ReadAllBytes("research/openrgb-device-"+device+".bin");}
                        else if(id!=50){Writes.Add(Tuple.Create(id,payload));WriteDevices.Add(device);}
                        if(reply==null)continue;
                        w.Write(System.Text.Encoding.ASCII.GetBytes("ORGB"));w.Write(device);w.Write(id);w.Write((uint)reply.Length);
                        int half=reply.Length/2;w.Write(reply,0,half);w.Flush();w.Write(reply,half,reply.Length-half);w.Flush();
                    }
                }
            });
        }
        public void Finish(){if(!worker.Wait(5000))throw new Exception("Fake SDK did not stop");}
        public void Dispose(){listener.Stop();}
    }
}
