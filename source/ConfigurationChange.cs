using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Win32;
namespace FurZap {
public partial class Backend {
 readonly object changeGate=new object();
 public virtual void RestoreServiceCommand(string command,string strategy){
  Run("sc.exe","config zapret binPath= "+Quote(command));
  using(var k=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\zapret",true))if(k!=null)k.SetValue("zapret-discord-youtube",strategy);
 }
 public virtual string ServiceStrategy(){if(Preview)return Get("strategy","general.bat");using(var k=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\zapret"))return k==null?"":Convert.ToString(k.GetValue("zapret-discord-youtube",""));}
 public virtual void VerifyActive(bool service){if(Preview)return;System.Threading.Thread.Sleep(900);if(service?ServiceState()!=4:!Running)throw new Exception("Движок остановился после применения. Открой Журнал для ответа winws.");}
 public void ApplyChange(string reason,Action change,string desired=null,bool startIfStopped=false){
  lock(changeGate){
   CheckOwnership();int state=ServiceState();if(state!=0&&state!=1&&state!=4)throw new Exception("Служба меняет состояние. Подожди завершения и повтори.");
   bool running=Running;if(running&&state!=0)throw new Exception("Одновременно обнаружены процесс и служба. Останови лишний экземпляр перед изменением.");
   string active=ActiveStrategy,command=ServicePath(),serviceStrategy=ServiceStrategy();
   var prefs=new Dictionary<string,string>(Pref);string recovery=Checkpoint("До применения: "+reason);bool stopped=false;
   try{
    if(running)Stop();if(state==4)ServiceStop();stopped=true;
    change();string selected=desired??Get("strategy",Strategies[0]);Arguments(selected);
    if(state!=0){Install(selected,state==4||startIfStopped);if(state==4||startIfStopped)VerifyActive(true);}
    else if(running||startIfStopped){Start(selected);VerifyActive(false);}
    Log(reason+((running||state==4||startIfStopped)?". Новая конфигурация применена; движок перезапущен.":". Сохранено; остановленный движок не запускался."));
   }catch(Exception error){
    if(!stopped)throw new Exception("Не удалось остановить движок. Конфигурация не изменена. "+error.Message,error);
    try{
     Stop();if(state!=0)ServiceStop();RestoreFiles(recovery);Pref.Clear();foreach(var p in prefs)Pref[p.Key]=p.Value;SavePrefs();
     if(state!=0){RestoreServiceCommand(command,serviceStrategy);if(state==4){ServiceStart();VerifyActive(true);}}
     else if(running){Start(active);VerifyActive(false);}
    }catch(Exception restore){throw new Exception("Изменения не применены: "+error.Message+"\nНе удалось полностью восстановить прежнее состояние: "+restore.Message+"\nРезервная копия: "+recovery,error);}
    throw new Exception("Изменения не применены. Прежние файлы и режим работы восстановлены.\nПричина: "+error.Message,error);
   }
  }
 }
 public void SelectStrategy(string strategy,bool start=false){if(!Strategies.Contains(strategy))throw new Exception("Стратегия отсутствует.");ApplyChange("Стратегия: "+strategy,()=>{Pref["strategy"]=strategy;Pref["recent."+strategy]=DateTime.UtcNow.Ticks.ToString();SavePrefs();},strategy,start);}
 public void RecordUpdate(string component,string from,string to,string result){
  string line=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" | "+component+" | "+from+" → "+to+" | "+result;
  try{File.AppendAllText(Path.Combine(Data,"updates-history.txt"),line+Environment.NewLine,System.Text.Encoding.UTF8);}catch(Exception ex){Log("Не удалось записать историю обновления: "+ex.Message);}
 }
 public string UpdateHistory(){string p=Path.Combine(Data,"updates-history.txt");return File.Exists(p)?File.ReadAllText(p):"Обновлений и откатов ещё не было.";}
}
}
