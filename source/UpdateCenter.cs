using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 Label TgUpdateStatus;CancellationTokenSource TgUpdateCancel;bool TgUpdating;
 string TgUpdateMessage="Обновления TG WS Proxy ещё не проверялись.";
 void UpdatesPage(){
  var intro=Box(22,112);Title(intro,"Центр обновлений","FurZap, Zapret и TG WS Proxy обновляются независимо. Установка каждого требует твоего решения.");
  Btn(intro,"Проверить все версии",23,67,258,34,CheckAllUpdates,true);
  UpdateCard();MaintenanceCard();
  var tg=Box(702,195);Title(tg,"TG WS Proxy · "+TelegramProxyUpdater.InstalledVersion(TgProxy.Executable,B.Data),"Официальная сборка Flowseal. Останови прокси перед установкой новой версии.");
  TgUpdateStatus=L(tg,TgUpdateMessage,23,82,tg.Width-46,36,9);TgUpdateStatus.ForeColor=Theme.Muted;
  Btn(tg,"Проверить / обновить",23,137,240,36,UpdateTelegram,true);
  Btn(tg,"Отменить загрузку",278,137,215,36,()=>{if(TgUpdateCancel!=null)TgUpdateCancel.Cancel();});
  Btn(tg,"Открыть Telegram",506,137,210,36,ConnectTelegram);
  var lists=Box(913,137);Title(lists,"Списки IPSet","Текущий режим IPSet сохраняется при обновлении списков Flowseal.");
  Btn(lists,"Обновить IPSet",23,84,235,37,()=>Work(()=>B.UpdateIps()),true);
  Btn(lists,"История обновлений",273,84,235,37,()=>TextDialog("История обновлений",B.UpdateHistory(),false,null));
  L(Page,"Проверка версии не меняет файлы. При установке FurZap и движка сохраняются копии для возврата.",33,1070,900,42,9).ForeColor=Theme.Muted;
 }
 void SetTgUpdateStatus(string message){TgUpdateMessage=message;if(TgUpdateStatus!=null&&!TgUpdateStatus.IsDisposed)TgUpdateStatus.Text=message;}
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
   var release=await Task.Run(()=>TelegramProxyUpdater.Latest(B));
   if(TgProxy.Executable!=null&&TelegramProxyUpdater.Matches(TgProxy.Executable,release)){SetTgUpdateStatus("TG WS Proxy "+release.Version+" · актуальная версия");return;}
   if(!Confirm("Установить TG WS Proxy "+release.Version+" из официального релиза Flowseal? Сначала прокси должен быть остановлен."))return;
   using(var cancel=new CancellationTokenSource()){
    TgUpdateCancel=cancel;
    await Task.Run(()=>TelegramProxyUpdater.Download(B,release,cancel.Token,p=>UI(()=>SetTgUpdateStatus("Загрузка TG WS Proxy · "+p+"%"))));
   }
   SetTgUpdateStatus("TG WS Proxy "+release.Version+" установлен. Запусти его на главной.");Feedback("Прокси обновлён","TG WS Proxy "+release.Version+" готов к запуску.");
  }catch(OperationCanceledException){SetTgUpdateStatus("Загрузка TG WS Proxy отменена.");}
  catch(Exception ex){SetTgUpdateStatus("Не удалось обновить TG WS Proxy: "+ex.Message);Error(ex);}
  finally{TgUpdateCancel=null;Busy=false;TgUpdating=false;RefreshPetActivity();RefreshState();}
 }
}
}
