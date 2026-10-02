using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Reflection;

namespace FurZap {
public sealed class TelegramProxyRelease {public string Version,Url,Digest;public long Size;}
public static class TelegramProxyUpdater {
 const string Api="https://api.github.com/repos/Flowseal/tg-ws-proxy/releases/latest";
 const string EmbeddedName="FurZap.TgWsProxy.exe";
 const string EmbeddedDigest="b51436e8960307316135e64ac14753b1f3b0e7a46afe1bd6081353b82de20f09";
 const long EmbeddedSize=21330255;
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
   if(File.Exists(target))return target;File.Move(temp,target);return target;
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
 public static string Download(Backend b,TelegramProxyRelease release,CancellationToken token,Action<int> progress){
  string dir=Path.Combine(b.Data,"tools");Directory.CreateDirectory(dir);string target=Path.Combine(dir,"TgWsProxy_windows.exe");
  if(Matches(target,release))return target;
  string temp=Path.Combine(dir,"TgWsProxy-"+Guid.NewGuid().ToString("N")+".download");
  try{
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(release.Url);request.UserAgent="FurZap/1.4.1";request.Timeout=20000;request.ReadWriteTimeout=20000;
   using(token.Register(()=>request.Abort()))using(var response=request.GetResponse())using(var input=response.GetResponseStream())using(var output=File.Create(temp)){
    byte[] bytes=new byte[65536];long total=0;int n;while((n=input.Read(bytes,0,bytes.Length))>0){token.ThrowIfCancellationRequested();total+=n;if(total>release.Size)throw new Exception("Размер TG WS Proxy превышен.");output.Write(bytes,0,n);progress((int)(total*100/release.Size));}
   }
   token.ThrowIfCancellationRequested();if(!Matches(temp,release))throw new Exception("Файл TG WS Proxy не прошёл проверку SHA-256.");
   if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);return target;
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
public sealed class TelegramProxyManager {
 readonly Backend backend;Process owned;
 public TelegramProxyManager(Backend b){backend=b;}
 public string Bundled{get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools","TgWsProxy_windows.exe");}}
 public string Installed{get{return Path.Combine(backend.Data,"tools","TgWsProxy_windows.exe");}}
 public string Executable{get{return File.Exists(Installed)?Installed:File.Exists(Bundled)?Bundled:null;}}
 public bool Available{get{return Executable!=null||TelegramProxyUpdater.HasEmbedded;}}
 public bool Running{get{try{return owned!=null&&!owned.HasExited;}catch{return false;}}}
 public void Start(){
  if(backend.Preview)throw new Exception("Запуск доступен только в Windows.");
  if(Running)throw new Exception("TG WS Proxy уже запущен из FurZap.");
  if(Process.GetProcessesByName("TgWsProxy_windows").Length>0)throw new Exception("TG WS Proxy уже работает. Найди его значок в системном трее.");
  string file=Executable??TelegramProxyUpdater.ExtractEmbedded(backend.Data);
  owned=Process.Start(new ProcessStartInfo(file){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(file)});
  if(owned==null)throw new Exception("Windows не запустила TG WS Proxy.");
  System.Threading.Thread.Sleep(800);if(!Running)throw new Exception("TG WS Proxy завершился сразу после запуска. Проверь его журнал в трее или папке программы.");
 }
 public void Stop(){if(!Running)throw new Exception("TG WS Proxy не запущен из этого окна FurZap. Если он открыт отдельно, заверши его через собственный трей.");
  if(!owned.CloseMainWindow()||!owned.WaitForExit(2000)){owned.Kill();if(!owned.WaitForExit(5000))throw new Exception("Не удалось остановить TG WS Proxy.");}owned.Dispose();owned=null;
 }
 public void InstallLatest(CancellationToken token,Action<int> progress){if(Running)throw new Exception("Сначала закрой TG WS Proxy через его значок в трее, затем обнови.");var release=TelegramProxyUpdater.Latest(backend);TelegramProxyUpdater.Download(backend,release,token,progress);}
}
}
