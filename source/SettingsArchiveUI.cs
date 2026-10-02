using System;
using System.IO;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 void ExportSettings(){
  if(HasUnsavedLists()){MessageBox.Show(this,"Сначала сохрани изменения в списках.","FurZap");return;}
  using(var dialog=new SaveFileDialog{Title="Экспорт настроек FurZap",FileName="FurZap-settings-"+DateTime.Now.ToString("yyyyMMdd")+".zip",Filter="Архив настроек FurZap|*.zip",DefaultExt="zip",AddExtension=true}){
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   string path=dialog.FileName;
   Work(()=>SettingsArchive.Export(B,path),()=>Feedback("Настройки экспортированы",Path.GetFileName(path)));
  }
 }
 void ImportSettings(){
  if(HasUnsavedLists()){MessageBox.Show(this,"Сначала сохрани изменения в списках.","FurZap");return;}
  using(var dialog=new OpenFileDialog{Title="Импорт настроек FurZap",Filter="Архив настроек FurZap|*.zip",CheckFileExists=true}){
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   string path=dialog.FileName;
   if(!Confirm("Импортировать настройки из «"+Path.GetFileName(path)+"»?\n\nТекущая конфигурация будет сохранена в истории. Совпадающие профили будут заменены. Работающий Zapret перезапустится; при ошибке прежнее состояние будет восстановлено."))return;
   Work(()=>SettingsArchive.Import(B,path),()=>{SyncProfile();Feedback("Настройки импортированы","Профили и конфигурация применены.");});
  }
 }
}
}
