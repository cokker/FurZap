using System;
using System.Drawing;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 internal Form CreateAppUpdateDialog(AppRelease release){
  var dialog=new Form{Text="Обновление FurZap",ClientSize=new Size(650,455),FormBorderStyle=FormBorderStyle.FixedDialog,
   StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,MaximizeBox=false,MinimizeBox=false,
   BackColor=Theme.Bg,ForeColor=Theme.Text,Font=Theme.F(),AutoScaleMode=AutoScaleMode.Dpi,Icon=Icon,KeyPreview=true};
  var card=new Card{Bounds=new Rectangle(17,17,616,421)};dialog.Controls.Add(card);
  L(card,"Доступна FurZap "+release.Version.ToString(3),24,21,565,35,17,true).ForeColor=Theme.Orange;
  L(card,"Обновление загружено и проверено. Выбери, когда его установить.",25,63,565,28,10).ForeColor=Theme.Muted;
  L(card,"Что нового в этом выпуске",25,111,565,26,11,true);
  var changes=new RoundedTextBox{Bounds=new Rectangle(25,146,565,158),ReadOnly=true,Text=AppUpdater.LatestChanges(release.Notes),
   BackColor=Theme.Side,ForeColor=Theme.Text,Font=Theme.F(10),BorderStyle=BorderStyle.None,WordWrap=true,DetectUrls=false,ScrollBars=RichTextBoxScrollBars.Vertical,TabStop=false};
  card.Controls.Add(changes);
  L(card,"При установке FurZap перезапустится. Папки engine и data сохранятся;\nобычный процесс движка остановится, служба продолжит работу.",25,318,565,42,9).ForeColor=Theme.Muted;
  var install=new FButton{Text="Установить и перезапустить",Accent=true,Bounds=new Rectangle(25,374,310,36)};
  install.Click+=(s,e)=>{dialog.DialogResult=DialogResult.Yes;dialog.Close();};card.Controls.Add(install);
  var later=new FButton{Text="Позже",Bounds=new Rectangle(350,374,240,36)};
  later.Click+=(s,e)=>{dialog.DialogResult=DialogResult.No;dialog.Close();};card.Controls.Add(later);
  dialog.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){dialog.DialogResult=DialogResult.No;dialog.Close();}};
  return dialog;
 }
}
}
