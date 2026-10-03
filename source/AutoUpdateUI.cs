using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 AppRelease PendingRelease;string PendingUpdate;bool UpdatingApp;
 CancellationTokenSource UpdateCancel;
 System.Windows.Forms.Timer UpdatePoll;
 FButton UpdateBadge;Label UpdateStatus;FProgressBar AppDownloadBar;
 string UpdateMessage="Обновления ещё не проверялись.";
 void InitializeAppUpdates(Control top){
  UpdateBadge=new FButton{Text="Обновление FurZap",Bounds=new Rectangle(Math.Max(310,top.Width-305),47,275,28),Anchor=AnchorStyles.Top|AnchorStyles.Right,Visible=false};
  UpdateBadge.Click+=(s,e)=>{if(!Busy&&!UpdatingApp)Navigate("Обновления");};top.Controls.Add(UpdateBadge);
  UpdatePoll=new System.Windows.Forms.Timer{Interval=3600000};
  UpdatePoll.Tick+=(s,e)=>CheckEnabledUpdates();
  Shown+=(s,e)=>{if(!B.Preview){UpdatePoll.Start();CheckEnabledUpdates();}};
  Disposed+=(s,e)=>{UpdatePoll.Dispose();if(UpdateCancel!=null)UpdateCancel.Cancel();};
 }
 void CheckEnabledUpdates(){
  if(B.Get("app-update-check","yes")=="yes")CheckAppUpdate(true);
  if(B.CheckEngineUpdates)CheckEngineUpdate(true);
  if(B.Get("tg-update-check","yes")=="yes")CheckTelegramUpdate(true);
 }
 void SetUpdateStatus(string message){UpdateMessage=message;if(UpdateStatus!=null&&!UpdateStatus.IsDisposed)UpdateStatus.Text=message;}
 void UpdateCard(){
  var c=Box(177,270);Title(c,"FurZap · версия "+typeof(MainForm).Assembly.GetName().Version.ToString(3),"Оболочка загружается с GitHub. Установка — после твоего подтверждения.");
  Check(c,"Проверять обновления FurZap при запуске и каждый час",23,85,B.Get("app-update-check","yes")=="yes",v=>{B.Pref["app-update-check"]=v?"yes":"no";B.SavePrefs();});
  Check(c,"Автоматически скачивать новую версию FurZap",23,123,B.Get("app-update-download","yes")=="yes",v=>{B.Pref["app-update-download"]=v?"yes":"no";B.SavePrefs();});
  UpdateStatus=L(c,UpdateMessage,23,161,c.Width-46,32,9);UpdateStatus.ForeColor=Theme.Muted;
  AppDownloadBar=new FProgressBar{Bounds=new Rectangle(23,196,c.Width-46,11),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};c.Controls.Add(AppDownloadBar);
  Btn(c,"Проверить / установить",23,216,260,36,CheckAppUpdate,true);
  Btn(c,"Отменить загрузку",298,216,190,36,()=>{if(UpdateCancel!=null)UpdateCancel.Cancel();});
  Btn(c,"Вернуть FurZap",500,216,210,36,RollbackApp);
 }
 void CheckAppUpdate(){CheckAppUpdate(false);}
 async void CheckAppUpdate(bool automatic){
  if(B.Preview||Busy||UpdatingApp||EngineUpdating)return;
  if(!automatic&&PendingUpdate!=null){InstallPendingUpdate();return;}
  UpdatingApp=true;SetUpdateStatus("Проверяю обновления FurZap…");
  try{
   var release=await Task.Run(()=>AppUpdater.Latest());
   if(IsDisposed||Disposing)return;
   if(release.Version<=typeof(MainForm).Assembly.GetName().Version){
    SetUpdateStatus("Установлена актуальная версия · "+DateTime.Now.ToString("HH:mm"));if(!automatic)Feedback("Обновлений нет","Установлена актуальная версия.");return;
   }
   string cached=await Task.Run(()=>AppUpdater.FindDownloaded(release,B.Data));if(IsDisposed||Disposing)return;
   if(cached!=null){PendingRelease=release;PendingUpdate=cached;UpdateBadge.Visible=true;UpdateBadge.Text="Установить FurZap "+release.Version.ToString(3);SetUpdateStatus("Обновление скачано и готово к установке.");if(!automatic)InstallPendingUpdate();return;}
   PendingUpdate=null;PendingRelease=release;UpdateBadge.Visible=true;UpdateBadge.Text="Доступна FurZap "+release.Version.ToString(3);
   SetUpdateStatus("Доступна версия "+release.Version.ToString(3));
   if(automatic&&B.Get("app-update-download","yes")!="yes")return;
   using(var cancel=new CancellationTokenSource()){
    UpdateCancel=cancel;SetAppProgress(0,true);
    string staged=await Task.Run(()=>AppUpdater.Download(release,B.Data,cancel.Token,percent=>UI(()=>{
     SetUpdateStatus("Загрузка FurZap "+release.Version.ToString(3)+" · "+percent+"%");
     SetAppProgress(percent,true);
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
   finally{UpdateCancel=null;UpdatingApp=false;SetAppProgress(0,false);}
 }
 void SetAppProgress(int value,bool visible){if(AppDownloadBar!=null&&!AppDownloadBar.IsDisposed){AppDownloadBar.Value=value;AppDownloadBar.Visible=visible;}}
 internal void VerifyAutoUpdateUi(Action<bool,string> check,string dir){
  Navigate("Обновления");
  check(UpdateStatus!=null&&!UpdateStatus.IsDisposed,"update status visible in update center");
  var card=UpdateStatus.Parent;
  check(card.Controls.Count>=8,"update settings, status and actions present");
  var toggles=new System.Collections.Generic.List<FToggle>();foreach(Control c in card.Controls)if(c is FToggle)toggles.Add((FToggle)c);
  check(toggles.Count==2,"app checking and downloading have separate toggles");
  check(AppDownloadBar!=null&&!AppDownloadBar.Visible,"app progress bar begins hidden");SetAppProgress(50,true);check(AppDownloadBar.Visible&&AppDownloadBar.Value==50,"app progress bar shows percentage");SetAppProgress(0,false);
  foreach(var toggle in toggles){bool value=toggle.Checked;toggle.Checked=!value;toggle.Checked=value;}
  check(B.Get("app-update-check")=="yes"&&B.Get("app-update-download")=="yes","app update preferences persist");
  SetUpdateStatus("Test progress 50%");check(UpdateStatus.Text=="Test progress 50%","download progress reaches status label");
  Navigate("Главная");Navigate("Обновления");check(UpdateStatus.Text=="Test progress 50%","update status survives navigation");
  check(!UpdatePoll.Enabled,"preview does not start network update timer");
  using(var dialog=CreateAppUpdateDialog(new AppRelease{Version=new Version(1,5,2),Notes="# FurZap 1.5.2\n\n- Новое окно\n\n## Ранее в версии 1.5.1\n- Старая история"})){
   var updateCard=dialog.Controls.OfType<Card>().First();var changes=updateCard.Controls.OfType<RichTextBox>().First();
   check(dialog.ClientSize.Width<=700&&changes.Text.Contains("Новое окно")&&!changes.Text.Contains("Старая история"),"compact update dialog shows only latest notes");
   dialog.Show(this);dialog.BringToFront();dialog.Refresh();
   using(var bmp=new Bitmap(dialog.ClientSize.Width,dialog.ClientSize.Height)){
    using(var graphics=Graphics.FromImage(bmp))graphics.CopyFromScreen(dialog.PointToScreen(Point.Empty),Point.Empty,bmp.Size);
    bmp.Save(Path.Combine(dir,"update-dialog.png"));
   }
   dialog.Close();
  }
  Navigate("Настройки");
 }
 void InstallPendingUpdate(){
  if(Busy||PendingUpdate==null||PendingRelease==null)return;
  if(HasUnsavedLists()){MessageBox.Show(this,"Сначала сохрани изменения в списках. Обновление уже скачано.","FurZap");return;}
  using(var dialog=CreateAppUpdateDialog(PendingRelease))if(dialog.ShowDialog(this)!=DialogResult.Yes)return;
  try{AppUpdater.ValidateFile(PendingUpdate,PendingRelease);B.Stop();AppUpdater.LaunchInstaller(PendingUpdate);AllowExit=true;Close();}catch(Exception ex){Error(ex);}
 }
}
}
