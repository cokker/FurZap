using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
namespace FurZap {
public sealed class EngineRelease {public string Version,Url,Digest,Notes;public long Size;}
public static class EngineUpdater {
 public static EngineRelease Parse(string json){
  var root=ReleaseJson.Parse(json);if((bool)root["draft"]||(bool)root["prerelease"])throw new Exception("Ожидается стабильный релиз движка.");
  string version=(string)root["tag_name"];if(!System.Text.RegularExpressions.Regex.IsMatch(version,@"^v?\d+(\.\d+){1,3}$"))throw new Exception("Неизвестный формат версии движка.");
  var asset=((List<object>)root["assets"]).Cast<Dictionary<string,object>>().SingleOrDefault(x=>((string)x["name"]).EndsWith(".zip",StringComparison.OrdinalIgnoreCase));
  if(asset==null)throw new Exception("В релизе нет единственного ZIP для Windows.");
  string url=(string)asset["browser_download_url"],digest=asset.ContainsKey("digest")?asset["digest"] as string:null;Uri uri;
  if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/Flowseal/zapret-discord-youtube/releases/download/",StringComparison.Ordinal)||uri.Query.Length>0)throw new Exception("Неожиданный источник движка.");
  if(digest==null||!System.Text.RegularExpressions.Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$"))throw new Exception("GitHub не предоставил данные для проверки архива. Обновление отменено.");
  long size=Convert.ToInt64(asset["size"]);if(size<1024||size>50000000)throw new Exception("Неожиданный размер движка.");
  return new EngineRelease{Version=version.TrimStart('v'),Url=url,Digest=digest.Substring(7),Size=size,Notes=root["body"] as string??""};
 }
 public static EngineRelease Latest(Backend b){return Parse(b.Download("https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest"));}
 public static void Extract(string zip,string target){
  Directory.CreateDirectory(target);string prefix=Path.GetFullPath(target)+Path.DirectorySeparatorChar;long total=0;
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  using(var archive=ZipFile.OpenRead(zip)){
   if(archive.Entries.Count>4000)throw new Exception("В архиве слишком много файлов.");
   foreach(var e in archive.Entries){
    string rel=e.FullName.Replace('\\','/');string[] parts=rel.TrimEnd('/').Split('/');
    if(rel.StartsWith("/")||parts.Any(p=>p.Length==0||p=="."||p==".."||p.IndexOfAny(new[]{':','\0'})>=0||p.EndsWith(".")||p.EndsWith(" ")))throw new Exception("Небезопасный путь в архиве.");
    if(((e.ExternalAttributes>>16)&0xF000)==0xA000)throw new Exception("Ссылки в архиве запрещены.");
    string path=Path.GetFullPath(Path.Combine(target,rel.Replace('/',Path.DirectorySeparatorChar)));
    if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!names.Add(path))throw new Exception("Повторяющийся или неверный путь в архиве.");
    if(rel.EndsWith("/")){Directory.CreateDirectory(path);continue;}
    total+=e.Length;if(total>150000000||e.Length>50000000)throw new Exception("Распакованный архив слишком большой.");
    Directory.CreateDirectory(Path.GetDirectoryName(path));using(var input=e.Open())using(var output=File.Create(path)){byte[] buffer=new byte[65536];long count=0;int n;while((n=input.Read(buffer,0,buffer.Length))>0){count+=n;if(count>e.Length)throw new Exception("Неверный размер файла в архиве.");output.Write(buffer,0,n);}}
   }
  }
 }
 public static string Download(Backend b,EngineRelease release,CancellationToken token,Action<int> progress){
  string dir=Path.Combine(b.Data,"engine-downloads",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string zip=Path.Combine(dir,"engine.zip");
  try{
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(release.Url);request.UserAgent="FurZap/1.4";request.Timeout=20000;request.ReadWriteTimeout=20000;
   using(token.Register(()=>request.Abort()))using(var response=request.GetResponse())using(var input=response.GetResponseStream())using(var output=File.Create(zip)){byte[] bytes=new byte[65536];long total=0;int n;while((n=input.Read(bytes,0,bytes.Length))>0){token.ThrowIfCancellationRequested();total+=n;if(total>release.Size)throw new Exception("Размер архива превышен.");output.Write(bytes,0,n);progress((int)(total*100/release.Size));}}
   if(new FileInfo(zip).Length!=release.Size||!String.Equals(AppUpdater.FileHash(zip),release.Digest,StringComparison.OrdinalIgnoreCase))throw new Exception("Архив движка не прошёл проверку целостности.");
   token.ThrowIfCancellationRequested();string unpack=Path.Combine(dir,"unpacked");Extract(zip,unpack);
   string[] roots=Directory.GetFiles(unpack,"winws.exe",SearchOption.AllDirectories).Where(p=>Path.GetFileName(Path.GetDirectoryName(p)).Equals("bin",StringComparison.OrdinalIgnoreCase)).Select(p=>Path.GetDirectoryName(Path.GetDirectoryName(p))).ToArray();
   if(roots.Length!=1)throw new Exception("Не удалось определить папку движка.");Validate(roots[0]);Backend.Atomic(Path.Combine(roots[0],".furzap-version"),release.Version);return roots[0];
  }catch{Directory.Delete(dir,true);token.ThrowIfCancellationRequested();throw;}
 }
 public static void Validate(string root){
  foreach(string rel in new[]{"bin/winws.exe","bin/WinDivert.dll","bin/WinDivert64.sys","bin/cygwin1.dll","lists/ipset-all.txt","service.bat"})if(!File.Exists(Path.Combine(root,rel)))throw new Exception("В комплекте движка отсутствует "+rel);
  var candidate=new Backend(root,true);if(candidate.Strategies.Length==0)throw new Exception("В архиве нет стратегий.");foreach(string strategy in candidate.Strategies)candidate.Arguments(strategy);
 }
 public static void CopyTree(string source,string target){
  if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new Exception("Папка движка не должна быть ссылкой.");Directory.CreateDirectory(target);
  foreach(string file in Directory.GetFiles(source)){if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new Exception("Ссылки в папке движка не поддерживаются.");File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);}
  foreach(string dir in Directory.GetDirectories(source))CopyTree(dir,Path.Combine(target,Path.GetFileName(dir)));
 }
}
public partial class Backend {
 public string EngineVersion{get{string file=P(".furzap-version");return File.Exists(file)?File.ReadAllText(file).Trim():"1.10.3";}}
 public bool CanRollbackEngine{get{return File.Exists(Path.Combine(Data,"previous-engine.txt"));}}
 public string PreviousEngine(){string p=File.ReadAllText(Path.Combine(Data,"previous-engine.txt")).Trim();string prefix=Path.GetFullPath(Path.Combine(Data,"engine-history"))+Path.DirectorySeparatorChar;if(!Path.GetFullPath(p).StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(p))throw new Exception("Копия движка не найдена.");return p;}
 public void RollbackEngine(){ReplaceEngine(PreviousEngine(),false);}
 public void ReplaceEngine(string prepared,bool preserve=true){
  lock(changeGate){
   CheckOwnership();int state=ServiceState();if(state!=0&&state!=1&&state!=4)throw new Exception("Служба меняет состояние. Повтори позже.");bool running=Running;if(running&&state!=0)throw new Exception("Одновременно запущены процесс и служба.");
   string selected=Get("strategy",Strategies[0]),active=ActiveStrategy,command=ServicePath(),serviceStrategy=ServiceStrategy(),oldVersion=EngineVersion;
   string stage=Path.Combine(Data,"engine-stage-"+Guid.NewGuid().ToString("N")),backup=Path.Combine(Data,"engine-history",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"),"engine");
   EngineUpdater.CopyTree(prepared,stage);if(preserve)foreach(string rel in ConfigFiles){string dst=Path.Combine(stage,rel);if(File.Exists(P(rel))){Directory.CreateDirectory(Path.GetDirectoryName(dst));File.Copy(P(rel),dst,true);}else if(File.Exists(dst))File.Delete(dst);}
   EngineUpdater.Validate(stage);var candidate=new Backend(stage,true);if(!candidate.Strategies.Contains(selected))throw new Exception("В новом движке нет выбранной стратегии. Выбери совместимую стратегию и повтори.");candidate.Arguments(selected);
   Directory.CreateDirectory(Path.GetDirectoryName(backup));bool moved=false,installed=false;
   try{
    if(running)Stop();if(state==4)ServiceStop();Directory.Move(Root,backup);moved=true;Directory.Move(stage,Root);installed=true;Strategies=candidate.Strategies;
    if(state!=0){Install(selected,state==4);if(state==4)VerifyActive(true);}else if(running){Start(selected);VerifyActive(false);}
    Atomic(Path.Combine(Data,"previous-engine.txt"),Path.GetFullPath(backup));RecordUpdate("Zapret",oldVersion,EngineVersion,preserve?"Обновлён":"Откат выполнен");Log("Движок применён. Предыдущая версия доступна для отката.");
   }catch(Exception original){
    try{
     if(installed){Stop();if(state!=0)ServiceStop();Directory.Delete(Root,true);}
     if(moved)Directory.Move(backup,Root);Strategies=Directory.GetFiles(Root,"general*.bat").Select(Path.GetFileName).ToArray();
     if(state!=0){RestoreServiceCommand(command,serviceStrategy);if(state==4){ServiceStart();VerifyActive(true);}}else if(running&&!Running){Start(active);VerifyActive(false);}
    }catch(Exception failure){throw new Exception("Обновление не завершено: "+original.Message+"\nОткат не завершён: "+failure.Message+"\nКопия: "+backup,original);}
    RecordUpdate("Zapret",oldVersion,oldVersion,"Ошибка обновления; прежнее состояние восстановлено");throw new Exception("Обновление не применено. Прежний движок и режим восстановлены. "+original.Message,original);
   }finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
  }
 }
}
}
