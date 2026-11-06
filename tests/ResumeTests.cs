using System;
using System.Drawing;
using System.Threading.Tasks;
using Rgbaby;
class ResumeTests {
    static int failures;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Test(string name,Action action){try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static int Main(){
        Test("Wake restores only the saved motherboard after delayed discovery",delegate{
            var settings=new AppSettings();foreach(DeviceKind kind in Enum.GetValues(typeof(DeviceKind)))settings.Remember(kind,new LightingState{Color=Color.Cyan,Brightness=40},-1);
            int scans=0,writes=0;var restore=new ResumeRestore();
            int result=restore.Run(settings,()=>false,()=>Task.FromResult(++scans==1?new DeviceKind[0]:new[]{DeviceKind.Motherboard}),
                (kind,state,slot)=>{Check(kind==DeviceKind.Motherboard,"Wake wrote another device");Check(state.Color.ToArgb()==Color.Cyan.ToArgb() && state.Brightness==40,"Wake did not use saved color");writes++;return Task.FromResult(true);},ms=>Task.FromResult(0),()=>false).GetAwaiter().GetResult();
            Check(result==1 && scans==2 && writes==1,"Saved motherboard was not restored exactly once");
        });
        Test("Wake waits for an existing apply and coalesces duplicate events",delegate{
            var settings=new AppSettings();settings.Remember(DeviceKind.Motherboard,new LightingState{Brightness=0},-1);
            var gate=new TaskCompletionSource<int>();bool busy=true;int writes=0;var restore=new ResumeRestore();
            Func<DeviceKind,LightingState,int,Task<bool>> apply=(kind,state,slot)=>{Check(state.Brightness==0,"Saved blackout lost");writes++;return Task.FromResult(true);};
            var first=restore.Run(settings,()=>busy,()=>Task.FromResult(new[]{DeviceKind.Motherboard}),apply,ms=>busy?gate.Task:Task.FromResult(0),()=>false);
            Check(!first.IsCompleted && writes==0,"Wake raced the active apply");
            var duplicate=restore.Run(settings,()=>busy,()=>Task.FromResult(new[]{DeviceKind.Motherboard}),apply,ms=>Task.FromResult(0),()=>false);
            Check(duplicate.GetAwaiter().GetResult()==0,"Duplicate wake started another restore");
            busy=false;gate.SetResult(0);Check(first.GetAwaiter().GetResult()==1 && writes==1 && !restore.Running,"Wake did not finish cleanly");
        });
        Test("Wake skips unsaved profiles and stops retries on close",delegate{
            var settings=new AppSettings();var restore=new ResumeRestore();int scans=0;
            Func<Task<DeviceKind[]>> discover=()=>{scans++;return Task.FromResult(new[]{DeviceKind.Motherboard});};
            Check(restore.Run(settings,()=>false,discover,(k,s,t)=>Task.FromResult(true),ms=>Task.FromResult(0),()=>false).GetAwaiter().GetResult()==0 && scans==0,"Unsaved board was changed");
            settings.Remember(DeviceKind.Motherboard,new LightingState(),-1);bool closing=false;
            Check(restore.Run(settings,()=>false,discover,(k,s,t)=>Task.FromResult(true),ms=>{closing=true;return Task.FromResult(0);},()=>closing).GetAwaiter().GetResult()==0 && scans==0,"Restore continued after close");
        });
        return failures==0?0:1;
    }
}
