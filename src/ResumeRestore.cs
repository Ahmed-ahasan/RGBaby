using System;
using System.Threading.Tasks;
namespace Rgbaby {
    public sealed class ResumeRestore {
        public bool Running {get;private set;}
        public async Task<int> Run(AppSettings settings,Func<bool> busy,Func<Task<DeviceKind[]>> discover,Func<DeviceKind,LightingState,int,Task<bool>> apply,Func<int,Task> delay,Func<bool> closing){
            if(Running || closing())return 0;
            Running=true;
            try{
                // Let an in-flight user apply or startup restore finish before snapshotting saved state.
                while(busy() && !closing())await delay(250);
                if(closing())return 0;
                var board=settings.For(DeviceKind.Motherboard);
                if(!board.HasApplied)return 0;
                var saved=new AppSettings();saved.Remember(DeviceKind.Motherboard,board.ToState(),-1);
                // USB and the engine can resume later than the window receives the power notification.
                await delay(2000);
                if(closing())return 0;
                return await StartupRestore.Run(saved,discover,apply,delay,closing);
            }finally{Running=false;}
        }
    }
}
