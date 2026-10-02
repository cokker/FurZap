using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace FurZap {
public static class Theme {
 public static Color Bg=Color.FromArgb(16,18,25), Side=Color.FromArgb(20,23,32), Card=Color.FromArgb(27,31,42), Line=Color.FromArgb(46,51,66), Text=Color.FromArgb(240,239,245), Muted=Color.FromArgb(156,164,185), Orange=Color.FromArgb(255,164,94), Mint=Color.FromArgb(127,221,188);
 public static Font F(float size=10,bool bold=false){return new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular);}
 public static GraphicsPath Round(RectangleF r,float n=18){n=Math.Min(n,Math.Min(r.Width,r.Height));var p=new GraphicsPath();p.AddArc(r.X,r.Y,n,n,180,90);p.AddArc(r.Right-n,r.Y,n,n,270,90);p.AddArc(r.Right-n,r.Bottom-n,n,n,0,90);p.AddArc(r.X,r.Bottom-n,n,n,90,90);p.CloseFigure();return p;}
 public static void Txt(Graphics g,string s,float x,float y,float size,Color color,bool bold=false){using(var f=F(size,bold))using(var b=new SolidBrush(color))g.DrawString(s,f,b,x,y);}
}
public class Card:Panel {
 public Card(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.ResizeRedraw,true);BackColor=Theme.Bg;}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.Round(new RectangleF(0,0,Width-1,Height-1),22)){using(var b=new SolidBrush(Theme.Card))e.Graphics.FillPath(b,p);using(var pen=new Pen(Theme.Line))e.Graphics.DrawPath(pen,p);}base.OnPaint(e);}
}
public class Hero:Card {
 public bool WideLayout;
 public string Status="Готов к запуску";public Dragon Pet;public float PaintZoom=1;
 public Hero(){Pet=new Dragon{Bounds=new Rectangle(460,5,300,264),Anchor=AnchorStyles.Top|AnchorStyles.Right};Controls.Add(Pet);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;using(var p=Theme.Round(new RectangleF(1,1,Width-3,Height-3),22))using(var b=new LinearGradientBrush(ClientRectangle,Color.FromArgb(37,32,37),Color.FromArgb(32,31,43),0f))g.FillPath(b,p);
  g.ScaleTransform(PaintZoom,PaintZoom);Theme.Txt(g,"ТВОЙ ДРАКОН НА СВЯЗИ",27,26,9,Theme.Orange,true);Theme.Txt(g,"Открывай свой мир.",25,56,26,Theme.Text,true);Theme.Txt(g,"Знакомый Zapret. Немного больше уюта.",28,104,11,Theme.Muted);
  using(var b=new SolidBrush(Theme.Mint))g.FillEllipse(b,30,WideLayout?257:155,7,7);Theme.Txt(g,Status,44,WideLayout?250:148,11,Theme.Text);Theme.Txt(g,"Статус движка · доступность проверяется отдельно",28,WideLayout?280:178,9,Theme.Muted);
 }
}
public partial class MainForm:Form {
 Backend B;TelegramProxyManager TgProxy;FButton HomeProxyStart,HomeProxyStop;Panel Side,Page;Label Subtitle,Toast;FButton[] Nav;Hero HeroBox;Dragon SidebarPet;NotifyIcon Tray;FramePump Timer;Process ExternalTool;
 Action HomeLayout=delegate{};Action ViewLayout=delegate{};FeedbackCard Notice;double repaintAfter=Motion.Now+.12;double noticeUntil;FButton ActionButton;FList StrategyList;Label AppliedLabel;
 string Selected;bool Busy,AllowExit;RichTextBox ListEditor;string ListBaseline="";bool Dirty{get{return ListEditor!=null&&!ListEditor.IsDisposed&&!ListEditor.Disposing&&ListDocument.Changed(ListBaseline,ListEditor.Text);}}RichTextBox LogView;StringBuilder Journal=new StringBuilder();string CurrentLog;
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);if(Environment.OSVersion.Platform==PlatformID.Win32NT){try{int on=1;DwmSetWindowAttribute(Handle,20,ref on,4);}catch{}}}
 public MainForm(Backend b){B=b;TgProxy=new TelegramProxyManager(b);Theme.Orange=AccentColor(B.Get("theme","orange"));B.Log=Log;Text="FurZap · уютная сторона интернета";Size=new Size(1190,820);MinimumSize=new Size(1050,740);StartPosition=FormStartPosition.CenterScreen;BackColor=Theme.Bg;ForeColor=Theme.Text;Font=Theme.F();AutoScaleMode=AutoScaleMode.Dpi;
  string icon=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"furzap.ico");if(File.Exists(icon))Icon=new Icon(icon,32,32);
  Selected=B.Get("strategy","general.bat");if(!B.Strategies.Contains(Selected))Selected=B.Strategies.First();
  CurrentLog=Path.Combine(B.Data,"furzap.log");if(File.Exists(CurrentLog)&&new FileInfo(CurrentLog).Length>2000000)File.Move(CurrentLog,CurrentLog+"."+DateTime.Now.ToString("yyyyMMddHHmmss"));
  Side=new Panel{Dock=DockStyle.Left,Width=213,BackColor=Theme.Side};Controls.Add(Side);Side.Paint+=(s,e)=>{using(var p=new Pen(Theme.Line))e.Graphics.DrawLine(p,212,0,212,Side.Height);};
  Label brand=L(Side,"FurZap",25,31,163,38,23,true);brand.ForeColor=Theme.Text;L(Side,"by COKKER  /  v1.4.4",27,75,170,22,9).ForeColor=Theme.Muted;
  string[] names={"Главная","Стратегии","Служба","Настройки","Списки","Инструменты","Проверки","Профили","Журнал"};string[] icons={"\uE80F","\uE8A4","\uE713","\uE8B7","\uE8A5","\uE90F","\uE9D9","\uE77B","\uE81C"};Nav=new FButton[names.Length];for(int i=0;i<names.Length;i++){string n=names[i];Nav[i]=Btn(Side,n,17,120+i*44,178,38,()=>Navigate(n));Nav[i].NavIcon=icons[i];}
  SidebarPet=new Dragon{Bounds=new Rectangle(42,Side.Height-221,125,110),Anchor=AnchorStyles.Left|AnchorStyles.Bottom,Reduced=true};Side.Controls.Add(SidebarPet);SidebarPet.Cursor=Cursors.Hand;SidebarPet.Click+=(s,e)=>Feedback("Буп!","Рад тебя видеть.");L(Side,"Маленький дракон.\nБольшая сеть.",26,Side.Height-99,168,45,10).Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
  var ver=L(Side,"ZAPRET ENGINE  "+B.EngineVersion,26,Side.Height-45,180,25,8);ver.ForeColor=Theme.Muted;ver.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
  var container=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Bg};Controls.Add(container);container.BringToFront();
  var top=new Panel{Dock=DockStyle.Top,Height=80};container.Controls.Add(top);InitializeAppUpdates(top);Subtitle=L(top,"",31,24,720,35,21,true);var eyebrow=L(top,"DESKTOP / WINDOWS",31,59,400,17,8);eyebrow.ForeColor=Theme.Muted;
  Toast=L(container,"Готово. Выбери стратегию и запусти движок.",0,0,300,36,9);Toast.Dock=DockStyle.Bottom;Toast.Padding=new Padding(30,8,8,0);Toast.BackColor=Theme.Side;
  var viewport=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Bg};container.Controls.Add(viewport);viewport.BringToFront();var clip=new Panel{BackColor=Theme.Bg};viewport.Controls.Add(clip);Page=new Panel{AutoScroll=true,Padding=new Padding(0),BackColor=Theme.Bg};clip.Controls.Add(Page);var wheel=new PageWheelRouter(Page);Disposed+=(s,e)=>wheel.Dispose();var rail=new ScrollRail(Page){Width=12};clip.Controls.Add(rail);ViewLayout=()=>{int limit=(int)((Subtitle.Text=="Главная"?1500:1120)*UiZoom);int width=Math.Min(limit,viewport.ClientSize.Width);clip.SetBounds(Math.Max(0,(viewport.ClientSize.Width-width)/2),0,width,viewport.ClientSize.Height);Page.SetBounds(0,0,width+SystemInformation.VerticalScrollBarWidth,clip.ClientSize.Height);rail.SetBounds(width-12,3,12,Math.Max(1,clip.ClientSize.Height-6));rail.BringToFront();Subtitle.Left=clip.Left+31;eyebrow.Left=clip.Left+31;HomeLayout();RenderPageScale();Page.Invalidate(true);};viewport.Resize+=(s,e)=>ViewLayout();ViewLayout();Notice=new FeedbackCard();Notice.Click+=(s,e)=>{noticeUntil=0;Notice.Visible=false;};container.Controls.Add(Notice);Notice.BringToFront();
  Tray=new NotifyIcon{Icon=Icon,Text="FurZap",Visible=!B.Preview};var menu=new ContextMenuStrip();menu.Items.Add("Открыть FurZap",null,(s,e)=>Restore());menu.Items.Add("Остановить процесс FurZap",null,(s,e)=>Work(()=>B.Stop()));menu.Items.Add("Выход",null,(s,e)=>{AllowExit=true;Close();});Tray.ContextMenuStrip=menu;Tray.DoubleClick+=(s,e)=>Restore();
  Timer=new FramePump(this,AnimateFrame);
  InitializeExtras();SizeChanged+=(s,e)=>{repaintAfter=Motion.Now+.12;UpdateFramePump();};FormClosing+=HandleClosing;Shown+=(s,e)=>{Log(B.Preview?"Предпросмотр. Системные операции отключены.":"FurZap открыт. Движок не запускается автоматически.");if(!B.Preview&&B.CheckEngineUpdates)CheckVersion(false);};Navigate("Главная");
 }
 Label L(Control p,string text,int x,int y,int w,int h,float size=10,bool bold=false){var l=new Label{Text=text,Bounds=new Rectangle(x,y,w,h),Font=Theme.F(size,bold),ForeColor=Theme.Text,BackColor=p is Card?Theme.Card:p.BackColor};p.Controls.Add(l);return l;}
 FButton Btn(Control p,string text,int x,int y,int w,int h,Action action,bool accent=false){var c=new FButton{Text=text,Bounds=new Rectangle(x,y,w,h),Accent=accent,AccessibleName=text};c.Click+=(s,e)=>{if(!Busy)try{ActionButton=c;action();}catch(Exception ex){Error(ex);}};p.Controls.Add(c);return c;}
 FCombo Combo(Control p,string[] items,int x,int y,int w){var c=new FCombo{Bounds=new Rectangle(x,y,w,36),AccessibleName="Выбор значения"};c.Items.AddRange(items);if(items.Length>0)c.SelectedIndex=0;p.Controls.Add(c);return c;}
 FInput Input(Control p,string text,int x,int y,int w){var c=new FInput{Text=text,Bounds=new Rectangle(x,y,w,34)};p.Controls.Add(c);return c;}
 FToggle Check(Control p,string text,int x,int y,bool value,Action<bool> action){var c=new FToggle{Text=text,Bounds=new Rectangle(x,y,670,30),Checked=value,ForeColor=Theme.Text};c.Width=Math.Min(c.Width,Math.Max(120,p.ClientSize.Width-x-20));c.CheckedChanged+=(s,e)=>action(c.Checked);p.Controls.Add(c);return c;}
 void Feedback(string heading,string message,bool failed=false){if(!B.Preview&&B.Get("sound","no")=="yes"){if(failed)System.Media.SystemSounds.Exclamation.Play();else System.Media.SystemSounds.Asterisk.Play();}if(ActionButton!=null&&!ActionButton.IsDisposed&&!failed)ActionButton.Confirmed();SidebarPet.React(failed);if(MiniPet!=null&&!MiniPet.IsDisposed)MiniPet.React(failed);if(HeroBox!=null&&!HeroBox.IsDisposed)HeroBox.Pet.React(failed);Notice.Heading=heading;Notice.Message=message;Notice.Failure=failed;Notice.Left=Math.Max(12,Notice.Parent.ClientSize.Width-Notice.Width-28);Notice.Top=Notice.Parent.ClientSize.Height-Notice.Height-48;noticeUntil=Motion.Now+3.6;Notice.Visible=true;Notice.BringToFront();Notice.Invalidate();}
 Card Box(int y,int h){var p=new Card{Location=new Point(30,y),Size=new Size(Math.Max(750,ContentWidth-61),h),Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right};Page.Controls.Add(p);return p;}
 void Title(Card c,string title,string desc){L(c,title,22,17,c.Width-45,30,14,true);L(c,desc,23,52,c.Width-45,desc.Contains("\n")?46:24,10).ForeColor=Theme.Muted;}
 public void Navigate(string name){
  if(Busy)return;if(Subtitle.Text==name&&Page.Controls.Count>0){RefreshState();return;}
  SavePage();Page.SuspendLayout();try{Page.Controls.Clear();Page.AutoScrollPosition=Point.Empty;Subtitle.Text=name;
  if(!RestorePage(name)){ListEditor=null;ListBaseline="";PageBounds=new Dictionary<Control,Rectangle>();PageFonts=new Dictionary<Control,Font>();PageStretch=new Dictionary<Control,bool>();HomeLayout=delegate{};HeroBox=null;LogView=null;StrategyList=null;AppliedLabel=null;
   switch(name){case "Главная":Home();break;case "Стратегии":Strategies();break;case "Служба":Service();break;case "Настройки":Settings();break;case "Списки":Lists();break;case "Инструменты":Tools();break;case "Журнал":Logs();break;case "Проверки":ChecksPage();break;case "Профили":ProfilesPage();break;}ApplyPageScale();}
  foreach(var n in Nav){n.Selected=n.Text==name;n.Invalidate();}ViewLayout();HomeLayout();AddHints(Page);RefreshState();pageSlide=Motion.Now;
  }finally{Page.ResumeLayout(true);}RestoreScroll(name);repaintAfter=Motion.Now+.20;
 }

 void Home(){
  HeroBox=new Hero{Location=new Point(30,22),Size=new Size(Math.Max(750,ContentWidth-61),282),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top};Page.Controls.Add(HeroBox);HeroBox.Pet.Left=HeroBox.Width-319;HeroBox.Pet.Cursor=Cursors.Hand;HeroBox.Pet.Click+=(s,e)=>Feedback("Буп!","Дракончик рядом.");
  Btn(HeroBox,"Запустить",28,220,155,42,()=>Work(()=>B.Start(Selected)),true);Btn(HeroBox,"Остановить",194,220,145,42,()=>Work(()=>B.Stop()));
  var proxyTitle=L(HeroBox,"TG WS Proxy",28,267,311,22,10,true);proxyTitle.ForeColor=Theme.Muted;proxyTitle.BackColor=Color.Transparent;
  HomeProxyStart=Btn(HeroBox,"Запустить TG",28,290,155,42,()=>Work(()=>TgProxy.Start()),true);
  HomeProxyStop=Btn(HeroBox,"Остановить TG",194,290,145,42,()=>Work(()=>TgProxy.Stop()));
  var card=Box(322,139);Title(card,"Твоя стратегия",Path.GetFileNameWithoutExtension(Selected)+"  ·  "+B.Strategies.Length+" вариантов в сборке");HomeStrategy=card.Controls.OfType<Label>().Skip(1).First();Btn(card,"Выбрать стратегию",22,83,210,39,()=>Navigate("Стратегии"));L(card,"Не подошла? Попробуй другой вариант\nили открой раздел «Проверки».",267,83,430,42,10).ForeColor=Theme.Muted;
  var info=Box(477,134);Title(info,"Всё под лапой", "Настрой запуск с Windows и игровые порты.");Btn(info,"Служба Windows",22,82,195,36,()=>ReloadPage("Служба"));Btn(info,"Игровые порты",228,82,195,36,()=>Navigate("Настройки"));
  var footer=L(Page,"Движок и драйверы — из твоего архива. FurZap — отдельная оболочка.",33,627,800,35,9);footer.ForeColor=Theme.Muted;
  HeroBox.Anchor=AnchorStyles.Top|AnchorStyles.Left;card.Anchor=AnchorStyles.Top|AnchorStyles.Left;info.Anchor=AnchorStyles.Top|AnchorStyles.Left;
  HomeBase.Clear();BaseFonts.Clear();CaptureHome(HeroBox);CaptureHome(card);CaptureHome(info);CaptureHome(footer);
  var comfort=ComfortHome();CaptureHome(comfort);
  HomeLayout=()=>{
   Point homeScroll=Page.AutoScrollPosition;Page.AutoScrollPosition=Point.Empty;ResetHome();
   if(HeroBox==null||HeroBox.IsDisposed)return;
   int width=Math.Max(750,(int)(Page.Parent.ClientSize.Width/RenderZoom)-65);
   bool wide=width>=1160;HeroBox.WideLayout=wide;
   if(wide){
    int left=(int)(width*.64),right=width-left-18;
    HeroBox.SetBounds(30,22,left,450);card.SetBounds(48+left,22,right,186);info.SetBounds(48+left,224,right,218);
    HeroBox.Pet.SetBounds(left-315,62,300,300);
    HeroBox.Controls[1].Top=326;HeroBox.Controls[2].Top=326;
    proxyTitle.Top=370;HomeProxyStart.Top=392;HomeProxyStop.Top=392;
    card.Controls[0].Width=right-44;card.Controls[1].SetBounds(23,52,right-46,38);
    card.Controls[2].SetBounds(22,97,right-44,38);card.Controls[3].SetBounds(23,144,right-46,36);
    info.Controls[0].Width=right-44;info.Controls[1].SetBounds(23,49,right-46,41);
    info.Controls[2].SetBounds(22,103,right-44,38);info.Controls[3].SetBounds(22,150,right-44,38);
    footer.Top=486;
   }else{
    HeroBox.SetBounds(30,22,width,350);HeroBox.Pet.SetBounds(width-319,5,300,264);
    HeroBox.Controls[1].Top=210;HeroBox.Controls[2].Top=210;
    proxyTitle.Top=266;HomeProxyStart.Top=290;HomeProxyStop.Top=290;
    card.SetBounds(30,390,width,139);card.Controls[0].Width=width-45;
    card.Controls[1].SetBounds(23,52,width-45,24);card.Controls[2].SetBounds(22,83,210,39);
    card.Controls[3].SetBounds(267,83,430,42);
    info.SetBounds(30,545,width,134);info.Controls[0].Width=width-45;
    info.Controls[1].SetBounds(23,52,width-45,24);
    info.Controls[2].SetBounds(22,82,195,36);info.Controls[3].SetBounds(228,82,195,36);
    footer.Top=695;
   }
   comfort.Top=wide?535:747;comfort.Width=width;
   ScaleHome(HeroBox,card,info,footer,comfort);
   HeroBox.Invalidate(true);card.Invalidate(true);info.Invalidate(true);
   Page.AutoScrollPosition=new Point(-homeScroll.X,-homeScroll.Y);
  };
  HomeLayout();RefreshState();
 }
 void RefreshState(){RefreshExtras();RefreshComfort();if(HomeProxyStart!=null&&!HomeProxyStart.IsDisposed)HomeProxyStart.Enabled=!TgProxy.Running;if(HomeProxyStop!=null&&!HomeProxyStop.IsDisposed)HomeProxyStop.Enabled=TgProxy.Running;if(HeroBox==null||HeroBox.IsDisposed)return;string s=B.Preview?"Предпросмотр интерфейса":B.Running?"Движок запущен · "+Path.GetFileNameWithoutExtension(B.ActiveStrategy):B.ServiceState()==4?"Запущена служба Windows":"Движок остановлен";if(HeroBox.Status!=s){HeroBox.Status=s;HeroBox.Invalidate();}}
 void Choose(string s,bool start=false){
  if(B.Preview){Selected=s;return;}
  Work(()=>B.SelectStrategy(s,start),()=>{Selected=B.Get("strategy",s);if(StrategyList!=null){StrategyList.Applied=Selected;StrategyList.Invalidate();}if(AppliedLabel!=null)AppliedLabel.Text="✓ Выбрано: "+Path.GetFileNameWithoutExtension(Selected);});
 }

 void Strategies(){
  L(Page,"Выбери маршрут для своего дракона",32,19,740,25,11).ForeColor=Theme.Muted;
  var search=Input(Page,"",32,58,ContentWidth-65);search.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;search.AccessibleName="Поиск стратегии";
  L(Page,"ПОИСК ПО НАЗВАНИЮ",33,98,480,20,8).ForeColor=Theme.Muted;
  var list=new FList{Bounds=new Rectangle(32,120,ContentWidth-65,280),FileNames=true,Applied=Selected,Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right};list.Caption=x=>(B.Get("favorite."+x)=="yes"?"★ ":"")+Path.GetFileNameWithoutExtension(x)+(B.Get("recent."+x).Length>0?" · недавно":"");Page.Controls.Add(list);StrategyList=list;
  Action fill=()=>{string focused=list.SelectedItem as string;list.SelectedIndex=-1;list.Items.Clear();foreach(var s in B.Strategies.OrderByDescending(x=>B.Get("favorite."+x)=="yes").ThenByDescending(x=>B.Get("recent."+x,"0").PadLeft(20,'0')).Where(x=>x.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0))list.Items.Add(s);if(focused!=null&&list.Items.Contains(focused))list.SelectedItem=focused;else if(list.Items.Contains(Selected))list.SelectedItem=Selected;else if(list.Items.Count>0)list.SelectedIndex=0;list.Invalidate();};fill();search.TextChanged+=(s,e)=>fill();
  Btn(Page,"Выбрать",32,417,154,43,()=>{if(list.SelectedItem!=null)Choose(list.SelectedItem.ToString());},true);
  Btn(Page,"Выбрать и запустить",197,417,235,43,()=>{if(list.SelectedItem==null)return;Choose(list.SelectedItem.ToString(),true);});
  Btn(Page,"Параметры",444,417,164,43,()=>{if(list.SelectedItem!=null)TextDialog("Параметры winws",B.Arguments(list.SelectedItem.ToString()),false,null);});
  AppliedLabel=L(Page,"✓ Выбрано: "+Path.GetFileNameWithoutExtension(Selected),33,473,800,25,10,true);AppliedLabel.ForeColor=Theme.Mint;
  StrategyExtras(list,fill);
  var note=Box(760,117);Title(note,"Одной универсальной стратегии нет","Результат зависит от провайдера и сети. Изменения сразу применяются к работающему\nпроцессу или службе. Остановленный движок остаётся остановленным.");
 }
 void Service(){var c=Box(22,265);Title(c,"Запуск вместе с Windows","Служба продолжает работать, когда окно FurZap закрыто. Не перемещай папку\nприложения после установки. Выбранная стратегия: "+Selected);
  string state=B.Preview?"Предпросмотр":B.ServiceState()==0?"Не установлена":B.ServiceState()==4?"Работает":"Остановлена / переходное состояние";L(c,"Состояние: "+state,23,107,700,28,12,true).ForeColor=Theme.Mint;
  Btn(c,"Установить / применить",22,151,263,44,()=>{if(Confirm("Применить выбранную стратегию к службе zapret и включить её автозапуск с Windows?"))Work(()=>B.Install(Selected),()=>ReloadPage("Служба"));},true);
  Btn(c,"Запустить",297,151,167,44,()=>Work(()=>B.ServiceStart(),()=>ReloadPage("Служба")));Btn(c,"Остановить",476,151,167,44,()=>Work(()=>B.ServiceStop(),()=>ReloadPage("Служба")));
  Btn(c,"Удалить службу",22,209,263,37,()=>{if(Confirm("Остановить и удалить службу zapret из этой папки?"))Work(()=>B.RemoveService(),()=>ReloadPage("Служба"));});Btn(c,"Обновить состояние",297,209,220,37,()=>ReloadPage("Служба"));
  var n=Box(305,159);Title(n,"Что произойдёт при закрытии","Обычный запуск: предложим остановить процесс или свернуть окно в трей.\nРежим службы: закрытие окна не останавливает службу.");Btn(n,"Открыть папку приложения",23,103,290,38,()=>Open(AppDomain.CurrentDomain.BaseDirectory));
 }
 void Settings(){
  var g=B.Game();var c=Box(22,245);Title(c,"Игровой трафик","Настрой TCP и UDP отдельно. Диапазоны: 443,50000-50100 или 1024-65535.");
  L(c,"Режим",23,94,110,25);var mode=Combo(c,new[]{"Выключен","TCP + UDP","Только TCP","Только UDP"},23,122,235);string[] modes={"disabled","all","tcp","udp"};mode.SelectedIndex=Math.Max(0,Array.IndexOf(modes,g["mode"]));
  L(c,"TCP-порты",281,94,210,25);var tcp=Input(c,g["tcp"],281,122,207);L(c,"UDP-порты",509,94,210,25);var udp=Input(c,g["udp"],509,122,207);
  L(c,"IPSet",23,173,200,25);var ip=Combo(c,new[]{"По списку IP","Не использовать IPSet","Все IP-адреса"},23,201,290);string[] ipm={"loaded","none","any"};ip.SelectedIndex=Array.IndexOf(ipm,B.IpMode());
  L(c,"«Все IP» расширяет обработку трафика.\nДля VRChat начни с текущих настроек сборки.",340,195,380,45,10).ForeColor=Theme.Muted;
  Btn(Page,"Сохранить настройки",31,283,245,43,()=>{string m=modes[mode.SelectedIndex],t=tcp.Text,u=udp.Text,i=ipm[ip.SelectedIndex];Work(()=>B.Settings(m,t,u,i));},true);
  var f=Box(343,249);Title(f,"Пакеты Discord и Game Filter","Выбери один из .bin-файлов исходной сборки. Предыдущий файл сохраняется в backups.");
  string[] files=Directory.GetFiles(B.P("bin"),"*.bin").Select(Path.GetFileName).Where(x=>!x.StartsWith("ACTIVE_")).OrderBy(x=>x).ToArray();
  var dc=Combo(f,files,23,109,485);var gm=Combo(f,files,23,185,485);dc.SelectedIndex=Array.FindIndex(files,x=>File.ReadAllBytes(B.P("bin/"+x)).SequenceEqual(File.ReadAllBytes(B.P("bin/ACTIVE_DISCORD_UDP.bin"))));gm.SelectedIndex=Array.FindIndex(files,x=>File.ReadAllBytes(B.P("bin/"+x)).SequenceEqual(File.ReadAllBytes(B.P("bin/ACTIVE_GAME_UDP.bin"))));L(f,"DISCORD UDP",23,85,300,20,8).ForeColor=Theme.Muted;L(f,"GAME UDP",23,161,300,20,8).ForeColor=Theme.Muted;
  Btn(f,"Применить",526,107,179,37,()=>{string v=dc.Text;Work(()=>B.ReplaceFake("ACTIVE_DISCORD_UDP.bin",v));});Btn(f,"Применить",526,183,179,37,()=>{string v=gm.Text;Work(()=>B.ReplaceFake("ACTIVE_GAME_UDP.bin",v));});
  var a=Box(610,206);Title(a,"Немного личного","Анимации и поведение окна сохраняются на этом компьютере.");Check(a,"Плавные анимации и живой дракончик",23,88,B.Get("motion","yes")=="yes",v=>{B.Pref["motion"]=v?"yes":"no";B.SavePrefs();});Check(a,"Крестик сворачивает приложение в трей",23,124,B.Get("tray","no")=="yes",v=>{B.Pref["tray"]=v?"yes":"no";B.SavePrefs();});Check(a,"Звук успешных действий",23,160,B.Get("sound","no")=="yes",v=>{B.Pref["sound"]=v?"yes":"no";B.SavePrefs();});AppearancePage();ComfortSettings();
 }
 void Lists(){
  string[] files={"list-general-user.txt","list-exclude-user.txt","ipset-exclude-user.txt","list-general.txt","list-exclude.txt","list-google.txt","ipset-all.txt","ipset-exclude.txt"};
  L(Page,"Свои домены и исключения — по одной записи на строку.",32,20,760,30,11).ForeColor=Theme.Muted;
  var combo=Combo(Page,files,32,61,440);var edit=new RoundedTextBox{Bounds=new Rectangle(32,153,ContentWidth-65,365),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top,BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,Font=new Font("Consolas",11),AcceptsTab=true,WordWrap=false,DetectUrls=false};Page.Controls.Add(edit);ListEditor=edit;
  L(Page,"Пользовательские списки сохраняют твои добавления отдельно от штатных.\nДомены — без https:// (знак ^ допустим); IP — с маской, например 192.0.2.0/24.",32,104,800,44,10).ForeColor=Theme.Muted;
  string loaded="";bool loading=false;
  Action load=()=>{loading=true;loaded=combo.Text;string path=B.P("lists/"+loaded);edit.Text=File.Exists(path)?File.ReadAllText(path):loaded.StartsWith("ipset")?"203.0.113.113/32\r\n":"domain.example.abc\r\n";ListBaseline=ListDocument.Normalize(edit.Text);loading=false;};
  combo.SelectedIndexChanged+=(s,e)=>{if(loading)return;if(Dirty&&!Confirm("Отбросить несохранённые изменения списка?")){loading=true;combo.SelectedItem=loaded;loading=false;return;}load();};load();
  Btn(Page,"Сохранить список",32,537,219,43,()=>{string name=loaded,content=edit.Text;try{Backend.ValidateList(name,content);}catch(Exception ex){var m=System.Text.RegularExpressions.Regex.Match(ex.Message,@"строка (\d+):");int line;if(m.Success&&Int32.TryParse(m.Groups[1].Value,out line)){int start=edit.GetFirstCharIndexFromLine(line-1);if(start>=0){edit.Select(start,Math.Max(0,edit.Lines[line-1].Length));edit.ScrollToCaret();edit.Focus();}}throw;}Work(()=>B.SaveList(name,content),()=>ListBaseline=ListDocument.Normalize(content));},true);Btn(Page,"Резервные копии",263,537,213,43,()=>{Directory.CreateDirectory(Path.Combine(B.Data,"backups"));Open(Path.Combine(B.Data,"backups"));});
  L(Page,"Перед каждым сохранением создаётся копия. Активный движок перезапускается автоматически.",32,600,800,40,10).ForeColor=Theme.Muted;
 }
 void Tools(){
  var d=Box(22,155);Title(d,"Проверить, что происходит","Файлы движка, службы, TCP, прокси и возможные конфликты. Результат появится в окне.");Btn(d,"Диагностика",23,97,209,40,ShowDiagnostics,true);Btn(d,"Подбор стратегий",244,97,215,40,()=>Navigate("Проверки"));Btn(d,"Результаты тестов",471,97,224,40,()=>{string p=B.P("utils/test results");Directory.CreateDirectory(p);Open(p);});
  TelegramProxyCard();
  var u=Box(442,159);Title(u,"Актуальные списки","Обновления загружаются из репозитория Flowseal. Ошибка загрузки не заменит файл.");Btn(u,"Обновить IPSet",23,100,209,40,()=>Work(()=>B.UpdateIps()));Btn(u,"Обновление движка",244,100,215,40,UpdateEngine);Btn(u,"Hosts: просмотр",471,100,224,40,DownloadHosts);
  var t=Box(618,159);Title(t,"Обслуживание","Очистка кэша Discord и доступ к исходным инструментам сборки.");Btn(t,"Очистить кэш Discord",23,100,238,40,()=>{if(Confirm("Удалить только Cache, Code Cache и GPUCache Discord? Сначала закрой Discord."))Work(()=>B.ClearDiscord());});Btn(t,"Исходный менеджер",272,100,234,40,()=>{if(B.Preview)return;if(Confirm("Открыть оригинальный service.bat? Его действия могут изменять сетевые настройки и службы."))OpenProcess(Environment.GetEnvironmentVariable("ComSpec")??"cmd.exe","/d /s /c \"\""+B.P("service.bat")+"\" admin\"");});Btn(t,"Папка сборки",517,100,178,40,()=>Open(B.Root));
  var h=Box(794,143);Title(h,"Изменения hosts можно отменить","FurZap добавляет отдельный блок, сохраняя другие записи. Полная копия — в data/backups.");Btn(h,"Удалить блок FurZap",23,90,258,36,()=>{if(Confirm("Удалить из hosts только блок, добавленный FurZap?"))Work(()=>B.RemoveHosts());});
  L(Page,"Встроенный подбор с прогрессом и отменой находится в разделе «Проверки».\nПредыдущая конфигурация возвращается после завершения тестов.",33,954,800,50,10).ForeColor=Theme.Muted;
  UpdateCard();MaintenanceCard();
 }
 void RunTests(){if(B.Preview)return;if(ExternalTool!=null&&!ExternalTool.HasExited){MessageBox.Show(this,"Исходный инструмент уже открыт.");return;}if(B.Running||B.ServiceState()!=0){MessageBox.Show(this,"Сначала останови процесс и удали службу со страницы Служба. После тестов её можно установить снова.","Тесты стратегий");return;}if(!Confirm("Открыть исходные интерактивные тесты? Они запускают стратегии и проверяют сеть; не закрывай окно до завершения."))return;OpenProcess("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File "+Backend.Quote(B.P("utils/test zapret.ps1")));Log("Открыты исходные тесты. Их результат не считается известным до завершения.");}
 void DownloadHosts(){Work(()=>{string s=B.Download("https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts");UI(()=>TextDialog("Hosts — проверь записи перед применением",s,true,value=>{if(Confirm("Добавить эти записи в системный hosts? Существующие записи сохранятся, будет сделана резервная копия."))Work(()=>B.ApplyHosts(value));}));});}
 async void CheckVersion(bool show){if(B.Preview)return;if(show&&Busy)return;try{var release=await Task.Run(()=>EngineUpdater.Latest(B));Version current,next;bool newer=Version.TryParse(B.EngineVersion,out current)&&Version.TryParse(release.Version,out next)&&next>current;Log("Версия Zapret у Flowseal: "+release.Version+". У тебя: "+B.EngineVersion+".");if(newer){Feedback("Доступен Zapret "+release.Version,"Открой Инструменты → Движок и восстановление.");}else if(show)Feedback("Движок актуален","Zapret "+B.EngineVersion);}catch(Exception ex){Log("Проверка обновлений движка: "+ex.Message);if(show)Error(ex);}}
 void Logs(){Btn(Page,"Сохранить журнал",32,20,225,41,()=>{using(var s=new SaveFileDialog{FileName="FurZap-log.txt",Filter="Текстовый файл|*.txt"})if(s.ShowDialog(this)==DialogResult.OK)File.WriteAllText(s.FileName,Journal.ToString(),Encoding.UTF8);},true);Btn(Page,"Очистить экран",269,20,210,41,()=>{Journal.Clear();if(LogView!=null)LogView.Clear();});
  Btn(Page,"Отчёт об ошибке",492,20,230,41,ShowErrorReport);
  LogView=new RoundedTextBox{Bounds=new Rectangle(32,81,ContentWidth-65,Math.Max(450,Page.ClientSize.Height-105)),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top|AnchorStyles.Bottom,BackColor=Theme.Card,ForeColor=Theme.Mint,Font=new Font("Consolas",10),ReadOnly=true,BorderStyle=BorderStyle.None,DetectUrls=false,Text=Journal.ToString()};Page.Controls.Add(LogView);LogView.SelectionStart=LogView.TextLength;LogView.ScrollToCaret();
 }
 public void Log(string value){if(IsDisposed)return;UI(()=>{string line="["+DateTime.Now.ToString("HH:mm:ss")+"] "+value+"\r\n";Journal.Append(line);if(Journal.Length>150000)Journal.Remove(0,50000);try{File.AppendAllText(CurrentLog,line,new UTF8Encoding(false));}catch{}if(LogView!=null&&!LogView.IsDisposed){LogView.Text=Journal.ToString();LogView.SelectionStart=LogView.TextLength;LogView.ScrollToCaret();}Toast.Text=value.Split(new char[] {'\n'})[0].Trim();});}
 void UI(Action a){if(IsDisposed||Disposing)return;if(InvokeRequired)BeginInvoke(a);else a();}
 async void Work(Action action,Action done=null){if(Busy||UpdatingApp||EngineUpdating)return;if(ExternalTool!=null&&!ExternalTool.HasExited){MessageBox.Show(this,"Дождись завершения тестов или закрой исходный менеджер, чтобы действия не пересекались.","FurZap");return;}if(B.Preview){Log("Предпросмотр: действие не выполняется.");return;}Busy=true;Page.Enabled=false;UseWaitCursor=true;var busyButton=ActionButton;string busyText=busyButton==null?null:busyButton.Text;if(busyButton!=null&&!busyButton.IsDisposed)busyButton.Text="Выполняю…";Toast.Text="Выполняю…";try{await Task.Run(action);Busy=false;if(done!=null)done();Feedback("Готово","Действие выполнено.");}catch(Exception ex){Error(ex);}finally{Busy=false;Page.Enabled=true;UseWaitCursor=false;if(busyButton!=null&&!busyButton.IsDisposed)busyButton.Text=busyText;InvalidateChecks();RefreshState();}}
 void Error(Exception ex){LastException=ex.ToString();Log("Ошибка: "+ex.ToString());Feedback("Не получилось",ex.Message,true);MessageBox.Show(this,Backend.FriendlyError(ex),"FurZap — не получилось",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 bool Confirm(string s){return MessageBox.Show(this,s,"FurZap",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;}
 void Open(string path){if(B.Preview)return;Process.Start(new ProcessStartInfo(path){UseShellExecute=true,WorkingDirectory=B.Root});}
 void OpenProcess(string name,string args){if(B.Preview)return;ExternalTool=Process.Start(new ProcessStartInfo(name,args){UseShellExecute=true,WorkingDirectory=B.Root});}
 void TextDialog(string title,string text,bool editable,Action<string> save){var f=new Form{Text=title,Size=new Size(880,650),StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Bg,ForeColor=Theme.Text,MinimumSize=new Size(650,400),Icon=Icon};var area=new RichTextBox{Dock=DockStyle.Fill,BackColor=Theme.Card,ForeColor=Theme.Text,Font=new Font("Consolas",10),Text=text,ReadOnly=!editable,WordWrap=false,BorderStyle=BorderStyle.None,DetectUrls=false};var bar=new Panel{Dock=DockStyle.Bottom,Height=66,BackColor=Theme.Bg};f.Controls.Add(area);f.Controls.Add(bar);if(save!=null)Btn(bar,"Применить",18,12,180,42,()=>{string s=area.Text;f.Close();save(s);},true);Btn(bar,"Скопировать",save==null?18:213,12,180,42,()=>Clipboard.SetText(area.Text));f.Show(this);}
 void Restore(){Show();WindowState=FormWindowState.Normal;Activate();}
 void HandleClosing(object sender,FormClosingEventArgs e){if(B.Preview){Timer.Stop();Tray.Dispose();return;}if(Busy){e.Cancel=true;MessageBox.Show(this,"Дождись завершения текущего действия.","FurZap");return;}if(!AllowExit&&B.Get("tray","no")=="yes"){e.Cancel=true;Hide();return;}if(HasUnsavedLists()&&!Confirm("Есть несохранённый список. Выйти без сохранения?")){e.Cancel=true;return;}if(B.Running){var r=MessageBox.Show(this,"Остановить процесс FurZap и выйти?\n«Нет» — оставить процесс и свернуть окно в трей.","FurZap",MessageBoxButtons.YesNoCancel);if(r==DialogResult.Cancel){e.Cancel=true;AllowExit=false;return;}if(r==DialogResult.No){e.Cancel=true;AllowExit=false;Hide();return;}try{B.Stop();}catch(Exception ex){e.Cancel=true;AllowExit=false;Error(ex);return;}}Timer.Stop();Tray.Dispose();}
 public void PreviewChoose(){if(StrategyList!=null&&StrategyList.Items.Count>1){StrategyList.SelectedIndex=1;ActionButton=Page.Controls.OfType<FButton>().First(x=>x.Text=="Выбрать");ActionButton.ActivateForPreview();}}
 public void PreviewDropdown(){var first=Page.Controls.OfType<Card>().SelectMany(c=>c.Controls.OfType<FCombo>()).FirstOrDefault();if(first!=null)first.ShowPopup();}
 static void RepaintTree(Control c){c.Invalidate();c.Update();foreach(Control child in c.Controls)RepaintTree(child);}
 public void Screenshot(string path){using(var bmp=new Bitmap(ClientSize.Width,ClientSize.Height)){using(var g=Graphics.FromImage(bmp))g.CopyFromScreen(PointToScreen(Point.Empty),Point.Empty,ClientSize);using(var output=new FileStream(path,FileMode.Create,FileAccess.Write)){bmp.Save(output,System.Drawing.Imaging.ImageFormat.Png);output.Flush(true);}}}
}
public static class Program {
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 [STAThread] public static int Main(string[] args){
  if(args.Contains("--install-update"))return AppUpdater.Install(args);
  bool preview=args.Contains("--preview");string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"engine");
  if(args.Contains("--engine-package-test")){try{var b=new Backend(root,false);var r=EngineUpdater.Latest(b);string path=EngineUpdater.Download(b,r,System.Threading.CancellationToken.None,p=>{});EngineUpdater.Validate(path);Console.WriteLine("PASS: official engine ZIP downloaded, verified, extracted and all strategies parsed: "+r.Version);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
  if(args.Contains("--self-test")){try{Tests.Run(root);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   bool windows=Environment.OSVersion.Platform==PlatformID.Win32NT;
   if(!preview&&!windows){MessageBox.Show("Запуск движка поддерживается только в Windows. Для просмотра: --preview");return 1;}
   if(windows){SetProcessDPIAware();if(!preview&&!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)){try{Process.Start(new ProcessStartInfo(Application.ExecutablePath){UseShellExecute=true,Verb="runas"});}catch(Exception ex){MessageBox.Show("Нужны права администратора для драйвера Zapret.\n"+ex.Message);}return 0;}}
   bool created;using(var mutex=new System.Threading.Mutex(true,"FurZap_Cokker_Desktop_1",out created)){if(!created&&!preview){MessageBox.Show("FurZap уже открыт. Проверь значок в трее.");return 0;}
    if(!File.Exists(Path.Combine(root,"bin","winws.exe"))){MessageBox.Show("Распакуй весь архив. Папка engine должна лежать рядом с FurZap.exe.");return 1;}
    var backend=new Backend(root,preview);if(preview&&args.Contains("--ui-regression")){int output=Array.IndexOf(args,"--screenshots");return UIRegression.Run(backend,args[output+1]);}var form=new MainForm(backend);if(preview&&args.Contains("--wide")){form.StartPosition=FormStartPosition.Manual;form.Location=Point.Empty;form.Size=new Size(1920,1080);}
    int index=Array.IndexOf(args,"--screenshots");if(preview&&index>=0&&index+1<args.Length&&!args.Contains("--resize-check")&&!args.Contains("--features-check")&&!args.Contains("--comfort-preview")){string dir=args[index+1];Directory.CreateDirectory(dir);form.Shown+=(s,e)=>{var timer=new System.Windows.Forms.Timer{Interval=700};int step=0;string[] pages={"Главная","Стратегии","Служба","Настройки","Списки","Инструменты","Проверки","Профили","Журнал"};timer.Tick+=(a,b)=>{if(step%2==0&&step/2<pages.Length){form.Navigate(pages[step/2]);if(step/2==1)form.PreviewChoose();if(step/2==3)form.PreviewDropdown();}else if(step/2<pages.Length)form.Screenshot(Path.Combine(dir,"page-"+(step/2)+".png"));step++;if(step>=pages.Length*2){timer.Stop();timer.Dispose();form.Close();}};timer.Start();};}
    if(preview&&args.Contains("--comfort-preview")){string dir=args[index+1];Directory.CreateDirectory(dir);form.Shown+=(ss,ee)=>{int step=0;var t=new System.Windows.Forms.Timer{Interval=900};t.Tick+=(aa,bb)=>{form.PreviewComfortStep(step++,dir);if(step>12){t.Stop();t.Dispose();}};t.Start();};}
    if(preview&&args.Contains("--resize-check")){string dir=args[index+1];Directory.CreateDirectory(dir);form.Shown+=(ss,ee)=>{int phase=0;var t=new System.Windows.Forms.Timer{Interval=900};t.Tick+=(aa,bb)=>{switch(phase++){case 0:form.Screenshot(Path.Combine(dir,"small.png"));break;case 1:form.Location=Point.Empty;form.Size=new Size(1920,1080);break;case 2:form.Screenshot(Path.Combine(dir,"wide.png"));break;case 3:form.Size=new Size(1190,820);break;case 4:form.Screenshot(Path.Combine(dir,"restored.png"));t.Stop();t.Dispose();form.Close();break;}};t.Start();};}
    if(preview&&args.Contains("--features-check")){string dir=args[index+1];Directory.CreateDirectory(dir);form.Shown+=(ss,ee)=>{int phase=0;var t=new System.Windows.Forms.Timer{Interval=800};t.Tick+=(aa,bb)=>{if(phase%2==0)form.PreviewExtras(phase/2);else form.Screenshot(Path.Combine(dir,"feature-"+(phase/2)+".png"));phase++;if(phase==14){t.Stop();t.Dispose();form.Close();}};t.Start();};}
    Application.Run(form);
   }return 0;
  }catch(Exception ex){if(preview)Console.Error.WriteLine(ex.ToString());else MessageBox.Show(Backend.FriendlyError(ex),"FurZap — ошибка запуска");return 1;}
 }
}
}
