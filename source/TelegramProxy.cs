using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using System.Reflection;
using System.Text;

namespace FurZap {
public sealed class TelegramProxyRelease {public string Version,Url,Digest;public long Size;}
public sealed class TelegramProxyState {public bool Running,OtherRunning;public string Path;}
public sealed class TelegramProxyConnection {public string Host,Link;public int Port;}
public static class TelegramProxyUpdater {
 const string Api="https://api.github.com/repos/Flowseal/tg-ws-proxy/releases/latest";
 const string EmbeddedName="FurZap.TgWsProxy.exe";
 const string EmbeddedDigest="b51436e8960307316135e64ac14753b1f3b0e7a46afe1bd6081353b82de20f09";
 const long EmbeddedSize=21330255;
 public const string EmbeddedVersion="1.10.4";
 static string VersionFile(string data){return Path.Combine(data,"tools","tg-ws-proxy-version.txt");}
 static void RecordVersion(string data,string version,string digest){Backend.Atomic(VersionFile(data),version+"|"+digest);}
 public static string InstalledVersion(string file,string data){
  if(file==null)return HasEmbedded?EmbeddedVersion:"Не установлен";
  string marker=VersionFile(data);
  if(File.Exists(marker)){string[] parts=File.ReadAllText(marker).Trim().Split('|');if(parts.Length==2&&System.Text.RegularExpressions.Regex.IsMatch(parts[0],@"^\d+(\.\d+){1,3}$")&&System.Text.RegularExpressions.Regex.IsMatch(parts[1],@"^[a-fA-F0-9]{64}$")&&String.Equals(AppUpdater.FileHash(file),parts[1],StringComparison.OrdinalIgnoreCase))return parts[0];}
  if(new FileInfo(file).Length==EmbeddedSize&&String.Equals(AppUpdater.FileHash(file),EmbeddedDigest,StringComparison.OrdinalIgnoreCase))return EmbeddedVersion;
  return "Версия не определена";
 }
 public static bool HasEmbedded{get{return Assembly.GetExecutingAssembly().GetManifestResourceInfo(EmbeddedName)!=null;}}
 public static string ExtractEmbedded(string data){
  string dir=Path.Combine(data,"tools"),target=Path.Combine(dir,"TgWsProxy_windows.exe");
  if(File.Exists(target))return target;
  Directory.CreateDirectory(dir);string temp=Path.Combine(dir,"TgWsProxy-"+Guid.NewGuid().ToString("N")+".download");
  try{using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedName)){
   if(input==null)throw new Exception("В FurZap.exe нет встроенного TG WS Proxy. Установи полный ZIP или скачай прокси в разделе Инструменты.");
   using(var output=File.Create(temp))input.CopyTo(output);
  }
   if(new FileInfo(temp).Length!=EmbeddedSize||!String.Equals(AppUpdater.FileHash(temp),EmbeddedDigest,StringComparison.OrdinalIgnoreCase))throw new Exception("Встроенный TG WS Proxy не прошёл проверку целостности.");
   if(File.Exists(target))return target;File.Move(temp,target);RecordVersion(data,EmbeddedVersion,EmbeddedDigest);return target;
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static TelegramProxyRelease Parse(string json){
  var root=ReleaseJson.Parse(json);if((bool)root["draft"]||(bool)root["prerelease"])throw new Exception("Ожидается опубликованный релиз TG WS Proxy.");
  string version=(string)root["tag_name"];if(!Regex.IsMatch(version,@"^v\d+(\.\d+){1,3}$"))throw new Exception("Неизвестная версия TG WS Proxy.");
  var asset=((List<object>)root["assets"]).Cast<Dictionary<string,object>>().SingleOrDefault(x=>(string)x["name"]=="TgWsProxy_windows.exe");
  if(asset==null)throw new Exception("В релизе TG WS Proxy нет Windows x64 EXE.");
  string url=(string)asset["browser_download_url"],digest=asset.ContainsKey("digest")?asset["digest"] as string:null;Uri uri;
  if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="github.com"||uri.AbsolutePath!="/Flowseal/tg-ws-proxy/releases/download/"+version+"/TgWsProxy_windows.exe"||uri.Query.Length>0||uri.Fragment.Length>0)throw new Exception("Неожиданный адрес TG WS Proxy.");
  if(digest==null||!Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$"))throw new Exception("GitHub не предоставил SHA-256 для TG WS Proxy.");
  long size=Convert.ToInt64(asset["size"]);if(size<1024||size>100000000)throw new Exception("Неожиданный размер TG WS Proxy.");
  return new TelegramProxyRelease{Version=version.Substring(1),Url=url,Digest=digest.Substring(7),Size=size};
 }
 public static TelegramProxyRelease Latest(Backend b){return Parse(b.Download(Api));}
 public static bool Matches(string file,TelegramProxyRelease release){return File.Exists(file)&&new FileInfo(file).Length==release.Size&&String.Equals(AppUpdater.FileHash(file),release.Digest,StringComparison.OrdinalIgnoreCase);}
 public static string Stage(Backend b,TelegramProxyRelease release,CancellationToken token,Action<int> progress){
  string dir=Path.Combine(b.Data,"tg-downloads",release.Version),target=Path.Combine(dir,"TgWsProxy_windows.exe");
  Directory.CreateDirectory(dir);if(Matches(target,release))return target;
  string temp=Path.Combine(dir,Guid.NewGuid().ToString("N")+".download");
  try{
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(release.Url);request.UserAgent="FurZap/1.5";request.Timeout=20000;request.ReadWriteTimeout=20000;
   using(token.Register(()=>request.Abort()))using(var response=request.GetResponse())using(var input=response.GetResponseStream())using(var output=File.Create(temp)){
    byte[] bytes=new byte[65536];long total=0;int n;while((n=input.Read(bytes,0,bytes.Length))>0){token.ThrowIfCancellationRequested();total+=n;if(total>release.Size)throw new Exception("Размер TG WS Proxy превышен.");output.Write(bytes,0,n);progress((int)(total*100/release.Size));}
   }
   token.ThrowIfCancellationRequested();if(!Matches(temp,release))throw new Exception("Файл TG WS Proxy не прошёл проверку SHA-256.");
   if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);return target;
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static string StagedPath(Backend b,TelegramProxyRelease release){return Path.Combine(b.Data,"tg-downloads",release.Version,"TgWsProxy_windows.exe");}
 public static void InstallStaged(Backend b,TelegramProxyRelease release,string staged){
  if(!Matches(staged,release))throw new Exception("Скачанный TG WS Proxy повреждён. Повтори загрузку.");
  string dir=Path.Combine(b.Data,"tools"),target=Path.Combine(dir,"TgWsProxy_windows.exe");Directory.CreateDirectory(dir);
  string temp=Path.Combine(dir,Guid.NewGuid().ToString("N")+".install");
  try{File.Copy(staged,temp);if(!Matches(temp,release))throw new Exception("Проверка TG WS Proxy при установке не пройдена.");
   if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);RecordVersion(b.Data,release.Version,release.Digest);
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static string Download(Backend b,TelegramProxyRelease release,CancellationToken token,Action<int> progress){
  string dir=Path.Combine(b.Data,"tools");Directory.CreateDirectory(dir);string target=Path.Combine(dir,"TgWsProxy_windows.exe");
  if(Matches(target,release)){RecordVersion(b.Data,release.Version,release.Digest);return target;}
  string temp=Path.Combine(dir,"TgWsProxy-"+Guid.NewGuid().ToString("N")+".download");
  try{
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(release.Url);request.UserAgent="FurZap/1.4.1";request.Timeout=20000;request.ReadWriteTimeout=20000;
   using(token.Register(()=>request.Abort()))using(var response=request.GetResponse())using(var input=response.GetResponseStream())using(var output=File.Create(temp)){
    byte[] bytes=new byte[65536];long total=0;int n;while((n=input.Read(bytes,0,bytes.Length))>0){token.ThrowIfCancellationRequested();total+=n;if(total>release.Size)throw new Exception("Размер TG WS Proxy превышен.");output.Write(bytes,0,n);progress((int)(total*100/release.Size));}
   }
   token.ThrowIfCancellationRequested();if(!Matches(temp,release))throw new Exception("Файл TG WS Proxy не прошёл проверку SHA-256.");
   if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);RecordVersion(b.Data,release.Version,release.Digest);return target;
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
public sealed class TelegramProxyManager {
 readonly Backend backend;
 public TelegramProxyManager(Backend b){backend=b;}
 public string Bundled{get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools","TgWsProxy_windows.exe");}}
 public string Installed{get{return Path.Combine(backend.Data,"tools","TgWsProxy_windows.exe");}}
 public string Executable{get{return File.Exists(Installed)?Installed:File.Exists(Bundled)?Bundled:null;}}
 public bool Available{get{return Executable!=null||TelegramProxyUpdater.HasEmbedded;}}
 public TelegramProxyState State(){
  var state=new TelegramProxyState();var processes=ProxyProcesses(false);
  try{foreach(var process in processes)try{
   var module=process.MainModule;string path=module==null?null:module.FileName;
   if(ManagedPath(path,Installed,Bundled)){state.Running=true;state.Path=path;}
   else state.OtherRunning=true;
  }catch(Win32Exception){state.OtherRunning=true;}catch(InvalidOperationException){}
  }finally{foreach(var process in processes)process.Dispose();}
  return state;
 }
 public string ConfigFile{
  get{
   string exe=Executable;string portable=exe==null?null:Path.Combine(Path.GetDirectoryName(exe),"TgWsProxy_data");
   return Path.Combine(portable!=null&&Directory.Exists(portable)?portable:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"TgWsProxy"),"config.json");
  }
 }
 internal static TelegramProxyConnection ParseConnection(string json){
  var config=ReleaseJson.Parse(json);object h,p,s;
  if(!config.TryGetValue("host",out h)||!config.TryGetValue("port",out p)||!config.TryGetValue("secret",out s))throw new Exception("Настройка TG WS Proxy ещё не завершена. Открой его значок в трее и проверь параметры.");
  string host=h as string,secret=s as string;int port;
  if(host=="0.0.0.0")host="127.0.0.1";if(host=="::")host="::1";
  double number=Convert.ToDouble(p);if(number!=Math.Floor(number)||number<1||number>65535||String.IsNullOrWhiteSpace(host)||host.Length>255||!System.Text.RegularExpressions.Regex.IsMatch(host,@"^[a-zA-Z0-9.:_-]+$")||secret==null||!System.Text.RegularExpressions.Regex.IsMatch(secret,@"^[a-fA-F0-9]{32}$"))throw new Exception("Параметры TG WS Proxy некорректны. Проверь адрес, порт и секрет в его настройках.");
  port=(int)number;
  return new TelegramProxyConnection{Host=host,Port=port,Link="tg://proxy?server="+Uri.EscapeDataString(host)+"&port="+port+"&secret=dd"+secret};
 }
 public TelegramProxyConnection Connection(){
  string path=ConfigFile;if(!File.Exists(path))throw new Exception("Сначала запусти TG WS Proxy и заверши его первоначальную настройку в трее.");
  if(new FileInfo(path).Length>65536)throw new Exception("Файл настроек TG WS Proxy слишком большой.");
  return ParseConnection(File.ReadAllText(path,Encoding.UTF8));
 }
 internal static bool ManagedPath(string actual,string installed,string bundled){
  if(String.IsNullOrEmpty(actual))return false;
  string path=Path.GetFullPath(actual);
  return String.Equals(path,Path.GetFullPath(installed),StringComparison.OrdinalIgnoreCase)||String.Equals(path,Path.GetFullPath(bundled),StringComparison.OrdinalIgnoreCase);
 }
 Process[] ProxyProcesses(bool managedOnly){
  var found=new List<Process>();
  foreach(var process in Process.GetProcesses()){
   bool keep=false;
   try{
    if(process.ProcessName.StartsWith("TgWsProxy",StringComparison.OrdinalIgnoreCase)){
     if(!managedOnly)keep=true;
     else{var module=process.MainModule;keep=module!=null&&ManagedPath(module.FileName,Installed,Bundled);}
    }
   }catch(Win32Exception){}catch(InvalidOperationException){}
   if(keep)found.Add(process);else process.Dispose();
  }
  return found.ToArray();
 }
 public bool Running{get{var processes=ProxyProcesses(true);foreach(var process in processes)process.Dispose();return processes.Length>0;}}
 public void Start(){
  if(backend.Preview)throw new Exception("Запуск доступен только в Windows.");
  if(Running)throw new Exception("TG WS Proxy уже запущен из папки FurZap.");
  var existing=ProxyProcesses(false);foreach(var process in existing)process.Dispose();
  if(existing.Length>0)throw new Exception("TG WS Proxy уже работает из другой папки. Заверши его через значок в системном трее.");
  string file=Executable??TelegramProxyUpdater.ExtractEmbedded(backend.Data);
  using(var started=Process.Start(new ProcessStartInfo(file){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(file)}))
   if(started==null)throw new Exception("Windows не запустила TG WS Proxy.");
  Thread.Sleep(800);if(!Running)throw new Exception("TG WS Proxy завершился сразу после запуска. Проверь его журнал в трее или папке программы.");
 }
 public void Stop(){
  if(backend.Preview)throw new Exception("Остановка доступна только в Windows.");
  bool found=false;
  for(int attempt=0;attempt<3;attempt++){
   var processes=ProxyProcesses(true);
   if(processes.Length==0){if(!found)throw new Exception("TG WS Proxy из папки FurZap не запущен.");return;}
   found=true;
   try{foreach(var process in processes)try{
     if(process.HasExited)continue;
     if(!process.CloseMainWindow()||!process.WaitForExit(1500)){
      process.Kill();if(!process.WaitForExit(5000))throw new Exception("TG WS Proxy не завершился после команды остановки.");
     }
    }catch(InvalidOperationException){/* Процесс завершился сам. */}
     catch(Win32Exception ex){throw new Exception("Не удалось остановить TG WS Proxy. Запусти FurZap с теми же правами, что и прокси, либо заверши прокси через его трей.",ex);}
   }finally{foreach(var process in processes)process.Dispose();}
   Thread.Sleep(200);
  }
  if(Running)throw new Exception("TG WS Proxy продолжает работать. Заверши его через значок в системном трее.");
 }
 public void InstallLatest(CancellationToken token,Action<int> progress){if(Running)throw new Exception("Сначала закрой TG WS Proxy через его значок в трее, затем обнови.");var release=TelegramProxyUpdater.Latest(backend);TelegramProxyUpdater.Download(backend,release,token,progress);}
}
}
