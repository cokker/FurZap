using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Xml;
namespace FurZap {
public static class Tests {
 static int count;
 static void Check(bool value,string title){if(!value)throw new Exception("FAIL: "+title);count++;Console.WriteLine("PASS: "+title);}
 static void Reject(Action a,string title){try{a();}catch{Check(true,title);return;}throw new Exception("FAIL: expected rejection: "+title);}
 public static void Run(string root){
  string temp=Path.Combine(Path.GetTempPath(),"FurZap-tests-"+Guid.NewGuid().ToString("N"));string copy=Path.Combine(temp,"engine");
  try{foreach(string f in Directory.GetFiles(root,"*",SearchOption.AllDirectories)){string target=Path.Combine(copy,f.Substring(root.Length).TrimStart(new char[] {Path.DirectorySeparatorChar}));Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(f,target);}
   var b=new Backend(copy,true);Check(b.Strategies.Length>=20,"all supplied strategies discovered");
   string task=FurZapAutostart.XmlForTest(@"C:\FurZap & Friends\FurZap.exe","S-1-5-21-42");var xml=new XmlDocument();xml.LoadXml(task);var ns=new XmlNamespaceManager(xml.NameTable);ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");
   Check(xml.SelectSingleNode("//t:Actions/t:Exec/t:Command",ns).InnerText==@"C:\FurZap & Friends\FurZap.exe"&&xml.SelectSingleNode("//t:Actions/t:Exec/t:Arguments",ns).InnerText=="--tray"&&xml.SelectSingleNode("//t:Principals/t:Principal/t:RunLevel",ns).InnerText=="HighestAvailable","FurZap startup task launches this portable EXE elevated and hidden in tray");
   string appCache=Path.Combine(b.Data,"updates",Guid.NewGuid().ToString("N"),"FurZap.exe");Directory.CreateDirectory(Path.GetDirectoryName(appCache));File.Copy(System.Windows.Forms.Application.ExecutablePath,appCache);
   var appRelease=new AppRelease{Version=typeof(Tests).Assembly.GetName().Version,Size=new FileInfo(appCache).Length,Digest=AppUpdater.FileHash(appCache)};
   Check(String.Equals(AppUpdater.FindDownloaded(appRelease,b.Data),Path.GetFullPath(appCache),StringComparison.OrdinalIgnoreCase),"completed FurZap update survives restart and is validated before reuse");
   using(var stream=new FileStream(appCache,FileMode.Append))stream.WriteByte(0);
   Check(AppUpdater.FindDownloaded(appRelease,b.Data)==null,"modified FurZap cache is rejected");
   Check(TelegramProxyUpdater.HasEmbedded,"official TG WS Proxy is embedded in FurZap.exe");string embedded=TelegramProxyUpdater.ExtractEmbedded(temp);Check(new FileInfo(embedded).Length==21330255&&AppUpdater.FileHash(embedded)=="b51436e8960307316135e64ac14753b1f3b0e7a46afe1bd6081353b82de20f09","embedded proxy extracts with verified official bytes");
   string fixture=Path.Combine(b.Data,"tools","TgWsProxy_windows.exe");Directory.CreateDirectory(Path.GetDirectoryName(fixture));File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"ping.exe"),fixture,true);
   Process fixtureProcess=null;
   try{
    fixtureProcess=Process.Start(new ProcessStartInfo(fixture,"-n 30 127.0.0.1"){UseShellExecute=false,CreateNoWindow=true});
    Thread.Sleep(350);
    var reopened=new TelegramProxyManager(new Backend(copy,false));
    Check(reopened.Running,"reopened FurZap rediscovers a proxy process in its tools directory");
    reopened.Stop();
    Check(!reopened.Running&&fixtureProcess.WaitForExit(1000),"Stop closes a previously launched proxy process");
   }finally{if(fixtureProcess!=null){if(!fixtureProcess.HasExited)fixtureProcess.Kill();fixtureProcess.Dispose();}File.Delete(fixture);}
   string staged=Path.Combine(b.Data,"tg-downloads","9.0","TgWsProxy_windows.exe");Directory.CreateDirectory(Path.GetDirectoryName(staged));File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"ping.exe"),staged);
   var stagedRelease=new TelegramProxyRelease{Version="9.0",Size=new FileInfo(staged).Length,Digest=AppUpdater.FileHash(staged)};
   File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"where.exe"),fixture);
   string previousDigest=AppUpdater.FileHash(fixture);
   TelegramProxyUpdater.InstallStaged(b,stagedRelease,staged);Check(TelegramProxyUpdater.Matches(fixture,stagedRelease),"downloaded Telegram proxy installs only after explicit apply");
   Check(TelegramProxyUpdater.CanRollback(b.Data),"Telegram proxy update keeps a verified previous executable");
   TelegramProxyUpdater.Rollback(b);Check(AppUpdater.FileHash(fixture)==previousDigest&&!TelegramProxyUpdater.CanRollback(b.Data),"Telegram proxy rollback restores previous bytes");
   stagedRelease.Digest=new string('0',64);Reject(()=>TelegramProxyUpdater.InstallStaged(b,stagedRelease,staged),"tampered Telegram proxy cannot replace installed version");File.Delete(fixture);
   var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;var connection=new TelegramProxyConnection{Host="127.0.0.1",Port=port};Check(TelegramProxyManager.Probe(connection).Ready,"Telegram proxy readiness checks the actual local listener");listener.Stop();Check(!TelegramProxyManager.Probe(connection).Ready,"a running process alone cannot imply an open proxy port");
   foreach(string strategy in b.Strategies){string a=b.Arguments(strategy);Check(a.StartsWith("--wf-tcp=")&&!a.Contains("%")&&!a.Contains("^")&&!a.Contains("\n"),"parse "+strategy);Check(a.Contains("--new")&&a.Contains("--filter-"),"profiles retained: "+strategy);}
   Check(Backend.Ports("443, 1024-65535")=="443,1024-65535","port list normalization");foreach(string v in new[]{"0","65536","443-80","1&calc","-1","01","1,","","1-2-3"})Reject(()=>Backend.Ports(v),"invalid ports: "+v);
   Check(Backend.ValidIp("203.0.113.0/24")&&Backend.ValidIp("2001:db8::/32"),"valid IPv4 and IPv6 networks");foreach(string v in new[]{"999.1.2.3","1.2.3.4/33","1.2.3.4/-1","127.1","2001:db8::/129"})Check(!Backend.ValidIp(v),"invalid IP: "+v);
   Reject(()=>Backend.ValidateList("list-general-user.txt","https://discord.com/a"),"reject URL in domains");Reject(()=>Backend.ValidateList("ipset-all.txt","<html>error</html>"),"reject HTML update");
   Backend.ValidateList("list-general.txt","^dns.google\napi.epicgames.dev\n");Check(true,"official Flowseal caret domain and custom Epic Games domain accepted");
   try{Backend.ValidateList("list-general.txt","^dns.google\nhttps://bad.example/a\n");throw new Exception("Missing validation error");}catch(Exception ex){Check(ex.Message.Contains("строка 2")&&ex.Message.Contains("https://bad.example/a"),"list validation names exact invalid line");}
   string initial=File.ReadAllText(b.P("lists/ipset-all.txt"));b.SetEngineUpdateCheck(false);b.Settings("tcp","443,8080","50000-50100","none");string args=b.Arguments("general.bat");Check(args.Contains("--filter-tcp=443,8080 ")&&args.Contains("--filter-udp=12 "),"TCP-only mode substitution");Check(!b.CheckEngineUpdates,"port settings preserve disabled engine update check");
   b.SetEngineUpdateCheck(true);b.Settings("udp","1024-65535","50000-50100","any");args=b.Arguments("general.bat");Check(args.Contains("--filter-tcp=12 ")&&args.Contains("--filter-udp=50000-50100 "),"UDP-only mode substitution");Check(b.IpMode()=="any","any IP mode");Check(b.CheckEngineUpdates,"port settings preserve enabled engine update check");
   b.Settings("all","1000-2000","3000-4000","loaded");Check(b.IpMode()=="loaded"&&new FileInfo(b.P("lists/ipset-all.txt")).Length>1000,"restore loaded IP list");
   b.Settings("disabled","1000-2000","3000-4000","none");Check(File.ReadAllText(b.P("lists/ipset-all.txt")).Trim()==initial.Trim(),"restore none mode");
   b.EnsureLists();b.SaveList("list-general-user.txt","discord.com\nvrchat.com\n");Check(File.ReadAllText(b.P("lists/list-general-user.txt")).Contains("vrchat.com"),"list write");Check(Directory.GetFiles(Path.Combine(b.Data,"backups")).Length>0,"backups created");Reject(()=>b.SaveList("../../escape.txt","x"),"list path traversal rejected");
   Reject(()=>b.Arguments("../service.bat"),"unknown strategy rejected");
   b.ReplaceFake("ACTIVE_DISCORD_UDP.bin","stun.bin");Check(File.ReadAllBytes(b.P("bin/ACTIVE_DISCORD_UDP.bin")).SequenceEqual(File.ReadAllBytes(b.P("bin/stun.bin"))),"fake replacement exact");
   Check(Backend.Quote("abc")=="\"abc\"","quote plain value");Check(Backend.Quote("C:\\path with spaces\\")=="\"C:\\path with spaces\\\\\"","quote trailing slash");Check(Backend.Quote("a\"b")=="\"a\\\"b\"","escape embedded quote");
   b.Pref["strategy"]="general (ALT).bat";b.SavePrefs();Check(new Backend(copy,true).Get("strategy")=="general (ALT).bat","settings persistence");
   b.SaveProfile("VRChat");Check(b.Profiles().Contains("VRChat"),"named profile saved");Reject(()=>b.SaveProfile("VRChat"),"existing profile cannot be overwritten silently");
   string settingsZip=Path.Combine(temp,"settings.zip");string savedList=File.ReadAllText(b.P("lists/list-general-user.txt"));
   SettingsArchive.Export(b,settingsZip);Check(File.Exists(settingsZip),"settings export creates ZIP");
   b.SaveList("list-general-user.txt","another.example\n");b.Pref["theme"]="cyan";b.SavePrefs();
   SettingsArchive.Import(b,settingsZip);Check(File.ReadAllText(b.P("lists/list-general-user.txt"))==savedList&&b.Get("theme")=="","settings import restores list and preferences");
   Check(b.Profiles().Contains("VRChat"),"settings import restores profiles");
   string unsafeZip=Path.Combine(temp,"settings-unsafe.zip");using(var zip=ZipFile.Open(unsafeZip,ZipArchiveMode.Create)){var entry=zip.CreateEntry("../outside.txt");using(var w=new StreamWriter(entry.Open()))w.Write("unsafe");}
   Reject(()=>SettingsArchive.Import(b,unsafeZip),"settings import rejects ZIP traversal");
   foreach(string name in new[]{"../escape","CON","test/path","","a.b","LPT1"})Reject(()=>Backend.ProfileName(name),"invalid profile name: "+name);
   string snapshot=Path.Combine(b.Data,"profiles","VRChat");string before=File.ReadAllText(b.P("lists/list-general-user.txt"));b.SaveList("list-general-user.txt","changed.example\n");b.RestoreFiles(snapshot);Check(File.ReadAllText(b.P("lists/list-general-user.txt"))==before,"snapshot restores exact list bytes");
   string absent=b.P("lists/list-exclude-user.txt");File.Delete(absent);string absentCopy=b.Checkpoint("absence test");b.EnsureLists();b.RestoreFiles(absentCopy);Check(!File.Exists(absent),"snapshot restores file absence");
   File.AppendAllText(Path.Combine(snapshot,"lists/list-general-user.txt"),"corruption");string unchanged=File.ReadAllText(b.P("lists/list-general-user.txt"));Reject(()=>b.RestoreFiles(snapshot),"tampered snapshot rejected");Check(File.ReadAllText(b.P("lists/list-general-user.txt"))==unchanged,"invalid snapshot leaves live config untouched");
   b.MarkWorking();Check(b.Working()!=null&&b.History().Length>=5,"working checkpoint and automatic history");
   Check(Backend.FriendlyError(new UnauthorizedAccessException("denied")).Contains("администратора"),"actionable access error");
   Check(!ListDocument.Changed("a\r\nb\r\n","a\nb\n"),"newline normalization does not dirty list");
   Check(ListDocument.Changed("a\nb\n","a\nb\nc\n"),"list content edits detected");
   Check(ListDocument.Changed("a\nb\n","a\nb"),"actual trailing newline edit detected");
   var blend=new PoseBlend();blend.Set(1,10,false);blend.Update(10.12,false);Check(blend.Weights[0]>.4f&&blend.Weights[1]>.4f,"dragon transition blends both poses midway");
   float oldWeight=blend.Weights[1];blend.Set(2,10.12,false);Check(Math.Abs(blend.Weights[1]-oldWeight)<.001,"interrupted pose transition remains continuous");blend.Update(11,false);Check(blend.Weights[2]==1&&blend.Weights[0]==0&&blend.Weights[1]==0,"dragon blend reaches target");blend.Set(0,11,true);Check(blend.Weights[0]==1,"reduced motion changes pose immediately");
   ScanTests.Run(copy,Check);ComfortTests.Run(Check,Reject);ApplyTests.Run(copy,Check,Reject);
   Console.WriteLine("ALL "+count+" CHECKS PASSED");
  }finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
 }
}}
