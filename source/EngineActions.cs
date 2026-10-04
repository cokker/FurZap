namespace FurZap {
public static class EngineActions {
 public static void Start(Backend backend,string strategy){
  if(backend.ServiceState()!=0)backend.ServiceStart();
  else backend.Start(strategy);
 }
 public static void Stop(Backend backend){
  if(backend.ServiceState()!=0)backend.ServiceStop();
  else backend.Stop();
 }
}
}
