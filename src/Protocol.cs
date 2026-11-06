using System;
using System.Collections.Generic;
namespace Rgbaby {
    public static class TestSchedule {
        public static bool CanSend(long elapsedMilliseconds,int sent) {return elapsedMilliseconds<5000 && sent<50;}
        public static int DelayMilliseconds(long elapsedMilliseconds) {
            if(elapsedMilliseconds>=5000) return 0;
            return (int)Math.Min(5000-elapsedMilliseconds,100-elapsedMilliseconds%100);
        }
    }
    public sealed class DeviceDescriptor {
        public string Path;
        public int Vendor, Product, UsagePage, Usage, FeatureBytes, ReportId, ValueBits, ValueCount;
    }
    public static class Protocol {
        public static bool Matches(DeviceDescriptor d) {
            return d!=null && !String.IsNullOrEmpty(d.Path) && d.Vendor==0x258A && d.Product==0x0049 && d.UsagePage==0xFF00 && d.Usage==1 && d.FeatureBytes==382 && d.ReportId==8 && d.ValueBits==8 && d.ValueCount==381;
        }
        public static DeviceDescriptor Select(IList<DeviceDescriptor> devices) {
            DeviceDescriptor result=null;
            foreach(var d in devices) if(Matches(d)) {
                if(result!=null) throw new InvalidOperationException("More than one matching keyboard. Connect only one K636CLO-RGB for this test.");
                result=d;
            }
            if(result==null) throw new InvalidOperationException("The verified K636CLO-RGB lighting interface was not found. Connect the keyboard and click Rescan.");
            return result;
        }
        public static byte[] Build(int[] slots, byte red, byte green, byte blue, int brightness) {
            return Build(slots,red,green,blue,brightness,100,100,100);
        }
        public static byte[] Build(int[] slots, byte red, byte green, byte blue, int brightness,int redBalance,int greenBalance,int blueBalance) {
            if(slots==null) throw new ArgumentNullException("slots");
            if(brightness<0 || brightness>100) throw new ArgumentOutOfRangeException("brightness");
            if(redBalance<0 || redBalance>100 || greenBalance<0 || greenBalance>100 || blueBalance<0 || blueBalance>100)
                throw new ArgumentOutOfRangeException("balance");
            var report=new byte[382]; report[0]=8; report[1]=10; report[2]=122; report[3]=1;
            foreach(int slot in slots) {
                if(slot<0 || slot>=126) throw new ArgumentOutOfRangeException("slots");
                int p=4+slot*3;
                report[p]=(byte)((red*brightness*redBalance+5000)/10000);
                report[p+1]=(byte)((green*brightness*greenBalance+5000)/10000);
                report[p+2]=(byte)((blue*brightness*blueBalance+5000)/10000);
            }
            return report;
        }
        public static void Validate(byte[] report) {
            if(report==null || report.Length!=382 || report[0]!=8 || report[1]!=10 || report[2]!=122 || report[3]!=1)
                throw new ArgumentException("Only the verified ID-8 RGB report is allowed.");
        }
    }
}
