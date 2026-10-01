using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 internal void VerifyEditorUi(Action<bool,string> check){
  foreach(string section in new[]{"Главная","Стратегии","Служба","Настройки","Списки","Инструменты","Проверки","Профили","Журнал"}){Navigate(section);check(Opacity==1,"window stays opaque on "+section);AnimateFrame(Motion.Now+.08);check(Opacity==1,"window stays opaque during animation on "+section);}
  Navigate("Списки");check(!Dirty,"list is clean after navigation and layout");var edit=ListEditor;string original=edit.Text;edit.Font=new Font("Consolas",14);edit.SelectAll();edit.SelectionColor=Color.White;edit.Select(0,0);RenderPageScale();check(!Dirty,"font/format/scale do not dirty the list");
  edit.Focus();edit.SelectionStart=edit.TextLength;edit.SelectedText="\nnew.example";check(Dirty,"typing creates unsaved changes");edit.Undo();check(!Dirty,"undo returns to saved state");
  edit.Text=original+"\nchanged.example";check(Dirty,"programmatic content difference detected");edit.Text=original;check(!Dirty,"restoring exact text clears dirty state");
  Navigate("Главная");check(Subtitle.Text=="Главная","can leave unchanged list without a prompt");Navigate("Списки");var combo=Page.Controls.OfType<FCombo>().First();combo.SelectedIndex=3;check(!Dirty,"switching loaded files does not dirty editor");Navigate("Настройки");
 }
 internal void VerifyPopupUi(int iteration,Action<bool,string> check){
  var appearance=Page.Controls.OfType<Card>().First(c=>c.Controls.OfType<FButton>().Any(b=>b.Text=="Применить оформление"));var combos=appearance.Controls.OfType<FCombo>().ToArray();
  foreach(var combo in combos){combo.ShowPopup();combo.CommitForTest(iteration%combo.Items.Count);check(!combo.PopupDisposedForTest,"popup survives native close callback");combo.ShowPopup();combo.CommitForTest((iteration+1)%combo.Items.Count);}
  appearance.Controls.OfType<FButton>().First().ActivateForPreview();check(Subtitle.Text=="Настройки","appearance applied and page rebuilt");
 }
 internal void ToggleFocusUi(){var personal=Page.Controls.OfType<Card>().Skip(2).First();var toggles=personal.Controls.OfType<FToggle>().ToArray();foreach(var t in toggles){t.Focus();t.Checked=!t.Checked;}Page.AutoScrollPosition=new Point(0,(int)(600*RenderZoom));}
 internal void BeginFrameSample(){B.Pref["motion"]="yes";UiZoom=1;Location=Point.Empty;Size=new Size(1190,820);Navigate("Главная");}
 internal string FrameStats{get{return "Timer pulses="+Timer.Pulses+", posts="+Timer.Posts+", skipped="+Timer.Skipped+", dragon mean paint ms="+(HeroBox.Pet.PaintSeconds*1000/Math.Max(1,HeroBox.Pet.PaintedFrames)).ToString("F2");}}
 internal int DragonPaints{get{return HeroBox==null?0:HeroBox.Pet.PaintedFrames;}}
 internal void PetReaction(){HeroBox.Pet.React();}
}
public static class UIRegression {
 public static int Run(Backend backend,string dir){
  Directory.CreateDirectory(dir);var report=new List<string>();int result=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("FAIL: "+label);report.Add("PASS: "+label);};
  using(var form=new MainForm(backend))using(var timer=new System.Windows.Forms.Timer{Interval=400}){
   int stage=0,startFrames=0,startPaints=0;double startTime=0;form.Shown+=(s,e)=>timer.Start();
   timer.Tick+=(s,e)=>{try{switch(stage++){
    case 0:form.VerifyComfortUi(check,dir);form.VerifyEditorUi(check);form.VerifyScrollUi(check);form.VerifyAutoUpdateUi(check);break;
    case 1:case 2:case 3:case 4:case 5:case 6:form.VerifyPopupUi(stage,check);break;
    case 7:form.ToggleFocusUi();break;
    case 8:form.Screenshot(Path.Combine(dir,"toggles.png"));form.BeginFrameSample();break;
    case 9:startFrames=form.AnimationFrames;startPaints=form.DragonPaints;startTime=Motion.Now;break;
    case 10:form.PetReaction();break;
    case 14:double elapsed=Motion.Now-startTime;double callbacks=(form.AnimationFrames-startFrames)/elapsed,paints=(form.DragonPaints-startPaints)/elapsed;report.Add("Measured preview: "+callbacks.ToString("F1")+" UI frames/s; "+paints.ToString("F1")+" dragon paints/s over "+elapsed.ToString("F2")+" s. Runtime: "+Environment.OSVersion+".");report.Add(form.FrameStats);check(form.AnimationFrames>startFrames,"frame pump advances during visible animation");report.Add("FPS is a measurement, not a pass/fail gate: the 1.2.2 baseline also measured about 36 UI frames/s on this shared runner.");form.Screenshot(Path.Combine(dir,"home.png"));form.Hide();startFrames=form.AnimationFrames;break;
    case 15:check(form.AnimationFrames==startFrames,"frame loop pauses while hidden");form.Show();break;
    case 16:check(form.AnimationFrames>startFrames,"frame loop resumes when shown");timer.Stop();form.Close();break;
   }}catch(Exception ex){result=1;report.Add(ex.ToString());timer.Stop();form.Close();}};
   Application.Run(form);
  }
  File.WriteAllLines(Path.Combine(dir,"ui-regression.txt"),report);foreach(string line in report)Console.WriteLine(line);return result;
 }
}
}

