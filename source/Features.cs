using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace FurZap {
public sealed class ProbeResult {
 public string Service,Detail;public bool Ok,Tls;public long Milliseconds;
 public override string ToString(){return Service+": "+(Ok?"HTTPS доступен":Tls?"TLS установлен, HTTP-ошибка":"нет подтверждения HTTPS")+" · "+Milliseconds+" мс · "+Detail;}
}
public sealed class StrategyResult {
 public string Strategy,Error="";public ProbeResult[] Checks=new ProbeResult[0];
 public int Score{get{return Checks.Count(x=>x.Ok);}}
 public long Duration{get{return Checks.Sum(x=>x.Milliseconds);}}
 public override string ToString(){return Path.GetFileNameWithoutExtension(Strategy)+" — "+Score+"/3"+(Error.Length>0?" · "+Error:"");}
}
public partial class Backend {
 static readonly string[] ConfigFiles={"utils/game_filter.enabled","utils/check_updates.enabled","lists/list-general-user.txt","lists/list-exclude-user.txt","lists/ipset-exclude-user.txt","lists/list-general.txt","lists/list-exclude.txt","lists/list-google.txt","lists/ipset-all.txt","lists/ipset-all.txt.backup","lists/ipset-exclude.txt","bin/ACTIVE_DISCORD_UDP.bin","bin/ACTIVE_GAME_UDP.bin"};
 static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
 public static string ProfileName(string name){name=name.Trim();if(!Regex.IsMatch(name,@"^[\p{L}\p{N} _()\-]{1,48}$")||new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(name.ToUpperInvariant()))throw new Exception("Имя профиля: 1–48 букв, цифр, пробелов, скобок или дефисов.");return name;}
 public void Capture(string dir,string reason){
  if(Directory.Exists(dir))throw new Exception("Такая копия уже существует.");Directory.CreateDirectory(dir);try{var manifest=new List<string>();foreach(string rel in ConfigFiles){string src=P(rel);if(File.Exists(src)){byte[] bytes=File.ReadAllBytes(src);string dst=Path.Combine(dir,rel);Directory.CreateDirectory(Path.GetDirectoryName(dst));File.WriteAllBytes(dst,bytes);manifest.Add(rel+"|"+Hash(bytes));}else manifest.Add(rel+"|-");}Atomic(Path.Combine(dir,"manifest.txt"),String.Join("\n",manifest));Atomic(Path.Combine(dir,"strategy.txt"),Get("strategy",Strategies[0]));Atomic(Path.Combine(dir,"description.txt"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" · "+reason);}catch{Directory.Delete(dir,true);throw;}
 }
 public string Checkpoint(string reason){string dir=Path.Combine(Data,"history",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Capture(dir,reason);return dir;}
 public string[] Profiles(){string dir=Path.Combine(Data,"profiles");return Directory.Exists(dir)?Directory.GetDirectories(dir).Select(Path.GetFileName).OrderBy(x=>x).ToArray():new string[0];}
 public string[] History(){string dir=Path.Combine(Data,"history");return Directory.Exists(dir)?Directory.GetDirectories(dir).OrderByDescending(x=>x).ToArray():new string[0];}
 public string Description(string dir){return File.ReadAllText(Path.Combine(dir,"description.txt"));}
 public void SaveProfile(string name){name=ProfileName(name);Capture(Path.Combine(Data,"profiles",name),"Профиль: "+name);Log("Профиль сохранён: "+name);}
 public void MarkWorking(){Checkpoint("★ Рабочая конфигурация — отмечена пользователем");Log("Текущая конфигурация отмечена как рабочая.");}
 public string Working(){return History().FirstOrDefault(x=>Description(x).Contains("★ Рабочая"));}
 Dictionary<string,byte[]> ReadSnapshot(string dir,out string strategy){
  var data=new Dictionary<string,byte[]>();string[] lines=File.ReadAllLines(Path.Combine(dir,"manifest.txt"));if(lines.Length!=ConfigFiles.Length)throw new Exception("Копия повреждена: неполный манифест.");foreach(string line in lines){string[] p=line.Split(new char[]{'|'});if(p.Length!=2||!ConfigFiles.Contains(p[0])||data.ContainsKey(p[0]))throw new Exception("Копия повреждена: неизвестный файл.");byte[] bytes=p[1]=="-"?null:File.ReadAllBytes(Path.Combine(dir,p[0]));if(bytes!=null&&Hash(bytes)!=p[1])throw new Exception("Копия повреждена: контрольная сумма "+p[0]);data.Add(p[0],bytes);}strategy=File.ReadAllText(Path.Combine(dir,"strategy.txt")).Trim();if(!Strategies.Contains(strategy))throw new Exception("Стратегия из копии отсутствует в этой сборке.");return data;
 }
 public void RestoreFiles(string dir){string strategy;var data=ReadSnapshot(dir,out strategy);foreach(var entry in data){string dst=P(entry.Key);if(entry.Value==null){if(File.Exists(dst))File.Delete(dst);}else{Directory.CreateDirectory(Path.GetDirectoryName(dst));File.WriteAllBytes(dst,entry.Value);}}Pref["strategy"]=strategy;SavePrefs();}
 public void ApplySnapshot(string dir){
  string desired;ReadSnapshot(dir,out desired);CheckOwnership();int state=ServiceState();if(state!=0&&state!=1&&state!=4)throw new Exception("Служба меняет состояние. Подожди и повтори.");bool running=Running;string active=ActiveStrategy,servicePath=ServicePath();string recovery=Checkpoint("Перед восстановлением: "+Description(dir));
  try{Stop();if(state!=0)ServiceStop();RestoreFiles(dir);if(state!=0)Install(desired,state==4);else if(running)Start(desired);Log("Восстановлено: "+Description(dir));}
  catch(Exception original){try{Stop();if(state!=0)ServiceStop();RestoreFiles(recovery);if(state!=0){Run("sc.exe","config zapret binPath= "+Quote(servicePath));if(state==4)ServiceStart();}else if(running)Start(active);}catch(Exception recoveryError){throw new Exception("Восстановление не завершено: "+original.Message+"\nНе удалось вернуть прежнее состояние: "+recoveryError.Message+"\nКопия: "+recovery,original);}throw;}
 }
 public void ApplyProfile(string name){ApplySnapshot(Path.Combine(Data,"profiles",ProfileName(name)));Pref["profile"]=name;SavePrefs();}
 public virtual ProbeResult[] ProbeServices(CancellationToken token){if(Preview)throw new Exception("Сетевые проверки отключены в предпросмотре.");var targets=new[]{new[]{"Discord","https://discord.com/"},new[]{"YouTube","https://www.youtube.com/"},new[]{"VRChat","https://vrchat.com/"}};return Task.WhenAll(targets.Select(t=>Task.Run(()=>Probe(t[0],t[1],token),token))).GetAwaiter().GetResult();}
 static ProbeResult Probe(string name,string url,CancellationToken token){
  token.ThrowIfCancellationRequested();var r=new ProbeResult{Service=name};var watch=Stopwatch.StartNew();ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var req=(HttpWebRequest)WebRequest.Create(url);req.Method="GET";req.Timeout=7000;req.ReadWriteTimeout=7000;req.UserAgent="FurZap/1.2";req.AllowAutoRedirect=false;req.KeepAlive=false;req.Proxy=null;
  using(token.Register(()=>req.Abort())){try{using(var response=(HttpWebResponse)req.GetResponse()){int code=(int)response.StatusCode;r.Tls=true;r.Ok=code>=200&&code<400;r.Detail="HTTP "+code+" · "+url;}}catch(WebException ex){token.ThrowIfCancellationRequested();using(var response=ex.Response as HttpWebResponse){r.Tls=response!=null;r.Detail=response!=null?"HTTP "+(int)response.StatusCode:ex.Status.ToString();}}finally{r.Milliseconds=watch.ElapsedMilliseconds;}}return r;
 }
 public List<StrategyResult> ScanStrategies(CancellationToken token,Action<int,int,string> progress){
  if(Preview)throw new Exception("Подбор отключён в предпросмотре.");CheckOwnership();int state=ServiceState();if(state!=0&&state!=1&&state!=4)throw new Exception("Служба меняет состояние. Повтори после её остановки или запуска.");bool running=Running;string active=ActiveStrategy;string snapshot=Path.Combine(Data,"scan-recovery",Guid.NewGuid().ToString("N"));Capture(snapshot,"До подбора стратегий");var results=new List<StrategyResult>();
  try{Stop();if(state!=0)ServiceStop();for(int i=0;i<Strategies.Length;i++){token.ThrowIfCancellationRequested();string strategy=Strategies[i];progress(i,Strategies.Length,strategy);var result=new StrategyResult{Strategy=strategy};try{Start(strategy,true);token.ThrowIfCancellationRequested();result.Checks=ProbeServices(token);}catch(OperationCanceledException){throw;}catch(Exception ex){result.Error=ex.Message;}finally{Stop();}results.Add(result);progress(i+1,Strategies.Length,result.ToString());}}
  finally{progress(results.Count,Strategies.Length,"Возвращаю прежнюю конфигурацию…");try{Stop();RestoreFiles(snapshot);if(state==4)ServiceStart();else if(running)Start(active,true);Directory.Delete(snapshot,true);}catch(Exception ex){throw new Exception("Не удалось вернуть прежнее состояние после теста. Копия: "+snapshot+"\n"+ex.Message,ex);}finally{string report=String.Join("\r\n\r\n",results.Select(x=>x+"\r\n"+String.Join("\r\n",x.Checks.Select(c=>c.ToString()))));Atomic(Path.Combine(Data,"last-scan.txt"),DateTime.Now+"\r\n"+report);}}
  return results.OrderByDescending(x=>x.Score).ThenBy(x=>x.Duration).ToList();
 }
 public static string FriendlyError(Exception ex){string s=ex.Message;string help=ex is FileNotFoundException||ex is DirectoryNotFoundException?"Распакуй полный ZIP FurZap. Проверь карантин антивируса и путь к папке engine.":ex is System.ComponentModel.Win32Exception?"Проверь права администратора и журнал Windows. Если файл занят, закрой прежний экземпляр Zapret и повтори.":ex is UnauthorizedAccessException?"Закрой программу и запусти FurZap.exe от имени администратора. Проверь доступ к папке приложения.":ex is WebException?"Проверь соединение и повтори. Подробности сетевой ошибки сохранены в журнале.":s.IndexOf("winws завершился",StringComparison.OrdinalIgnoreCase)>=0?"Открой Журнал: там будет ответ winws. Проверь, что архив распакован полностью и драйвер не заблокирован.":s.IndexOf("другой winws",StringComparison.OrdinalIgnoreCase)>=0?"Останови прежний экземпляр Zapret и повтори запуск.":s.IndexOf("служба zapret",StringComparison.OrdinalIgnoreCase)>=0?"Открой раздел «Служба» и проверь её состояние и папку установки.":s.IndexOf("контрольная сумма",StringComparison.OrdinalIgnoreCase)>=0?"Выбери другую копию из истории. Повреждённая копия не применена.":"Подробности можно скопировать из раздела «Журнал».";return s+"\r\n\r\nЧто сделать: "+help;}
}
}

