using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
namespace FurZap {
sealed class ApplyTestBackend:Backend {
 public bool Live,Foreign,FailNext;public int State,Starts,Installs;public string Command="previous exact command",Marker="previous marker";
 public ApplyTestBackend(string root):base(root,true){}
 public override bool Running{get{return Live;}}
 public override int ServiceState(){return State;}
 public override string ServicePath(){return Command;}
 public override string ServiceStrategy(){return Marker;}
 public override void CheckOwnership(){if(Foreign)throw new Exception("foreign service");}
 public override void Stop(){Live=false;}
 public override void Start(string strategy,bool testing=false){Starts++;if(FailNext){FailNext=false;throw new Exception("simulated startup failure");}Live=true;ActiveStrategy=strategy;}
 public override void ServiceStop(){if(State!=0)State=1;}
 public override void ServiceStart(){if(FailNext){FailNext=false;throw new Exception("simulated service failure");}State=4;}
 public override void Install(string strategy,bool start=true){Installs++;Command=Arguments(strategy);Marker=strategy;State=1;if(start)ServiceStart();}
 public override void RestoreServiceCommand(string command,string strategy){Command=command;Marker=strategy;}
 public override void VerifyActive(bool service){if(service?State!=4:!Live)throw new Exception("not running");}
}
public static class ApplyTests {
 public static void Run(string root,Action<bool,string> check,Action<Action,string> reject){
  var b=new ApplyTestBackend(root);string strategy=b.Strategies[0];b.Pref["strategy"]=strategy;b.ActiveStrategy=strategy;b.Live=true;
  b.Settings("all","444","555","any");check(b.Live&&b.Starts==1&&b.Game()["tcp"]=="444","saving settings restarts live process with new ports");
  b.SaveList("list-general-user.txt","first.example\n");check(b.Starts==2&&b.Live,"saving lists restarts live process");
  int unchangedStarts=b.Starts;reject(()=>b.SaveList("list-general.txt","^dns.google\nhttps://bad.example/a\n"),"bad domain is rejected before stopping active engine");check(b.Live&&b.Starts==unchangedStarts,"validation failure leaves active process running");
  b.ReplaceFake("ACTIVE_DISCORD_UDP.bin","stun.bin");check(b.Starts==3,"packet changes restart process");
  string original=File.ReadAllText(b.P("lists/list-general-user.txt"));b.FailNext=true;reject(()=>b.SaveList("list-general-user.txt","bad.example\n"),"failed process restart reports rollback");check(b.Live&&File.ReadAllText(b.P("lists/list-general-user.txt"))==original,"process rollback restores exact previous file and running state");
  b.FailNext=true;reject(()=>b.SelectStrategy(b.Strategies[1]),"failed strategy switch rolls back");check(b.Get("strategy")==strategy&&b.ActiveStrategy==strategy&&b.Live,"strategy preference and active strategy restored");
  b.Live=false;int starts=b.Starts;b.SaveList("list-general-user.txt","stopped.example\n");check(!b.Live&&b.Starts==starts,"stopped process stays stopped");
  b.State=4;b.Settings("tcp","443","555","none");check(b.State==4&&!b.Live&&b.Installs==1&&b.Command.Contains("443"),"running service reconfigured and restarted");
  string command=b.Command,marker=b.Marker;original=File.ReadAllText(b.P("lists/list-general-user.txt"));b.FailNext=true;
  reject(()=>b.SaveList("list-general-user.txt","service-fail.example\n"),"failed service restart reports rollback");check(b.State==4&&b.Command==command&&b.Marker==marker&&File.ReadAllText(b.P("lists/list-general-user.txt"))==original,"service rollback restores command marker content and running state");
  b.State=1;b.SelectStrategy(b.Strategies[1]);check(b.State==1&&!b.Live,"stopped service receives config without starting");
  b.Foreign=true;original=File.ReadAllText(b.P("lists/list-general-user.txt"));reject(()=>b.SaveList("list-general-user.txt","foreign.example\n"),"foreign service rejected before changes");check(original==File.ReadAllText(b.P("lists/list-general-user.txt")),"foreign service leaves files untouched");b.Foreign=false;b.State=0;
  string package=Path.Combine(Path.GetDirectoryName(root),"candidate-engine");EngineUpdater.CopyTree(root,package);Backend.Atomic(Path.Combine(package,".furzap-version"),"9.0.0");File.WriteAllText(Path.Combine(package,"lists/list-general-user.txt"),"upstream.example\n");
  b.Live=true;b.ActiveStrategy=b.Get("strategy");b.ReplaceEngine(package);check(b.EngineVersion=="9.0.0"&&b.Live&&File.ReadAllText(b.P("lists/list-general-user.txt"))==original,"engine update keeps custom lists and running process");
  b.RollbackEngine();check(b.EngineVersion=="1.10.3"&&b.Live,"engine rollback restores previous version and process");
  b.FailNext=true;reject(()=>b.ReplaceEngine(package),"failed engine startup rolls back");check(b.EngineVersion=="1.10.3"&&b.Live,"failed engine update restores complete previous engine");
  b.Live=false;b.State=4;b.ReplaceEngine(package);check(b.State==4&&b.EngineVersion=="9.0.0","engine update restarts service");b.RollbackEngine();check(b.State==4&&b.EngineVersion=="1.10.3","engine rollback restores service");
  string zip=Path.Combine(Path.GetDirectoryName(root),"unsafe.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){using(var w=new StreamWriter(archive.CreateEntry("../escape.txt").Open()))w.Write("escape");}
  reject(()=>EngineUpdater.Extract(zip,Path.Combine(Path.GetDirectoryName(root),"extract-test")),"archive traversal rejected");
  check(b.UpdateHistory().Contains("Zapret"),"engine update and rollback history recorded");
 }
}
}
