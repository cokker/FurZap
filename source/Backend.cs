using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Win32;

namespace FurZap {
public partial class Backend {
 public readonly string Root, Data;
 public readonly bool Preview;
 public Action<string> Log = delegate{};
 public Process Child;
 public string ActiveStrategy = "";
 public Dictionary<string,string> Pref = new Dictionary<string,string>();
 public string[] Strategies;
 public const string Repo = "https://github.com/Flowseal/zapret-discord-youtube";
 public Backend(string root, bool preview) {
  Root=root; Preview=preview; Data=Path.Combine(root,"..","data"); Directory.CreateDirectory(Data);
  Strategies=Directory.GetFiles(root,"general*.bat").Select(Path.GetFileName).OrderBy(x=>x=="general.bat"?0:1).ThenBy(x=>Regex.Replace(x,@"\d+",m=>m.Value.PadLeft(5,'0'))).ToArray();
  string pref=Path.Combine(Data,"preferences.txt"); if(File.Exists(pref)) foreach(string l in File.ReadAllLines(pref)) {int p=l.IndexOf('='); if(p>0)Pref[l.Substring(0,p)]=l.Substring(p+1);}
 }
 public string Get(string key,string fallback="") { return Pref.ContainsKey(key)?Pref[key]:fallback; }
 public void SavePrefs(){Atomic(Path.Combine(Data,"preferences.txt"),String.Join("\r\n",Pref.Select(x=>x.Key+"="+x.Value)));}
 public static void Atomic(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp=path+".new"; File.WriteAllText(temp,text,new UTF8Encoding(false)); if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
 public void Backup(string file) {if(File.Exists(file)){string dir=Path.Combine(Data,"backups");Directory.CreateDirectory(dir);File.Copy(file,Path.Combine(dir,Path.GetFileName(file)+"."+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".bak"),true);}}
 public string P(string rel){return Path.Combine(Root,rel.Replace('/',Path.DirectorySeparatorChar));}
 public static string Quote(string s) {return "\""+Regex.Replace(s,@"(\\*)\""", "$1$1\\\"")+Regex.Match(s,@"\\*$").Value+"\"";}
 public static string Ports(string s) {
  s=s.Replace(" ",""); if(s.Length==0)throw new Exception("Укажи порты, например 1024-65535 или 443,50000-50100.");
  foreach(string item in s.Split(new char[] {','})){if(!Regex.IsMatch(item,@"^[1-9][0-9]{0,4}(-[1-9][0-9]{0,4})?$"))throw new Exception("Неверный диапазон портов: "+item);int[] n=item.Split(new char[] {'-'}).Select(int.Parse).ToArray(); if(n.Any(x=>x>65535)||(n.Length==2&&n[0]>n[1]))throw new Exception("Порты должны быть от 1 до 65535, по возрастанию.");}return s;
 }
 public Dictionary<string,string> Game(){var d=new Dictionary<string,string>{{"mode","disabled"},{"tcp","1024-65535"},{"udp","1024-65535"}}; if(File.Exists(P("utils/game_filter.enabled")))foreach(string l in File.ReadAllLines(P("utils/game_filter.enabled"))){var v=l.Split(new char[] {'='});if(v.Length==2&&d.ContainsKey(v[0]))d[v[0]]=v[1];else if(v.Length==1&&new[]{"all","tcp","udp"}.Contains(l))d["mode"]=l;}return d;}
 public string IpMode(){string s=File.ReadAllText(P("lists/ipset-all.txt"));return String.IsNullOrWhiteSpace(s)?"any":s.Contains("203.0.113.113/32")?"none":"loaded";}
 public void Settings(string mode,string tcp,string udp,string ipmode,bool update){
  tcp=Ports(tcp);udp=Ports(udp);if(!new[]{"disabled","all","tcp","udp"}.Contains(mode))throw new Exception("Неизвестный режим.");
  string file=P("lists/ipset-all.txt"), bak=file+".backup", old=IpMode();
  if(ipmode=="loaded"&&old!="loaded"&&!File.Exists(bak))throw new Exception("Нет сохранённого IPSet. Сначала обнови список на странице Инструменты.");
  Checkpoint("Настройки портов и IPSet");Backup(P("utils/game_filter.enabled"));Atomic(P("utils/game_filter.enabled"),"mode="+mode+"\r\ntcp="+tcp+"\r\nudp="+udp+"\r\n");
  if(old!=ipmode){Backup(file);if(old=="loaded")File.Copy(file,bak,true);if(ipmode=="loaded")Atomic(file,File.ReadAllText(bak));else Atomic(file,ipmode=="none"?"203.0.113.113/32\r\n":"");}
  if(update)Atomic(P("utils/check_updates.enabled"),"enabled\r\n");else if(File.Exists(P("utils/check_updates.enabled")))File.Delete(P("utils/check_updates.enabled"));
  Log("Настройки сохранены. Для работающего процесса нужен перезапуск; для службы — повторное применение стратегии.");
 }
 public void EnsureLists(){foreach(string name in new[]{"list-general-user.txt","list-exclude-user.txt","ipset-exclude-user.txt"}){string p=P("lists/"+name);if(!File.Exists(p))Atomic(p,name.StartsWith("ipset")?"203.0.113.113/32\r\n":"domain.example.abc\r\n");}}
 public string Arguments(string strategy){
  if(!Strategies.Contains(strategy))throw new Exception("Стратегия отсутствует в этой сборке.");
  string s=File.ReadAllText(P(strategy)); var m=Regex.Match(s,@"(?im)^start\s+[^\r\n]*?winws\.exe\""\s+",RegexOptions.IgnoreCase);
  if(!m.Success)throw new Exception("Не удалось прочитать команду winws: "+strategy);
  s=s.Substring(m.Index+m.Length);s=Regex.Replace(s,@"\^\r?\n", " ").Replace("^!","!").Trim();
  if(s.Contains("\n")||s.Contains("\r")||s.IndexOfAny(new[]{'&','|','<','>'})>=0)throw new Exception("Неожиданные команды в стратегии. Открой её через исходный менеджер.");
  var g=Game();string tcp=(g["mode"]=="all"||g["mode"]=="tcp")?Ports(g["tcp"]):"12",udp=(g["mode"]=="all"||g["mode"]=="udp")?Ports(g["udp"]):"12";
  var vars=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"BIN",P("bin/")},{"LISTS",P("lists/")},{"GameFilterTCP",tcp},{"GameFilterUDP",udp},{"GameFilter",g["mode"]=="udp"?udp:tcp}};
  s=Regex.Replace(s,@"%([^%]+)%",x=>{if(!vars.ContainsKey(x.Groups[1].Value))throw new Exception("Неизвестная переменная: "+x.Value);return vars[x.Groups[1].Value];});
  if(!s.StartsWith("--wf-"))throw new Exception("Некорректная стратегия.");return s;
 }
 public virtual bool Running {get {try{return Child!=null&&!Child.HasExited;}catch{return false;}}}
 public virtual int ServiceState(){if(Preview)return 0;try{using(var s=new ServiceController("zapret"))return (int)s.Status;}catch{return 0;}}
 public virtual string ServicePath(){if(Preview)return "";using(var k=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\zapret"))return k==null?"":Convert.ToString(k.GetValue("ImagePath"));}
 public virtual void CheckOwnership(){string p=ServicePath();if(p.Length>0&&!p.StartsWith(Quote(P("bin/winws.exe"))+" ",StringComparison.OrdinalIgnoreCase))throw new Exception("Служба zapret принадлежит другой папке. Удали её через прежний service.bat, затем установи здесь. Чужая служба не изменена.");}
 public string Run(string exe,string args,bool check=true){
  if(Preview){Log("Предпросмотр: системная команда не выполняется.");return "";}
  var si=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Root};
  if(Environment.OSVersion.Platform==PlatformID.Win32NT){si.StandardOutputEncoding=Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);si.StandardErrorEncoding=si.StandardOutputEncoding;}
  using(var p=new Process{StartInfo=si}){p.Start();var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(20000)){try{p.Kill();}catch{}throw new Exception("Команда не ответила за 20 секунд: "+exe);}string output=stdout.Result+stderr.Result;Log(exe+" → "+p.ExitCode+"\r\n"+output.Trim());if(check&&p.ExitCode!=0)throw new Exception(exe+": код "+p.ExitCode+"\n"+output);return output;}
 }
 public virtual void Start(string strategy,bool testing=false){
  if(Preview)throw new Exception("Это предпросмотр интерфейса; движок запускается только в Windows.");
  if(Running)throw new Exception("Процесс уже запущен. Сначала останови его.");
  if(testing)CheckOwnership();if(ServiceState()!=0&&!(testing&&ServiceState()==1))throw new Exception("Установлена служба zapret. Запусти её со страницы Служба или удали перед обычным запуском.");
  if(Process.GetProcessesByName("winws").Length>0)throw new Exception("Уже работает другой winws.exe. Останови его в прежнем окне Zapret.");
  EnsureLists();string args=Arguments(strategy);if(!testing)Run("netsh.exe","interface tcp set global timestamps=enabled");
  if(Child!=null)Child.Dispose();Child=new Process{StartInfo=new ProcessStartInfo(P("bin/winws.exe"),args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=P("bin")}};
  Child.OutputDataReceived+=(s,e)=>{if(e.Data!=null)Log(e.Data);};Child.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Log(e.Data);};
  Child.Start();Child.BeginOutputReadLine();Child.BeginErrorReadLine();ActiveStrategy=strategy;Thread.Sleep(700);if(!Running)throw new Exception("winws завершился сразу после запуска. Подробности в журнале.");Log("Запущена стратегия: "+strategy);
 }
 public virtual void Stop(){if(Running){Child.Kill();if(!Child.WaitForExit(5000))throw new Exception("Не удалось остановить winws за 5 секунд. Настройки не переключены.");Log("Процесс FurZap остановлен.");}}
 public virtual void ServiceStop(){CheckOwnership();if(ServiceState()==0)return;using(var s=new ServiceController("zapret")){if(s.Status!=ServiceControllerStatus.Stopped){s.Stop();s.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(15));}}Log("Служба остановлена.");}
 public virtual void ServiceStart(){CheckOwnership();if(ServiceState()==0)throw new Exception("Служба ещё не установлена.");using(var s=new ServiceController("zapret")){if(s.Status!=ServiceControllerStatus.Running){s.Start();s.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(15));}}Log("Служба запущена.");}
 public void Install(string strategy,bool start=true){
  if(Preview)return;CheckOwnership();if(!Running&&ServiceState()==0&&Process.GetProcessesByName("winws").Length>0)throw new Exception("Останови другой winws.exe перед установкой службы.");
  EnsureLists();string args=Arguments(strategy);Stop();bool exists=ServiceState()!=0;if(exists)ServiceStop();Run("netsh.exe","interface tcp set global timestamps=enabled");
  string binary=Quote(P("bin/winws.exe"))+" "+args;
  Run("sc.exe",(exists?"config":"create")+" zapret binPath= "+Quote(binary)+" start= auto DisplayName= "+Quote("zapret"));
  using(var k=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\zapret",true))k.SetValue("zapret-discord-youtube",Path.GetFileNameWithoutExtension(strategy));
  if(start)ServiceStart();Log("Стратегия службы применена: "+strategy);
 }
 public void RemoveService(){if(Preview)return;CheckOwnership();ServiceStop();if(ServiceState()!=0)Run("sc.exe","delete zapret");Log("Служба zapret удалена. Общие драйверы WinDivert оставлены без изменений.");}
 public static bool ValidIp(string line){string[] p=line.Split(new char[] {'/'});IPAddress a;int n;if(p.Length>2||!IPAddress.TryParse(p[0],out a))return false;if(a.AddressFamily==AddressFamily.InterNetwork&&!Regex.IsMatch(p[0],@"^\d{1,3}(\.\d{1,3}){3}$"))return false;return p.Length==1||(int.TryParse(p[1],out n)&&n>=0&&n<=(a.AddressFamily==AddressFamily.InterNetwork?32:128));}
 public static void ValidateList(string name,string content){foreach(string raw in content.Split(new char[] {'\n'})){string line=raw.Split(new char[] {'#'})[0].Trim();if(line.Length==0)continue;if(name.StartsWith("ipset")){if(!ValidIp(line))throw new Exception("Неверный IP / CIDR: "+line);}else if(!Regex.IsMatch(line,@"^(\*\.)?([a-zA-Z0-9_-]+\.)+[a-zA-Z0-9_-]+$"))throw new Exception("Укажи домен без https:// и пути: "+line);}}
 public void SaveList(string name,string content){if(!new[]{"list-general-user.txt","list-exclude-user.txt","ipset-exclude-user.txt","list-general.txt","list-exclude.txt","list-google.txt","ipset-all.txt","ipset-exclude.txt"}.Contains(name))throw new Exception("Неизвестный список.");ValidateList(name,content);if(!name.StartsWith("ipset")&&String.IsNullOrWhiteSpace(content))throw new Exception("Оставь хотя бы один домен. Для пустого пользовательского списка допустим domain.example.abc.");Checkpoint("Список: "+name);Backup(P("lists/"+name));Atomic(P("lists/"+name),content);Log("Сохранён "+name+". Перезапусти движок, чтобы применить изменения.");}
 public string Download(string url){if(Preview)throw new Exception("Загрузка отключена в предпросмотре.");ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var req=(HttpWebRequest)WebRequest.Create(url);req.Timeout=15000;req.ReadWriteTimeout=15000;req.UserAgent="FurZap/1.0";using(var res=req.GetResponse())using(var sr=new StreamReader(res.GetResponseStream())){string s=sr.ReadToEnd();if(s.Length>4000000)throw new Exception("Ответ слишком большой.");return s;}}
 public void UpdateIps(){string s=Download("https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/ipset-service.txt");if(String.IsNullOrWhiteSpace(s))throw new Exception("Сервер вернул пустой IPSet.");ValidateList("ipset-all.txt",s);string target=IpMode()=="loaded"?P("lists/ipset-all.txt"):P("lists/ipset-all.txt.backup");Checkpoint("Обновление IPSet");Backup(target);Atomic(target,s);Log("IPSet обновлён. Текущий режим фильтра сохранён.");}
 public void ReplaceFake(string target,string source){if(!new[]{"ACTIVE_DISCORD_UDP.bin","ACTIVE_GAME_UDP.bin"}.Contains(target)||!Directory.GetFiles(P("bin"),"*.bin").Select(Path.GetFileName).Contains(source)||source.StartsWith("ACTIVE_"))throw new Exception("Неизвестный пакет.");Checkpoint("Пакет: "+target);Backup(P("bin/"+target));File.Copy(P("bin/"+source),P("bin/"+target),true);Log(target+" ← "+source+". Нужен перезапуск движка.");}
 public string Diagnostics(){var b=new StringBuilder();b.AppendLine("FurZap 1.2.2 / исходная сборка 1.10.3");b.AppendLine("Папка: "+Root);foreach(string f in new[]{"bin/winws.exe","bin/WinDivert.dll","bin/WinDivert64.sys","bin/cygwin1.dll"})b.AppendLine((File.Exists(P(f))?"OK  ":"НЕТ  ")+f);b.AppendLine("Стратегий: "+Strategies.Length+" | IPSet: "+IpMode()+" | Game Filter: "+Game()["mode"]);if(!Preview){b.AppendLine("\nСлужба zapret: "+ServiceState()+" (0=не установлена, 1=остановлена, 4=работает)");b.AppendLine(Run("sc.exe","query BFE",false));b.AppendLine(Run("netsh.exe","interface tcp show global",false));string procs=Run("tasklist.exe","",false);b.AppendLine("\nПроцессы, которые могут пересекаться с Zapret:");foreach(string l in procs.Split(new char[] {'\n'}))if(Regex.IsMatch(l,"winws|Adguard|goodbyedpi|discord",RegexOptions.IgnoreCase))b.AppendLine(l);string services=Run("sc.exe","query state= all",false);foreach(string l in services.Split(new char[] {'\n'}))if(Regex.IsMatch(l,"Killer|SmartByte|TracSrvWrapper|EPWD|VPN|Connectivity",RegexOptions.IgnoreCase))b.AppendLine(l);using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings")){if(k!=null)b.AppendLine("Системный прокси: "+k.GetValue("ProxyEnable",0)+" "+k.GetValue("ProxyServer",""));}}b.AppendLine("\nДиагностика не изменяет настройки. Наличие процесса не доказывает доступность сайтов.");return b.ToString();}
 public string HostsFile{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"drivers\etc\hosts");}}
 public void ApplyHosts(string content){
  foreach(string raw in content.Split(new char[] {'\n'})){string l=raw.Split(new char[] {'#'})[0].Trim();if(l.Length==0)continue;string[] p=Regex.Split(l,@"\s+");IPAddress a;if(p.Length<2||!IPAddress.TryParse(p[0],out a)||p.Skip(1).Any(x=>!Regex.IsMatch(x,@"^[a-zA-Z0-9_.-]+$")))throw new Exception("Некорректная строка hosts: "+l);}
  if(String.IsNullOrWhiteSpace(content))throw new Exception("Пустой hosts не применяется.");string old=File.ReadAllText(HostsFile);Backup(HostsFile);string cleaned=Regex.Replace(old,@"(?ms)^# BEGIN FURZAP\r?\n.*?^# END FURZAP\r?\n?","");File.WriteAllText(HostsFile,cleaned.TrimEnd()+"\r\n\r\n# BEGIN FURZAP\r\n"+content.Trim()+"\r\n# END FURZAP\r\n",new UTF8Encoding(false));Log("Блок hosts обновлён. Исходный файл сохранён в data/backups.");
 }
 public void RemoveHosts(){string s=File.ReadAllText(HostsFile);Backup(HostsFile);File.WriteAllText(HostsFile,Regex.Replace(s,@"(?ms)^# BEGIN FURZAP\r?\n.*?^# END FURZAP\r?\n?",""),new UTF8Encoding(false));Log("Удалён только блок FURZAP из hosts.");}
 public void ClearDiscord(){if(new[]{"Discord","DiscordPTB","DiscordCanary","DiscordDevelopment"}.Any(n=>Process.GetProcessesByName(n).Length>0))throw new Exception("Сначала полностью закрой Discord, включая значок в трее.");foreach(string variant in new[]{"discord","discordptb","discordcanary","discorddevelopment"}){string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),variant);foreach(string sub in new[]{"Cache","Code Cache","GPUCache"}){string p=Path.Combine(root,sub);if(Directory.Exists(p))Directory.Delete(p,true);}}Log("Кэш Discord очищен. Данные аккаунта не удалены.");}
}}
