using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 internal void VerifyComfortUi(Action<bool,string> check,string dir){
  Navigate("Списки");var editor=ListEditor;string baseline=editor.Text;editor.AppendText("\ncache-test.example");int caret=editor.SelectionStart;
  Navigate("Главная");check(HasUnsavedLists(),"detached list draft is tracked");Navigate("Списки");check(Object.ReferenceEquals(editor,ListEditor)&&ListEditor.Text.EndsWith("cache-test.example"),"list editor and unsaved content survive navigation");check(editor.SelectionStart==caret,"list caret survives navigation");editor.Text=baseline;check(!Dirty,"restored cached text clears dirty state");
  Navigate("Стратегии");var list=StrategyList;list.SelectedIndex=1;var chosen=list.SelectedItem;Navigate("Инструменты");Navigate("Стратегии");check(Object.ReferenceEquals(list,StrategyList)&&Equals(chosen,list.SelectedItem),"strategy selection and control are cached");
  Navigate("Настройки");var card=Page.Controls.OfType<Card>().First();var mode=card.Controls.OfType<FCombo>().First();int previous=mode.SelectedIndex;mode.SelectedIndex=(previous+1)%mode.Items.Count;Navigate("Главная");Navigate("Настройки");check(card==Page.Controls.OfType<Card>().First()&&mode.SelectedIndex!=previous,"unsaved setting choices survive navigation");mode.SelectedIndex=previous;
  UiZoom=2;Location=Point.Empty;Size=new Size(1920,1080);ReloadPage("Настройки");check(UiZoom==2&&Page.Controls.Cast<Control>().All(c=>c.Width>0&&c.Height>0),"200 percent layout has valid bounds");Refresh();Screenshot(Path.Combine(dir,"settings-200.png"));UiZoom=1;Size=new Size(1190,820);ReloadPage("Главная");check(ConnectionLabel!=null&&ConnectionLabel.Text.Contains("проверка не выполнена"),"unmeasured connection is not reported as connected");
  Page.AutoScrollPosition=new Point(0,680);Refresh();Screenshot(Path.Combine(dir,"home-status.png"));Page.AutoScrollPosition=Point.Empty;
  Navigate("Стратегии");Page.AutoScrollPosition=new Point(0,360);Refresh();Screenshot(Path.Combine(dir,"strategies-notes.png"));Navigate("Главная");
  ShowMini();check(Mini.Visible&&!Visible,"compact window replaces main window");check(Mini.Controls.OfType<FButton>().Any(b=>b.Text=="Запустить"),"compact window provides start action");using(var bmp=new Bitmap(Mini.Width,Mini.Height)){Mini.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(dir,"compact.png"));}Mini.Close();check(Visible,"closing compact window restores full window");
 }
}
public static class ComfortTests {
 public static void Run(Action<bool,string> check,Action<Action,string> reject){
  string current=typeof(MainForm).Assembly.Location;long size=new FileInfo(current).Length;string hash=AppUpdater.FileHash(current);string version=typeof(MainForm).Assembly.GetName().Version.ToString();
  string json="{\"tag_name\":\"v"+version+"\",\"draft\":false,\"prerelease\":false,\"body\":\"Test \\u0434\\nnotes\",\"assets\":[{\"name\":\"FurZap.exe\",\"size\":"+size+",\"digest\":\"sha256:"+hash+"\",\"browser_download_url\":\"https://github.com/cokker/FurZap/releases/download/v"+version+"/FurZap.exe\"}]}";
  var release=AppUpdater.ParseRelease(json);check(release.Notes.Contains("д\nnotes"),"release JSON preserves escapes");AppUpdater.ValidateFile(current,release);check(true,"update validates size hash assembly and version");
  reject(()=>AppUpdater.ParseRelease(json.Replace("github.com/cokker","evil.example/cokker")),"update rejects foreign download host");reject(()=>AppUpdater.ParseRelease(json.Replace("/cokker/FurZap/","/stranger/FurZap/")),"update rejects foreign repository");reject(()=>AppUpdater.ParseRelease(json.Replace("sha256:","md5:")),"update rejects missing SHA256");reject(()=>AppUpdater.ParseRelease(json.Replace("\"draft\":false","\"draft\":true")),"update rejects draft release");release.Digest=new string('0',64);reject(()=>AppUpdater.ValidateFile(current,release),"modified update fails verification");
  string clean=MainForm.RedactReport("C:\\Users\\Tester\\FurZap a@b.com token=abc 192.0.2.1 https://example.com/test", "Tester", "C:\\Users\\Tester\\FurZap");check(!clean.Contains("Tester")&&!clean.Contains("a@b.com")&&!clean.Contains("abc")&&!clean.Contains("192.0.2.1")&&!clean.Contains("example.com"),"error report redacts paths emails tokens IPs and URLs");
 }
}
}
