using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace Rgbaby {
    public static class StartupRestore {
        public static async Task<int> Run(AppSettings settings,Func<Task<DeviceKind[]>> discover,Func<DeviceKind,LightingState,int,Task<bool>> apply,Func<int,Task> delay,Func<bool> closing){
            var pending=settings.Devices.Where(d=>d.HasApplied).ToList();int restored=0;
            if(pending.Count==0){await discover();return 0;}
            for(int attempt=0;attempt<6 && pending.Count>0 && !closing();attempt++){
                var ready=await discover();if(closing())break;
                foreach(var d in pending.ToArray()){
                    if(closing())break;
                    if(ready.Contains(d.Kind) && await apply(d.Kind,d.ToState(),d.TargetSlot)){pending.Remove(d);restored++;}
                }
                if(pending.Count>0 && attempt<5 && !closing())await delay(2000);
            }
            return restored;
        }
    }
}
