using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 internal void VerifyToolsLayout(Action<bool,string> check,string file){
  Navigate("Инструменты");Page.AutoScrollPosition=Point.Empty;
  var cards=Page.Controls.OfType<Card>().OrderBy(c=>c.Top).ToArray();
  check(cards.Length>=5,"Tools shows all cards");
  check(cards.Zip(cards.Skip(1),(a,b)=>a.Bottom<=b.Top).All(x=>x),"Tools cards do not overlap at "+UiZoom+"x");
  var telegram=cards.FirstOrDefault(c=>c.Controls.OfType<Label>().Any(l=>l.Text=="Telegram · TG WS Proxy"));
  check(telegram!=null&&telegram.Top<cards[2].Top,"Telegram proxy appears near top of Tools");
  Navigate("Обновления");var engine=Page.Controls.OfType<Card>().FirstOrDefault(c=>c.Controls.OfType<FToggle>().Any(t=>t.Text=="Проверять обновления Zapret при запуске и каждый час"));
  check(engine!=null,"engine update check is in update center");
  check(Page.Controls.OfType<Card>().Count()>=5,"unified update center shows app, engine, proxy and IPSet");
  var intro=Page.Controls.OfType<Card>().OrderBy(c=>c.Top).First();var introDescription=intro.Controls.OfType<Label>().OrderBy(c=>c.Top).Last();var introButton=intro.Controls.OfType<FButton>().First();
  check(introButton.Top>=introDescription.Bottom+8&&introButton.Bottom<=intro.Height-8,"update check button has its own row and remains fully visible");
  var components=Page.Controls.OfType<Card>().Where(c=>c.Controls.OfType<FToggle>().Any()).ToArray();
  check(components.Length==3&&components.All(c=>c.Controls.OfType<FToggle>().Count()==2),"all three components have check and download switches");
  check(components.All(c=>c.Controls.OfType<FProgressBar>().Count()==1&&c.Controls.OfType<FProgressBar>().First().Bottom<c.Controls.OfType<FButton>().Min(b=>b.Top)),"three download progress bars fit above update actions");
  check(components.SelectMany(c=>c.Controls.OfType<FProgressBar>()).All(p=>!p.Visible&&p.AccessibleName=="Ход загрузки обновления"),"download bars start hidden and expose accessible names");
  check(components.Any(c=>c.Controls.OfType<FButton>().Any(b=>b.Text.StartsWith("Вернуть предыдущую версию TG"))),"Telegram proxy rollback is available in the update center");
  BuildTray();check(Tray.ContextMenuStrip.Items.OfType<ToolStripItem>().Any(i=>i.Text=="Запустить TG WS Proxy")&&Tray.ContextMenuStrip.Items.OfType<ToolStripItem>().Any(i=>i.Text=="Остановить TG WS Proxy")&&Tray.ContextMenuStrip.Items.OfType<ToolStripItem>().Any(i=>i.Text=="Подключить Telegram"),"tray contains Telegram proxy controls");
  Refresh();Screenshot(Path.Combine(Path.GetDirectoryName(file),file.Contains("-200")?"updates-200.png":"updates.png"));Navigate("Инструменты");
  Refresh();Screenshot(file);
 }
 internal void VerifyEditorUi(Action<bool,string> check){
  foreach(string section in new[]{"Главная","Стратегии","Служба","Настройки","Списки","Инструменты","Обновления","Проверки","Профили","Журнал"}){Navigate(section);check(Opacity==1,"window stays opaque on "+section);AnimateFrame(Motion.Now+.08);check(Opacity==1,"window stays opaque during animation on "+section);}
  Navigate("Инструменты");check(Page.Controls.OfType<Card>().Any(c=>c.Controls.OfType<FButton>().Any(b=>b.Text=="Подключить Telegram")),"Telegram connection visible in Tools");
  Navigate("Настройки");check(!Page.Controls.OfType<Card>().SelectMany(c=>c.Controls.OfType<FToggle>()).Any(t=>t.Text.StartsWith("Проверять обновления Zapret")),"engine update check removed from game settings");var settingsCards=Page.Controls.OfType<Card>().OrderBy(c=>c.Top).ToArray();check(settingsCards.Zip(settingsCards.Skip(1),(a,b)=>a.Bottom<=b.Top).All(x=>x),"settings cards remain separated after moving update toggle");
  Navigate("Служба");check(Page.Controls.OfType<Card>().Any(c=>c.Controls.OfType<FToggle>().Any(t=>t.Text=="Запускать TG WS Proxy вместе с Windows")),"Telegram autostart is in Service page");
  Navigate("Списки");check(!Dirty,"list is clean after navigation and layout");var edit=ListEditor;string original=edit.Text;edit.Font=new Font("Consolas",14);edit.SelectAll();edit.SelectionColor=Color.White;edit.Select(0,0);RenderPageScale();check(!Dirty,"font/format/scale do not dirty the list");
  edit.Focus();edit.SelectionStart=edit.TextLength;edit.SelectedText="\nnew.example";check(Dirty,"typing creates unsaved changes");edit.Undo();check(!Dirty,"undo returns to saved state");
  edit.Text=original+"\nchanged.example";check(Dirty,"programmatic content difference detected");edit.Text=original;check(!Dirty,"restoring exact text clears dirty state");
  Navigate("Главная");check(Subtitle.Text=="Главная","can leave unchanged list without a prompt");VerifyHomeActions(check);Navigate("Списки");var combo=Page.Controls.OfType<FCombo>().First();combo.SelectedIndex=3;check(!Dirty,"switching loaded files does not dirty editor");Navigate("Настройки");
 }
 internal void VerifyHomeActions(Action<bool,string> check){
  check(Nav.All(n=>n.NavIcon.Length>0)&&Nav.Select(n=>n.NavIcon).Distinct().Count()==Nav.Length,"every navigation section has a separate icon");
  check(HomeProxyStart!=null&&HomeProxyStop!=null&&HomeProxyStart.Parent==HeroBox&&HomeProxyStop.Parent==HeroBox,"Telegram proxy start and stop are in the home hero");
  check(HomeProxyStart.Top>HeroBox.Controls.OfType<FButton>().First(b=>b.Text=="Запустить").Bottom&&HomeProxyStop.Top==HomeProxyStart.Top,"Telegram proxy actions sit below Zapret actions");
  check(HomeProxyStart.Bottom<HeroBox.Height&&HomeProxyStop.Bottom<HeroBox.Height,"Telegram proxy actions fit inside the home hero");
  check(HomeProxyStatus!=null&&HomeTgDetails!=null&&HomeConnectButton!=null&&HomeConnectButton.Text=="Подключить Telegram","home shows proxy state and quick Telegram connection");
  check(HomeConnectButton.Top>=HomeTgDetails.Bottom&&HomeConnectButton.Left<=23*RenderZoom&&HomeConnectButton.Right>=HomeConnectButton.Parent.Width-25*RenderZoom,"Telegram connection is a full row below home controls");
  check(HomeConnectButton.Enabled,"Telegram quick connect can start a stopped proxy");
  var cards=Page.Controls.OfType<Card>().OrderBy(c=>c.Top).ToArray();
  check(cards.Zip(cards.Skip(1),(a,b)=>a.Bottom<=b.Top||a.Right<=b.Left).All(x=>x),"home cards do not overlap");
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
    case 0:form.VerifyComfortUi(check,dir);form.VerifyEditorUi(check);form.VerifyScrollUi(check);form.VerifyAutoUpdateUi(check,dir);break;
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
