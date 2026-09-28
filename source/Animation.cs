using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
namespace FurZap {
public static class ListDocument {
 public static string Normalize(string text){return (text??"").Replace("\r\n","\n").Replace("\r","\n");}
 public static bool Changed(string baseline,string current){return !String.Equals(Normalize(baseline),Normalize(current),StringComparison.Ordinal);}
}
// One coalesced UI callback per deadline. Missed frames are skipped, never queued.
// All control state and painting stay on the UI thread.
public sealed class FramePump:IDisposable {
 const double Step=1.0/60;readonly Control owner;readonly Action<double> frame;readonly System.Threading.Timer timer;readonly object gate=new object();bool running,disposed,resolution;int pending;double next;public int Pulses,Posts,Skipped;
 [DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint period);
 [DllImport("winmm.dll")]static extern uint timeEndPeriod(uint period);
 public FramePump(Control owner,Action<double> frame){this.owner=owner;this.frame=frame;timer=new System.Threading.Timer(Pulse,null,Timeout.Infinite,Timeout.Infinite);}
 public void Start(){lock(gate){if(running||disposed)return;if(Environment.OSVersion.Platform==PlatformID.Win32NT)resolution=timeBeginPeriod(1)==0;running=true;next=Motion.Now;timer.Change(0,Timeout.Infinite);}}
 public void Stop(){lock(gate){if(!running)return;running=false;timer.Change(Timeout.Infinite,Timeout.Infinite);if(resolution){timeEndPeriod(1);resolution=false;}}}
 void Pulse(object state){lock(gate){if(!running||disposed)return;Pulses++;double now=Motion.Now;if(now>=next){next+=Step;if(next<=now)next=now+Step;if(Interlocked.CompareExchange(ref pending,1,0)==0){try{Posts++;owner.BeginInvoke((Action)(()=>{try{bool active;lock(gate)active=running&&!disposed;if(active&&!owner.IsDisposed)frame(Motion.Now);}finally{Interlocked.Exchange(ref pending,0);}}));}catch(InvalidOperationException){Interlocked.Exchange(ref pending,0);}}else Skipped++;}timer.Change(Math.Max(1,(int)Math.Ceiling((next-Motion.Now)*1000)),Timeout.Infinite);}}
 public void Dispose(){Stop();lock(gate){if(disposed)return;disposed=true;timer.Dispose();}}
}
public sealed class PoseBlend {
 public readonly float[] Weights={1,0,0};readonly float[] from={1,0,0};int target;double started;
 public void Set(int pose,double now,bool reduced){Update(now,reduced);if(pose==target)return;Array.Copy(Weights,from,3);target=pose;started=now;if(reduced)Update(now,true);}
 public void Update(double now,bool reduced){float t=reduced?1:Motion.Ease((float)((now-started)/.24));for(int i=0;i<3;i++)Weights[i]=from[i]*(1-t)+(i==target?t:0);}
}
public class Dragon:Control {
 public bool Reduced,Sleeping,Checking;readonly PoseBlend blend=new PoseBlend();double reactionStart=-10,reactionUntil=-10,now;bool sad;int lastPose=-1;bool lastReduced,lastChecking,lastReacting,lastSad;Bitmap[] cache;Bitmap background;Control backgroundParent;int cacheSize;public int PaintedFrames;public double PaintSeconds;
 static readonly Image[] Art={Load("FurZap.Dragon.png"),Load("FurZap.DragonHappy.png"),Load("FurZap.DragonSleep.png")};
 static Image Load(string name){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(name))using(var source=Image.FromStream(stream))return new Bitmap(source);}
 public Dragon(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;MouseEnter+=(s,e)=>React();}
 void ResetBackground(object sender,EventArgs e){if(background!=null){background.Dispose();background=null;}}
 protected override void OnParentChanged(EventArgs e){if(backgroundParent!=null){backgroundParent.SizeChanged-=ResetBackground;backgroundParent.BackColorChanged-=ResetBackground;}base.OnParentChanged(e);backgroundParent=Parent;if(backgroundParent!=null){backgroundParent.SizeChanged+=ResetBackground;backgroundParent.BackColorChanged+=ResetBackground;}ResetBackground(this,e);}
 protected override void OnLocationChanged(EventArgs e){ResetBackground(this,e);base.OnLocationChanged(e);}
 protected override void OnSizeChanged(EventArgs e){ResetBackground(this,e);base.OnSizeChanged(e);}
 protected override void OnPaintBackground(PaintEventArgs e){if(Width<1||Height<1||Parent==null){base.OnPaintBackground(e);return;}if(background==null){background=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(background)){g.TranslateTransform(-Left,-Top);var args=new PaintEventArgs(g,new Rectangle(Left,Top,Width,Height));InvokePaintBackground(Parent,args);InvokePaint(Parent,args);}}e.Graphics.DrawImageUnscaled(background,0,0);}
 public void React(bool failed=false){sad=failed;reactionStart=Motion.Now;reactionUntil=reactionStart+1.8;Advance(reactionStart);Invalidate();}
 public void Advance(double time){now=time;bool reacting=now<reactionUntil;int pose=reacting&&!sad?1:Sleeping&&!Checking?2:0;blend.Set(pose,now,Reduced);if(Visible&&(!Reduced||pose!=lastPose||Reduced!=lastReduced||Checking!=lastChecking||reacting!=lastReacting||sad!=lastSad))Invalidate();lastPose=pose;lastReduced=Reduced;lastChecking=Checking;lastReacting=reacting;lastSad=sad;}
 void EnsureCache(){int pixels=Math.Max(1,(int)Math.Ceiling(Math.Min(Width,Height)*.96));if(cache!=null&&pixels==cacheSize)return;ReleaseCache();cacheSize=pixels;cache=new Bitmap[3];for(int i=0;i<3;i++){cache[i]=new Bitmap(pixels,pixels,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(cache[i])){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(Art[i],new Rectangle(0,0,pixels,pixels));}}}
 void ReleaseCache(){if(cache!=null){foreach(var image in cache)image.Dispose();cache=null;}}
 protected override void OnPaint(PaintEventArgs e){double paintStart=Motion.Now;PaintedFrames++;EnsureCache();var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.Bilinear;float unit=Math.Min(Width,Height),cx=Width/2f,cy=Height/2f;
  using(var halo=new SolidBrush(Color.FromArgb(22,Theme.Orange)))g.FillEllipse(halo,cx-unit*.42f,cy-unit*.42f,unit*.84f,unit*.84f);using(var pen=new Pen(Color.FromArgb(45,Theme.Orange),1))g.DrawEllipse(pen,cx-unit*.47f,cy-unit*.47f,unit*.94f,unit*.94f);
  float wave=Reduced?0:(float)Math.Sin(now/.88),size=cacheSize,y=(Height-size)/2+wave*2.5f;bool reacting=now<reactionUntil;float phase=(float)((now-reactionStart)/1.8);if(reacting&&!Reduced){float bounce=(float)Math.Sin(Math.Max(0,phase)*Math.PI);y-=bounce*unit*.07f;g.TranslateTransform(cx,cy);g.RotateTransform((sad?-1:1)*bounce*5);g.TranslateTransform(-cx,-cy);}
  var spriteState=g.Save();g.TranslateTransform((Width-size)/2,y);g.PixelOffsetMode=PixelOffsetMode.HighQuality;var destination=new Rectangle(0,0,cacheSize,cacheSize);float accumulated=0;for(int i=0;i<3;i++){float weight=blend.Weights[i];if(weight<.001f)continue;accumulated+=weight;float alpha=weight/accumulated;if(alpha>.999f){g.DrawImageUnscaled(cache[i],0,0);continue;}using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);g.DrawImage(cache[i],destination,0,0,cacheSize,cacheSize,GraphicsUnit.Pixel,attributes);}}g.Restore(spriteState);
  if(Checking){using(var ring=new Pen(Theme.Orange,2))g.DrawArc(ring,cx-unit*.46f,cy-unit*.46f,unit*.92f,unit*.92f,Reduced?0:(float)(now*100%360),75);}if(Sleeping&&!reacting&&!Checking)Theme.Txt(g,"z z",Width-40,10,12,Theme.Muted);if(reacting)Theme.Txt(g,sad?"!":"+",Width-24,18,16,sad?Color.Salmon:Theme.Mint,true);PaintSeconds+=Motion.Now-paintStart;
 }
 protected override void Dispose(bool disposing){if(disposing){ReleaseCache();ResetBackground(this,EventArgs.Empty);if(backgroundParent!=null){backgroundParent.SizeChanged-=ResetBackground;backgroundParent.BackColorChanged-=ResetBackground;}}base.Dispose(disposing);}
}
public partial class MainForm {
 double lastFrame,nextStateRefresh,navigationStarted=-1;public int AnimationFrames;
 void AnimateFrame(double now){AnimationFrames++;Motion.Enabled=B.Get("motion","yes")!="no";Motion.Delta=Math.Max(0,now-lastFrame);lastFrame=now;Motion.Pulse(now);if(repaintAfter>0&&now>=repaintAfter){repaintAfter=0;ViewLayout();RepaintTree(this);}SidebarPet.Reduced=!Motion.Enabled;SidebarPet.Advance(now);if(HeroBox!=null&&!HeroBox.IsDisposed){HeroBox.Pet.Reduced=!Motion.Enabled;HeroBox.Pet.Advance(now);}
  if(noticeUntil>0){Rectangle old=Notice.Bounds;double remaining=noticeUntil-now;float slide=Motion.Enabled?(1-Motion.Ease((float)((3.6-remaining)/.18)))+1-Motion.Ease((float)(remaining/.18)):0;Notice.Top=Notice.Parent.ClientSize.Height-Notice.Height-48+(int)(slide*20);if(remaining<=0){Notice.Visible=false;noticeUntil=0;}if(old!=Notice.Bounds||!Notice.Visible)Notice.Parent.Invalidate(old,true);}
  if(navigationStarted>=0){float t=Motion.Enabled?Motion.Ease((float)((now-navigationStarted)/.15)):1;Opacity=.94+.06*t;if(t>=1)navigationStarted=-1;}
  if(now>=nextStateRefresh){nextStateRefresh=now+2;RefreshState();}
 }
 protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);UpdateFramePump();}
 void UpdateFramePump(){if(Timer==null)return;if(Visible&&WindowState!=FormWindowState.Minimized)Timer.Start();else Timer.Stop();}
 protected override void OnFormClosed(FormClosedEventArgs e){if(Timer!=null)Timer.Dispose();base.OnFormClosed(e);}
}
}
