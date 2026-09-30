using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 AppRelease PendingRelease;string PendingUpdate;bool UpdatingApp;
 CancellationTokenSource UpdateCancel;
 System.Windows.Forms.Timer UpdatePoll;
 FButton UpdateBadge;Label UpdateStatus;
 string UpdateMessage="Обновления ещё не проверялись.";
 void InitializeAppUpdates(Control top){
  UpdateBadge=new FButton{Text="Обновление FurZap",Bounds=new Rectangle(Math.Max(310,top.Width-305),47,275,28),Anchor=AnchorStyles.Top|AnchorStyles.Right,Visible=false};
  UpdateBadge.Click+=(s,e)=>{if(!Busy&&!UpdatingApp)CheckAppUpdate();};top.Controls.Add(UpdateBadge);
  UpdatePoll=new System.Windows.Forms.Timer{Interval=3600000};
  UpdatePoll.Tick+=(s,e)=>{if(B.Get("app-update-check","yes")=="yes")CheckAppUpdate(true);};
  Shown+=(s,e)=>{if(!B.Preview){UpdatePoll.Start();if(B.Get("app-update-check","yes")=="yes")CheckAppUpdate(true);}};
  Disposed+=(s,e)=>{UpdatePoll.Dispose();if(UpdateCancel!=null)UpdateCancel.Cancel();};
 }
 void SetUpdateStatus(string message){UpdateMessage=message;if(UpdateStatus!=null&&!UpdateStatus.IsDisposed)UpdateStatus.Text=message;}
 void UpdateCard(){
  var c=Box(785,260);Title(c,"Обновления FurZap","Оболочка загружается с GitHub. Установка — после твоего подтверждения.");
  Check(c,"Проверять обновления FurZap при запуске и каждый час",23,85,B.Get("app-update-check","yes")=="yes",v=>{B.Pref["app-update-check"]=v?"yes":"no";B.SavePrefs();});
  Check(c,"Автоматически скачивать новую версию FurZap",23,123,B.Get("app-update-download","yes")=="yes",v=>{B.Pref["app-update-download"]=v?"yes":"no";B.SavePrefs();});
  UpdateStatus=L(c,UpdateMessage,23,161,c.Width-46,32,9);UpdateStatus.ForeColor=Theme.Muted;
  Btn(c,"Проверить / установить",23,208,260,36,CheckAppUpdate,true);
  Btn(c,"Отменить загрузку",298,208,190,36,()=>{if(UpdateCancel!=null)UpdateCancel.Cancel();});
  Btn(c,"Отчёт об ошибке",500,208,210,36,ShowErrorReport);
 }
 void CheckAppUpdate(){CheckAppUpdate(false);}
 async void CheckAppUpdate(bool automatic){
  if(B.Preview||Busy||UpdatingApp)return;
  if(!automatic&&PendingUpdate!=null){InstallPendingUpdate();return;}
  UpdatingApp=true;SetUpdateStatus("Проверяю обновления FurZap…");
  try{
   var release=await Task.Run(()=>AppUpdater.Latest());
   if(IsDisposed||Disposing)return;
   if(release.Version<=typeof(MainForm).Assembly.GetName().Version){
    SetUpdateStatus("Установлена актуальная версия · "+DateTime.Now.ToString("HH:mm"));if(!automatic)Feedback("Обновлений нет","Установлена актуальная версия.");return;
   }
   if(PendingRelease!=null&&PendingRelease.Version==release.Version&&PendingUpdate!=null){SetUpdateStatus("Обновление скачано и готово к установке.");return;}
   PendingUpdate=null;PendingRelease=release;UpdateBadge.Visible=true;UpdateBadge.Text="Доступна FurZap "+release.Version.ToString(3);
   SetUpdateStatus("Доступна версия "+release.Version.ToString(3));
   if(automatic&&B.Get("app-update-download","yes")!="yes")return;
   if(!automatic&&!Confirm("Доступна FurZap "+release.Version.ToString(3)+"\n\n"+release.Notes+"\n\nСкачать обновление?"))return;
   using(var cancel=new CancellationTokenSource()){
    UpdateCancel=cancel;
    string staged=await Task.Run(()=>AppUpdater.Download(release,B.Data,cancel.Token,percent=>UI(()=>{
     SetUpdateStatus("Загрузка FurZap "+release.Version.ToString(3)+" · "+percent+"%");
     UpdateBadge.Text="Загрузка обновления · "+percent+"%";
    })));
    if(IsDisposed||Disposing)return;
    PendingUpdate=staged;UpdateBadge.Text="Установить FurZap "+release.Version.ToString(3);
    SetUpdateStatus("Обновление скачано. Нажми «Проверить / установить».");
    if(automatic)Feedback("Обновление скачано","FurZap "+release.Version.ToString(3)+" готов к установке. Кнопка — вверху окна.");
   }
   if(!automatic)InstallPendingUpdate();
  }catch(OperationCanceledException){if(!IsDisposed){SetUpdateStatus("Загрузка отменена. Повтори проверку, чтобы скачать.");UpdateBadge.Text="Скачать обновление FurZap";}}
  catch(Exception ex){if(!IsDisposed){SetUpdateStatus("Не удалось обновить: "+ex.Message);UpdateBadge.Text="Повторить обновление FurZap";Log("Обновление FurZap: "+ex.Message);if(!automatic)Error(ex);}}
  finally{UpdateCancel=null;UpdatingApp=false;}
 }
 internal void VerifyAutoUpdateUi(Action<bool,string> check){
  Navigate("Инструменты");
  check(UpdateStatus!=null&&!UpdateStatus.IsDisposed,"update status visible in tools");
  var card=UpdateStatus.Parent;
  check(card.Controls.Count>=8,"update settings, status and actions present");
  var toggles=new System.Collections.Generic.List<FToggle>();foreach(Control c in card.Controls)if(c is FToggle)toggles.Add((FToggle)c);
  check(toggles.Count==2,"app checking and downloading have separate toggles");
  foreach(var toggle in toggles){bool value=toggle.Checked;toggle.Checked=!value;toggle.Checked=value;}
  check(B.Get("app-update-check")=="yes"&&B.Get("app-update-download")=="yes","app update preferences persist");
  SetUpdateStatus("Test progress 50%");check(UpdateStatus.Text=="Test progress 50%","download progress reaches status label");
  Navigate("Главная");Navigate("Инструменты");check(UpdateStatus.Text=="Test progress 50%","update status survives navigation");
  check(!UpdatePoll.Enabled,"preview does not start network update timer");
  Navigate("Настройки");
 }
 void InstallPendingUpdate(){
  if(Busy||PendingUpdate==null||PendingRelease==null)return;
  if(HasUnsavedLists()){MessageBox.Show(this,"Сначала сохрани изменения в списках. Обновление уже скачано.","FurZap");return;}
  if(!Confirm("Установить FurZap "+PendingRelease.Version.ToString(3)+" и перезапустить приложение?\n\n"+PendingRelease.Notes+"\n\nПроцесс движка FurZap будет остановлен. Служба продолжит работать. Папки engine и data сохраняются."))return;
  try{AppUpdater.ValidateFile(PendingUpdate,PendingRelease);B.Stop();AppUpdater.LaunchInstaller(PendingUpdate);AllowExit=true;Close();}catch(Exception ex){Error(ex);}
 }
}
}
