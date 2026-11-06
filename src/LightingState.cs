using System;
using System.Collections.Generic;
using System.Drawing;
namespace Rgbaby {
    public enum DeviceKind { Keyboard, Motherboard, Mouse, Ram }
    public sealed class LightingState {
        public Color Color=Color.Cyan;
        public int Brightness=50,RedBalance=100,GreenBalance=100,BlueBalance=100;
        public Color OutputColor {
            get {return Color.FromArgb(Scale(Color.R,RedBalance),Scale(Color.G,GreenBalance),Scale(Color.B,BlueBalance));}
        }
        int Scale(byte channel,int balance) {
            if(Brightness<0 || Brightness>100 || balance<0 || balance>100) throw new ArgumentOutOfRangeException("brightness/balance");
            return (channel*Brightness*balance+5000)/10000;
        }
        public static void ShareColor(IEnumerable<LightingState> devices,Color color) {foreach(var d in devices) d.Color=color;}
        public static void ShareBrightness(IEnumerable<LightingState> devices,int brightness) {if(brightness<0 || brightness>100)throw new ArgumentOutOfRangeException("brightness");foreach(var d in devices)d.Brightness=brightness;}
        public LightingState Snapshot() {return (LightingState)MemberwiseClone();}
    }
}
