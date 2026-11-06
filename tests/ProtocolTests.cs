using System;
using System.Collections.Generic;
using Rgbaby;
class ProtocolTests {
    static int failures, passed;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Test(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch(Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.Message); } }
    static void Throws(Action action) { try { action(); } catch(ArgumentException) { return; } catch(InvalidOperationException) { return; } throw new Exception("Expected rejection"); }
    static DeviceDescriptor Device() { return new DeviceDescriptor { Path="test", Vendor=0x258A, Product=0x0049, UsagePage=0xFF00, Usage=1, FeatureBytes=382, ReportId=8, ValueBits=8, ValueCount=381 }; }
    static int Main() {
        Test("Color balance attenuates channels independently without changing slot mapping", delegate {
            var p=Protocol.Build(new[]{9},255,255,255,50,50,100,25);
            Check(p[31]==64 && p[32]==128 && p[33]==32,"Color balance not applied before output rounding");
            Check(p[4]==0 && p[379]==0,"Balance lit unrelated slots");
        });
        Test("Color balance cannot introduce blue into pure green", delegate {
            var p=Protocol.Build(new[]{0},0,255,0,50,35,100,75);
            Check(p[4]==0 && p[5]==128 && p[6]==0,"Cross-channel color contamination");
        });
        Test("Reject color balance values outside zero to one hundred", delegate {
            Throws(()=>Protocol.Build(new[]{0},255,255,255,50,-1,100,100));
            Throws(()=>Protocol.Build(new[]{0},255,255,255,50,100,101,100));
            Throws(()=>Protocol.Build(new[]{0},255,255,255,50,100,100,101));
        });
        Test("Timed tests stop at their deadline even after slow writes", delegate {
            Check(TestSchedule.CanSend(0,0),"First frame rejected");
            Check(TestSchedule.CanSend(4999,20),"Early deadline");
            Check(!TestSchedule.CanSend(5000,20) && !TestSchedule.CanSend(6500,2),"Late writes allowed");
            Check(!TestSchedule.CanSend(4999,50),"Report limit ignored");
        });
        Test("Frame timing compensates for writes and skips missed intervals", delegate {
            Check(TestSchedule.DelayMilliseconds(40)==60,"Write latency added to interval");
            Check(TestSchedule.DelayMilliseconds(230)==70,"Missed frames should not burst");
            Check(TestSchedule.DelayMilliseconds(4990)==10 && TestSchedule.DelayMilliseconds(5100)==0,"Wait exceeded deadline");
        });
        Test("Select the RGB collection among shared USB IDs", delegate {
            var config=Device(); config.ReportId=6; config.FeatureBytes=1032;
            var rgb=Device();
            Check(Protocol.Select(new[]{config,rgb})==rgb,"Selected the wrong HID collection");
        });
        Test("Reject wrong descriptors independently", delegate {
            Action<DeviceDescriptor>[] edits={ d=>d.Vendor=0x1234,d=>d.Product=0x0016,d=>d.UsagePage=0x0001,d=>d.Usage=2,d=>d.FeatureBytes=1032,d=>d.ReportId=6,d=>d.ValueBits=16,d=>d.ValueCount=380,d=>d.Path=null };
            foreach(var edit in edits) { var d=Device(); edit(d); Check(!Protocol.Matches(d),"Accepted an incompatible interface"); }
        });
        Test("Reject missing and ambiguous devices", delegate { Throws(()=>Protocol.Select(new DeviceDescriptor[0])); Throws(()=>Protocol.Select(new[]{Device(),Device()})); });
        Test("Build A-key packet with correct RGB offsets and zero unused slots", delegate {
            var p=Protocol.Build(new[]{9},200,100,50,10);
            Check(p.Length==382,"Wrong report length");
            Check(p[0]==8 && p[1]==10 && p[2]==122 && p[3]==1,"Wrong report header");
            Check(p[31]==20 && p[32]==10 && p[33]==5,"Wrong A-key position or RGB order/brightness");
            for(int i=4;i<p.Length;i++) if(i<31 || i>33) Check(p[i]==0,"Unexpected lit slot");
        });
        Test("Handle final slot without truncation", delegate { var p=Protocol.Build(new[]{125},255,64,32,100); Check(p.Length==382 && p[379]==255 && p[380]==64 && p[381]==32,"Lost last RGB triplet"); });
        Test("Reject out-of-range slots and brightness", delegate {
            Throws(()=>Protocol.Build(new[]{126},1,2,3,10)); Throws(()=>Protocol.Build(new[]{-1},1,2,3,10));
            Throws(()=>Protocol.Build(new[]{9},1,2,3,101)); Throws(()=>Protocol.Build(new[]{9},1,2,3,-1));
            Throws(()=>Protocol.Build(null,1,2,3,10));
        });
        Test("Zero brightness leaves a valid black RGB report", delegate { var p=Protocol.Build(new[]{0,9,95},255,255,255,0); Check(p.Length==382,"Invalid packet"); for(int i=4;i<p.Length;i++) Check(p[i]==0,"Not black"); Protocol.Validate(p); });
        Test("Reject malformed reports at transport boundary", delegate {
            Throws(()=>Protocol.Validate(null)); Throws(()=>Protocol.Validate(new byte[1032]));
            var p=Protocol.Build(new[]{9},30,40,50,10); p[0]=6; Throws(()=>Protocol.Validate(p));
            p=Protocol.Build(new[]{9},30,40,50,10); p[1]=0x82; Throws(()=>Protocol.Validate(p));
        });
        Console.WriteLine(passed+" passed, "+failures+" failed"); return failures==0 ? 0 : 1;
    }
}
