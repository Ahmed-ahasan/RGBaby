using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

namespace Rgbaby {
    public sealed class KeyBounds { public int left,top,right,bottom; }
    public sealed class Key { public int id,ledSlot; public string label; public KeyBounds bounds; }
    public sealed class KeyMap {
        public Key[] keys;
        public static KeyMap Load() {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("keymap.json"))
            using(var reader=new StreamReader(stream)) {
                var map=new JavaScriptSerializer().Deserialize<KeyMap>(reader.ReadToEnd());
                if(map.keys.Length!=94 || map.keys.Select(k=>k.ledSlot).Distinct().Count()!=94 || map.keys.Any(k=>k.ledSlot<0 || k.ledSlot>=126))
                    throw new InvalidDataException("The embedded keyboard map is invalid.");
                return map;
            }
        }
    }
    internal static class Hid {
        [StructLayout(LayoutKind.Sequential)] internal struct InterfaceData { public int cbSize; public Guid guid; public int flags; public UIntPtr reserved; }
        [StructLayout(LayoutKind.Sequential)] internal struct Attributes { public int Size; public ushort Vendor,Product,Version; }
        [DllImport("hid.dll")] internal static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string enumerator,IntPtr hwnd,uint flags);
        [DllImport("setupapi.dll",SetLastError=true)] internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr dev,ref Guid guid,uint index,ref InterfaceData data);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData data,IntPtr detail,uint size,out uint needed,IntPtr dev);
        [DllImport("setupapi.dll")] internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("hid.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
        [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetAttributes(SafeFileHandle handle,ref Attributes attributes);
        [DllImport("hid.dll")] internal static extern int HidP_GetCaps(IntPtr data,IntPtr caps);
        [DllImport("hid.dll")] internal static extern int HidP_GetValueCaps(int type,IntPtr caps,ref ushort count,IntPtr data);
        [DllImport("hid.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_SetFeature(SafeFileHandle handle,byte[] report,int length);

        internal static DeviceDescriptor Describe(SafeFileHandle handle,string path) {
            var a=new Attributes(); a.Size=Marshal.SizeOf(typeof(Attributes));
            if(!HidD_GetAttributes(handle,ref a)) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr data;
            if(!HidD_GetPreparsedData(handle,out data)) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr caps=Marshal.AllocHGlobal(64);
            try {
                if(HidP_GetCaps(data,caps)!=0x110000) throw new IOException("Unable to read HID capabilities.");
                var d=new DeviceDescriptor { Path=path,Vendor=a.Vendor,Product=a.Product,Usage=(ushort)Marshal.ReadInt16(caps,0),UsagePage=(ushort)Marshal.ReadInt16(caps,2),FeatureBytes=(ushort)Marshal.ReadInt16(caps,8) };
                ushort count=(ushort)Marshal.ReadInt16(caps,60);
                if(count==1) {
                    IntPtr values=Marshal.AllocHGlobal(72);
                    try {
                        if(HidP_GetValueCaps(2,values,ref count,data)==0x110000 && count==1) {
                            d.ReportId=Marshal.ReadByte(values,2); d.ValueBits=(ushort)Marshal.ReadInt16(values,18); d.ValueCount=(ushort)Marshal.ReadInt16(values,20);
                        }
                    } finally { Marshal.FreeHGlobal(values); }
                }
                return d;
            } finally { Marshal.FreeHGlobal(caps); HidD_FreePreparsedData(data); }
        }
        internal static List<DeviceDescriptor> Enumerate() {
            var results=new List<DeviceDescriptor>();
            Guid guid; HidD_GetHidGuid(out guid);
            IntPtr set=SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,0x12);
            if(set==new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try {
                for(uint i=0;;i++) {
                    var item=new InterfaceData(); item.cbSize=Marshal.SizeOf(typeof(InterfaceData));
                    if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref item)) {
                        int error=Marshal.GetLastWin32Error(); if(error==259) break; throw new Win32Exception(error);
                    }
                    uint needed;
                    SetupDiGetDeviceInterfaceDetail(set,ref item,IntPtr.Zero,0,out needed,IntPtr.Zero);
                    if(needed==0) throw new Win32Exception(Marshal.GetLastWin32Error());
                    IntPtr detail=Marshal.AllocHGlobal((int)needed);
                    try {
                        Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);
                        if(!SetupDiGetDeviceInterfaceDetail(set,ref item,detail,needed,out needed,IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
                        string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                        if(path.IndexOf("vid_258a&pid_0049&",StringComparison.OrdinalIgnoreCase)<0) continue;
                        using(var handle=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                            if(handle.IsInvalid) continue;
                            try { results.Add(Describe(handle,path)); } catch(Win32Exception) { } catch(IOException) { }
                        }
                    } finally { Marshal.FreeHGlobal(detail); }
                }
            } finally { SetupDiDestroyDeviceInfoList(set); }
            return results;
        }
    }
    public sealed class KeyboardConnection : IDisposable {
        readonly SafeFileHandle handle;
        public KeyboardConnection(DeviceDescriptor selected) {
            if(!Protocol.Matches(selected)) throw new InvalidOperationException("Incompatible keyboard interface.");
            handle=Hid.CreateFile(selected.Path,0x40000000,3,IntPtr.Zero,3,0,IntPtr.Zero);
            try {
                if(handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot open the lighting interface. Close other keyboard lighting apps and rescan.");
                if(!Protocol.Matches(Hid.Describe(handle,selected.Path))) throw new InvalidOperationException("The keyboard descriptor changed. Rescan before testing.");
            } catch { handle.Dispose(); throw; }
        }
        public void Send(byte[] report) {
            Protocol.Validate(report);
            if(!Hid.HidD_SetFeature(handle,report,report.Length)) throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows rejected the lighting report. Close other keyboard RGB software and rescan.");
        }
        public void Dispose() { handle.Dispose(); }
    }
}
