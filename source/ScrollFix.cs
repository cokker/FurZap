using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace FurZap {
// Route wheel input by pointer location, independent of keyboard focus.
public sealed class PageWheelRouter : IMessageFilter, IDisposable {
 readonly Panel page;
 [StructLayout(LayoutKind.Sequential)] struct ScrollInfo {
  public uint cbSize, fMask; public int nMin,nMax; public uint nPage; public int nPos,nTrackPos;
 }
 [DllImport("user32.dll")] static extern bool GetScrollInfo(IntPtr h,int bar,ref ScrollInfo info);
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
 public PageWheelRouter(Panel value){page=value;Application.AddMessageFilter(this);}
 public void Dispose(){Application.RemoveMessageFilter(this);}
 public bool PreFilterMessage(ref Message m){
  if(m.Msg!=0x020A||page.IsDisposed||!page.Visible||!page.Enabled)return false;
  long xy=m.LParam.ToInt64();Point screen=new Point(unchecked((short)(xy&65535)),unchecked((short)((xy>>16)&65535)));
  int delta=unchecked((short)((m.WParam.ToInt64()>>16)&65535));
  bool popupScrolled;
  if(FCombo.TryScrollPopup(page,screen,delta,out popupScrolled)){
   if(!popupScrolled)Route(page,delta,m.WParam,m.LParam);
   return true;
  }
  if(!page.RectangleToScreen(page.ClientRectangle).Contains(screen))return false;
  Control target=page;
  while(true){Control child=target.GetChildAtPoint(target.PointToClient(screen),GetChildAtPointSkip.Invisible|GetChildAtPointSkip.Disabled);if(child==null)break;target=child;}
  Route(target,delta,m.WParam,m.LParam);return true;
 }
 internal void Route(Control target,int delta,IntPtr w,IntPtr l){
  for(Control c=target;c!=null&&c!=page;c=c.Parent){
   FList list=c as FList;if(list!=null&&list.ScrollWheel(delta))return;
   RichTextBox text=c as RichTextBox;
   if(text!=null&&Environment.OSVersion.Platform==PlatformID.Win32NT){
    ScrollInfo info=new ScrollInfo();info.cbSize=(uint)Marshal.SizeOf(typeof(ScrollInfo));info.fMask=0x17;
    if(GetScrollInfo(text.Handle,1,ref info)){
     int max=Math.Max(info.nMin,info.nMax-(int)info.nPage+1);
     if((delta>0&&info.nPos>info.nMin)||(delta<0&&info.nPos<max)){
      SendMessage(text.Handle,0x020A,w,l);return;
     }
    }
   }
  }
  int lines=SystemInformation.MouseWheelScrollLines;if(lines==0)return;
  int step=lines<0?page.ClientSize.Height:Math.Max(1,lines)*20;
  int amount=(int)Math.Round(delta/120.0*step);if(amount==0&&delta!=0)amount=Math.Sign(delta);
  int maxPage=Math.Max(0,page.DisplayRectangle.Height-page.ClientSize.Height);
  page.AutoScrollPosition=new Point(0,Math.Max(0,Math.Min(maxPage,-page.AutoScrollPosition.Y-amount)));
  page.Invalidate(true);
 }
}
public class RoundedTextBox : RichTextBox {
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,ref Rectangle rect);
 protected override void OnResize(EventArgs e){base.OnResize(e);UpdateShape();}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UpdateShape();}
 void UpdateShape(){
  if(Width<2||Height<2)return;
  Region old=Region;using(var shape=Theme.Round(new RectangleF(0,0,Width,Height),18))Region=new Region(shape);if(old!=null)old.Dispose();
  if(IsHandleCreated&&Environment.OSVersion.Platform==PlatformID.Win32NT){
   // EM_SETRECT takes RECT (left, top, right, bottom), not width/height.
   Rectangle rect=new Rectangle(12,10,Math.Max(13,ClientSize.Width-12),Math.Max(11,ClientSize.Height-10));
   SendMessage(Handle,0x00B3,IntPtr.Zero,ref rect);
  }
 }
}
}

