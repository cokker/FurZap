using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 bool EngineUpdating;Label EngineStatus;CancellationTokenSource EngineCancel;
 void MaintenanceCard(){
  var card=Box(428,257);Title(card,"Zapret · версия "+B.EngineVersion,"Движок из Flowseal. Обновление сохраняет списки и порты.");
  EngineStatus=L(card,"Перед установкой сохраняется предыдущий движок целиком.",23,79,700,24,9);
  Check(card,"Проверять обновления движка при открытии",23,111,B.CheckEngineUpdates,v=>{try{B.SetEngineUpdateCheck(v);}catch(Exception ex){Error(ex);}});
  Btn(card,"Обновить Zapret",23,153,220,36,UpdateEngine,true);
  Btn(card,"Вернуть движок",255,153,220,36,()=>{if(!B.CanRollbackEngine){Feedback("Копии пока нет","Она появится после первого обновления движка.");return;}if(Confirm("Вернуть предыдущий движок и его конфигурацию? Текущий режим работы будет сохранён."))Work(()=>B.RollbackEngine(),()=>ReloadPage("Обновления"));});
  Btn(card,"История обновлений",487,153,220,36,()=>TextDialog("История обновлений",B.UpdateHistory(),false,null));
  Btn(card,"Проверить версию",23,207,220,32,()=>CheckVersion(true));
  var cancelButton=new FButton{Text="Отменить загрузку",Bounds=new Rectangle(255,207,220,32)};cancelButton.Click+=(s,e)=>{if(EngineCancel!=null)EngineCancel.Cancel();};card.Controls.Add(cancelButton);
  Btn(card,"Конфликты / помощь",487,207,220,32,ShowDiagnostics);
 }
 void ShowDiagnostics(){Work(()=>{string report=B.ConflictReport()+"\r\n\r\n"+B.Diagnostics();UI(()=>{
  var form=new Form{Text="Диагностика FurZap",Size=new Size(920,700),MinimumSize=new Size(780,520),BackColor=Theme.Bg,StartPosition=FormStartPosition.CenterParent};
  var text=new RoundedTextBox{Dock=DockStyle.Fill,ReadOnly=true,Text=report,BackColor=Theme.Card,ForeColor=Theme.Text,Font=Theme.F(10),BorderStyle=BorderStyle.None};
  var bar=new Panel{Dock=DockStyle.Bottom,Height=60};form.Controls.Add(text);form.Controls.Add(bar);
  Btn(bar,"Открыть службу",16,12,200,36,()=>{form.Close();Navigate("Служба");});Btn(bar,"Открыть журнал",230,12,200,36,()=>{form.Close();Navigate("Журнал");});Btn(bar,"Скопировать отчёт",444,12,220,36,()=>Clipboard.SetText(report));form.Show(this);
 });});}
 async void UpdateEngine(){
  if(B.Preview||Busy||UpdatingApp||EngineUpdating)return;
  if(HasUnsavedLists()){MessageBox.Show(this,"Сначала сохрани изменения в списках.");return;}
  EngineUpdating=true;Busy=true;RefreshPetActivity();
  try{
   var release=await Task.Run(()=>EngineUpdater.Latest(B));
   Version installed,available;if(Version.TryParse(B.EngineVersion,out installed)&&Version.TryParse(release.Version,out available)&&available<=installed){Feedback("Движок актуален","Установлена версия "+B.EngineVersion);return;}
   if(!Confirm("Установить Zapret "+release.Version+"?\n\n"+release.Notes+"\n\nПорты и списки сохранятся. Работающий движок будет кратко остановлен и запущен заново. Предыдущая версия останется для отката."))return;
   using(var cancel=new CancellationTokenSource()){
    EngineCancel=cancel;
    string prepared=await Task.Run(()=>EngineUpdater.Download(B,release,cancel.Token,p=>UI(()=>{if(EngineStatus!=null&&!EngineStatus.IsDisposed)EngineStatus.Text="Загрузка движка · "+p+"%";})));
    cancel.Token.ThrowIfCancellationRequested();EngineCancel=null;
    if(EngineStatus!=null)EngineStatus.Text="Применяю движок. Дождись завершения…";
    await Task.Run(()=>B.ReplaceEngine(prepared));
   }
   Busy=false;ReloadPage("Обновления");Feedback("Движок обновлён","Zapret "+B.EngineVersion+". Предыдущая версия доступна для отката.");
  }catch(OperationCanceledException){Feedback("Загрузка отменена","Работающий движок не изменён.");}
  catch(Exception ex){Error(ex);}finally{EngineCancel=null;Busy=false;EngineUpdating=false;RefreshState();RefreshPetActivity();}
 }
 void RollbackApp(){
  if(B.Preview||Busy||UpdatingApp||EngineUpdating)return;if(HasUnsavedLists()){MessageBox.Show(this,"Сначала сохрани изменения в списках.");return;}
  string root=AppDomain.CurrentDomain.BaseDirectory;
  string backup=Directory.GetFiles(root,"FurZap.exe.*.bak").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
  if(backup==null){Feedback("Нет предыдущей версии","Копия создаётся при встроенном обновлении FurZap.");return;}
  try{var version=System.Reflection.AssemblyName.GetAssemblyName(backup);if(version.Name!="FurZap")throw new Exception("Неверная резервная копия.");if(!Confirm("Вернуть FurZap "+version.Version+" и перезапустить приложение? engine и data сохраняются."))return;
   string dir=Path.Combine(B.Data,"updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string staged=Path.Combine(dir,"FurZap.exe");File.Copy(backup,staged);B.Stop();AppUpdater.LaunchInstaller(staged);AllowExit=true;Close();
  }catch(Exception ex){Error(ex);}
 }
 void RefreshPetActivity(){foreach(var pet in new[]{SidebarPet,HeroBox==null?null:HeroBox.Pet,MiniPet})if(pet!=null&&!pet.IsDisposed){pet.Downloading=(UpdatingApp||EngineUpdating)&&B.Get("pet-download","yes")=="yes";pet.Checking=Scanning&&B.Get("pet-check","yes")=="yes";pet.Applying=Busy&&!Scanning&&!EngineUpdating&&B.Get("pet-apply","yes")=="yes";pet.Reactions=B.Get("pet-reactions","yes")=="yes";}}
 void PetOptions(){var c=Box(1289,207);Title(c,"Реакции дракона","Включай отдельные состояния; общий переключатель анимаций также действует.");
  string[] labels={"Загрузка обновлений","Проверка сети","Применение конфигурации","Успех и ошибка"};string[] keys={"pet-download","pet-check","pet-apply","pet-reactions"};
  for(int i=0;i<4;i++){string key=keys[i];Check(c,labels[i],23,83+i*29,B.Get(key,"yes")=="yes",v=>{B.Pref[key]=v?"yes":"no";B.SavePrefs();RefreshPetActivity();});}
 }
}
}
