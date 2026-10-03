using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 Label TgUpdateStatus;FProgressBar TgDownloadBar;CancellationTokenSource TgUpdateCancel;bool TgUpdating;
 TelegramProxyRelease PendingTgRelease;string PendingTgFile;
 string TgUpdateMessage="Обновления TG WS Proxy ещё не проверялись.";
 void UpdatesPage(){
  var intro=Box(22,139);Title(intro,"Центр обновлений","FurZap, Zapret и TG WS Proxy обновляются независимо. Установка каждого требует твоего решения.");
  Btn(intro,"Проверить все версии",23,88,258,37,CheckAllUpdates,true);
  UpdateCard();MaintenanceCard();
  var tg=Box(768,315);Title(tg,"TG WS Proxy · "+TelegramProxyUpdater.InstalledVersion(TgProxy.Executable,B.Data),"Официальная сборка Flowseal. Для установки останови прокси; загрузка не прерывает его работу.");
  TgUpdateStatus=L(tg,TgUpdateMessage,23,82,tg.Width-46,25,9);TgUpdateStatus.ForeColor=Theme.Muted;
  Check(tg,"Проверять обновления TG WS Proxy при запуске и каждый час",23,112,B.Get("tg-update-check","yes")=="yes",v=>{B.Pref["tg-update-check"]=v?"yes":"no";B.SavePrefs();});
  Check(tg,"Автоматически скачивать новую версию TG WS Proxy",23,150,B.Get("tg-update-download","no")=="yes",v=>{B.Pref["tg-update-download"]=v?"yes":"no";B.SavePrefs();});
  TgDownloadBar=new FProgressBar{Bounds=new Rectangle(23,188,tg.Width-46,11),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};tg.Controls.Add(TgDownloadBar);
  Btn(tg,"Проверить / установить",23,211,240,36,UpdateTelegram,true);
  Btn(tg,"Отменить загрузку",278,211,215,36,()=>{if(TgUpdateCancel!=null)TgUpdateCancel.Cancel();});
  Btn(tg,"Открыть Telegram",506,211,210,36,ConnectTelegram);
  Btn(tg,"Вернуть предыдущую версию TG WS Proxy",23,260,366,36,RollbackTelegram);
  var lists=Box(1101,137);Title(lists,"Списки IPSet","Текущий режим IPSet сохраняется при обновлении списков Flowseal.");
  Btn(lists,"Обновить IPSet",23,84,235,37,()=>Work(()=>B.UpdateIps()),true);
  Btn(lists,"История обновлений",273,84,235,37,()=>TextDialog("История обновлений",B.UpdateHistory(),false,null));
  L(Page,"Проверка версии не меняет файлы. Загрузка не устанавливает обновление без подтверждения.",33,1255,900,42,9).ForeColor=Theme.Muted;
 }
 void SetTgProgress(int value,bool visible){if(TgDownloadBar!=null&&!TgDownloadBar.IsDisposed){TgDownloadBar.Value=value;TgDownloadBar.Visible=visible;}}
 void RollbackTelegram(){
  if(TgProxy.Running){Feedback("Прокси работает","Останови TG WS Proxy перед откатом.");return;}
  if(!TelegramProxyUpdater.CanRollback(B.Data)){Feedback("Копии нет","Она появится после первого обновления TG WS Proxy.");return;}
  if(Confirm("Вернуть предыдущую версию TG WS Proxy? Текущая версия будет заменена, настройки сохранятся."))Work(()=>{TelegramProxyUpdater.Rollback(B);},()=>ReloadPage("Обновления"));
 }
 void SetTgUpdateStatus(string message){TgUpdateMessage=message;if(TgUpdateStatus!=null&&!TgUpdateStatus.IsDisposed)TgUpdateStatus.Text=message;}
 async void CheckTelegramUpdate(bool automatic){
  if(B.Preview||Busy||TgUpdating||(!automatic&&(UpdatingApp||EngineUpdating)))return;
  TgUpdating=true;SetTgUpdateStatus("Проверяю TG WS Proxy…");
  try{
   var release=await Task.Run(()=>TelegramProxyUpdater.Latest(B));if(IsDisposed||Disposing)return;
   string installed=TgProxy.Executable;
   if(installed!=null&&TelegramProxyUpdater.Matches(installed,release)){SetTgUpdateStatus("TG WS Proxy "+release.Version+" · актуальная версия");return;}
   if(PendingTgRelease==null||PendingTgRelease.Version!=release.Version)PendingTgFile=null;
   PendingTgRelease=release;
   SetTgUpdateStatus("Доступна TG WS Proxy "+release.Version+" · установлена "+TelegramProxyUpdater.InstalledVersion(installed,B.Data));
   if(!automatic||B.Get("tg-update-download","no")!="yes")return;
   string cached=TelegramProxyUpdater.StagedPath(B,release);
   if(TelegramProxyUpdater.Matches(cached,release)){PendingTgFile=cached;SetTgUpdateStatus("TG WS Proxy "+release.Version+" скачан. Установка — по кнопке.");return;}
   using(var cancel=new CancellationTokenSource()){
    TgUpdateCancel=cancel;SetTgProgress(0,true);
    PendingTgFile=await Task.Run(()=>TelegramProxyUpdater.Stage(B,release,cancel.Token,p=>UI(()=>{SetTgUpdateStatus("Загрузка TG WS Proxy · "+p+"%");SetTgProgress(p,true);} )));
   }
   if(!IsDisposed&&!Disposing)SetTgUpdateStatus("TG WS Proxy "+release.Version+" скачан. Установка — по кнопке.");
  }catch(OperationCanceledException){SetTgUpdateStatus("Загрузка TG WS Proxy отменена.");}
  catch(Exception ex){Log("Проверка TG WS Proxy: "+ex.Message);SetTgUpdateStatus("Не удалось проверить TG WS Proxy: "+ex.Message);if(!automatic)Error(ex);}
  finally{TgUpdateCancel=null;TgUpdating=false;SetTgProgress(0,false);}
 }
 async void CheckAllUpdates(){
  if(B.Preview||Busy||UpdatingApp||EngineUpdating||TgUpdating)return;
  Busy=true;Page.Enabled=false;SetUpdateStatus("Проверяю FurZap…");if(EngineStatus!=null)EngineStatus.Text="Проверяю Zapret…";SetTgUpdateStatus("Проверяю TG WS Proxy…");
  try{
   var app=Task.Run(()=>{try{return AppUpdater.Latest();}catch(Exception ex){Log("FurZap: "+ex.Message);return null;}});
   var engine=Task.Run(()=>{try{return EngineUpdater.Latest(B);}catch(Exception ex){Log("Zapret: "+ex.Message);return null;}});
   var tg=Task.Run(()=>{try{return TelegramProxyUpdater.Latest(B);}catch(Exception ex){Log("TG WS Proxy: "+ex.Message);return null;}});
   await Task.WhenAll(app,engine,tg);if(IsDisposed||Disposing)return;
   var current=typeof(MainForm).Assembly.GetName().Version;
   if(app.Result==null)SetUpdateStatus("Не удалось проверить FurZap. Повтори проверку.");
   else if(app.Result.Version>current){PendingRelease=app.Result;SetUpdateStatus("Доступна FurZap "+app.Result.Version.ToString(3)+" · установлена "+current.ToString(3));UpdateBadge.Visible=true;UpdateBadge.Text="Доступна FurZap "+app.Result.Version.ToString(3);}
   else SetUpdateStatus("FurZap "+current.ToString(3)+" · актуальная версия");
   if(EngineStatus!=null&&!EngineStatus.IsDisposed){Version installed,available;bool newer=engine.Result!=null&&Version.TryParse(B.EngineVersion,out installed)&&Version.TryParse(engine.Result.Version,out available)&&available>installed;EngineStatus.Text=engine.Result==null?"Не удалось проверить Zapret. Повтори проверку.":newer?"Доступна Zapret "+engine.Result.Version+" · установлена "+B.EngineVersion:"Zapret "+B.EngineVersion+" · актуальная версия";}
   string local=TelegramProxyUpdater.InstalledVersion(TgProxy.Executable,B.Data);
   bool currentTg=tg.Result!=null&&TgProxy.Executable!=null&&TelegramProxyUpdater.Matches(TgProxy.Executable,tg.Result);
   SetTgUpdateStatus(tg.Result==null?"Не удалось проверить TG WS Proxy. Повтори проверку.":currentTg?"TG WS Proxy "+tg.Result.Version+" · актуальная версия":"Доступна TG WS Proxy "+tg.Result.Version+" · установлена "+local);
  }catch(Exception ex){SetTgUpdateStatus("Не удалось завершить проверку обновлений.");Error(ex);}
  finally{Busy=false;if(!IsDisposed&&!Page.IsDisposed)Page.Enabled=true;}
 }
 async void UpdateTelegram(){
  if(B.Preview||Busy||UpdatingApp||EngineUpdating||TgUpdating)return;
  if(TgProxy.Running){Feedback("Прокси работает","Сначала останови TG WS Proxy, затем обнови его.");return;}
  TgUpdating=true;Busy=true;SetTgUpdateStatus("Проверяю TG WS Proxy…");RefreshPetActivity();
  try{
   var release=PendingTgRelease??await Task.Run(()=>TelegramProxyUpdater.Latest(B));
   if(TgProxy.Executable!=null&&TelegramProxyUpdater.Matches(TgProxy.Executable,release)){SetTgUpdateStatus("TG WS Proxy "+release.Version+" · актуальная версия");return;}
   if(!Confirm("Установить TG WS Proxy "+release.Version+" из официального релиза Flowseal? Работающий прокси должен быть остановлен."))return;
   using(var cancel=new CancellationTokenSource()){
    TgUpdateCancel=cancel;SetTgProgress(0,true);
    string cached=PendingTgFile??TelegramProxyUpdater.StagedPath(B,release);
    string staged=TelegramProxyUpdater.Matches(cached,release)?cached:await Task.Run(()=>TelegramProxyUpdater.Stage(B,release,cancel.Token,p=>UI(()=>{SetTgUpdateStatus("Загрузка TG WS Proxy · "+p+"%");SetTgProgress(p,true);} )));
    cancel.Token.ThrowIfCancellationRequested();
    await Task.Run(()=>TelegramProxyUpdater.InstallStaged(B,release,staged));
   }
   PendingTgFile=null;PendingTgRelease=null;
   SetTgUpdateStatus("TG WS Proxy "+release.Version+" установлен. Запусти его на главной.");Feedback("Прокси обновлён","TG WS Proxy "+release.Version+" готов к запуску.");
  }catch(OperationCanceledException){SetTgUpdateStatus("Загрузка TG WS Proxy отменена.");}
  catch(Exception ex){SetTgUpdateStatus("Не удалось обновить TG WS Proxy: "+ex.Message);Error(ex);}
  finally{TgUpdateCancel=null;Busy=false;TgUpdating=false;SetTgProgress(0,false);RefreshPetActivity();RefreshState();}
 }
}
}
