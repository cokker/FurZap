using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace FurZap {
public class FakeScanBackend:Backend {
 public int State,Starts;public bool Live,Fail;public Action OnProbe=delegate{};
 public FakeScanBackend(string root):base(root,false){Strategies=Strategies.Take(2).ToArray();}
 public override bool Running{get{return Live;}}
 public override int ServiceState(){return State;}
 public override void CheckOwnership(){}
 public override void Start(string strategy,bool testing=false){Starts++;if(Fail&&strategy!=Strategies[0])throw new Exception("simulated engine failure");Live=true;ActiveStrategy=strategy;EnsureLists();}
 public override void Stop(){Live=false;}
 public override void ServiceStop(){if(State!=0)State=1;}
 public override void ServiceStart(){State=4;}
 public override ProbeResult[] ProbeServices(CancellationToken token){OnProbe();token.ThrowIfCancellationRequested();return new[]{new ProbeResult{Service="test",Ok=true}};}
}
public static class ScanTests {
 public static void Run(string root,Action<bool,string> check){
  var b=new FakeScanBackend(root);b.State=4;var r=b.ScanStrategies(CancellationToken.None,(a,c,d)=>{});check(b.State==4&&!b.Live&&r.Count==2,"scan restores running service");
  b.State=1;b.ScanStrategies(CancellationToken.None,(a,c,d)=>{});check(b.State==1&&!b.Live,"scan keeps stopped service stopped");
  b.State=0;b.Live=true;b.ActiveStrategy=b.Strategies[0];using(var cancel=new CancellationTokenSource()){b.OnProbe=()=>cancel.Cancel();bool cancelled=false;try{b.ScanStrategies(cancel.Token,(a,c,d)=>{});}catch(OperationCanceledException){cancelled=true;}check(cancelled&&b.Live&&b.ActiveStrategy==b.Strategies[0],"cancel restores previous process and strategy");}
  b.Live=false;b.OnProbe=delegate{};b.Fail=true;r=b.ScanStrategies(CancellationToken.None,(a,c,d)=>{});check(!b.Live&&r.Count==2&&r.Any(x=>x.Error.Length>0),"engine failure recorded and stopped state restored");
 }
}
}

