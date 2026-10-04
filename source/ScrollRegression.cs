using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
namespace FurZap {
public partial class MainForm {
 internal void VerifyScrollUi(Action<bool,string> check){
  foreach(bool maximized in new[]{false,true}){
   WindowState=maximized?FormWindowState.Maximized:FormWindowState.Normal;
   foreach(string section in new[]{"Главная","Стратегии","Служба","Настройки","Списки","Инструменты","Обновления","Проверки","Профили","Журнал"}){
    Navigate(section);Page.AutoScrollPosition=Point.Empty;Page.PerformLayout();
    var original=Page.Controls.Cast<Control>().ToDictionary(c=>c,c=>c.Top);
    using(var router=new PageWheelRouter(Page)){
     Control target=Page.Controls.Cast<Control>().First();
     router.Route(target,-120,new IntPtr(unchecked(-120<<16)),IntPtr.Zero);
     int max=Math.Max(0,Page.DisplayRectangle.Height-Page.ClientSize.Height);
     check(max==0||Page.AutoScrollPosition.Y<0,"wheel over child scrolls "+section+" max="+maximized);
     Page.AutoScrollPosition=new Point(0,100000);int position=Page.AutoScrollPosition.Y;
     Navigate(section=="Главная"?"Служба":"Главная");Navigate(section);
     check(Math.Abs(Page.AutoScrollPosition.Y-position)<=1,"cached scroll restored "+section+" expected="+position+" actual="+Page.AutoScrollPosition.Y);
     ViewLayout();Page.AutoScrollPosition=Point.Empty;
     check(original.All(x=>x.Key.Top==x.Value),"no blank offset after relayout "+section+" "+String.Join(";",original.Where(x=>x.Key.Top!=x.Value).Select(x=>x.Value+"->"+x.Key.Top)));
     for(int i=0;i<5;i++)router.Route(target,120,new IntPtr(120<<16),IntPtr.Zero);
     check(Page.AutoScrollPosition.Y==0&&Page.Top==0,"upper boundary clamped "+section);
    }
   }
  }
  Navigate("Настройки");Page.AutoScrollPosition=Point.Empty;
  var rail=Page.Parent.Controls.OfType<ScrollRail>().First();int repaint=0;rail.Invalidated+=(s,e)=>repaint++;
  using(var router=new PageWheelRouter(Page)){router.Route(Page,-120,new IntPtr(unchecked(-120<<16)),IntPtr.Zero);check(repaint>0&&rail.ThumbFraction>0,"wheel repaints right scroll rail and moves thumb");}
  WindowState=FormWindowState.Normal;Navigate("Стратегии");Page.AutoScrollPosition=Point.Empty;
  using(var router=new PageWheelRouter(Page)){
   StrategyList.SelectedIndex=0;router.Route(StrategyList,-120,new IntPtr(unchecked(-120<<16)),IntPtr.Zero);
   check(Page.AutoScrollPosition.Y==0,"nested list consumes wheel while it can scroll");
   StrategyList.SelectedIndex=StrategyList.Items.Count-1;router.Route(StrategyList,-120,new IntPtr(unchecked(-120<<16)),IntPtr.Zero);
   check(Page.AutoScrollPosition.Y<0,"nested list bottom passes wheel to page");
   StrategyList.SelectedIndex=0;int before=Page.AutoScrollPosition.Y;router.Route(StrategyList,120,new IntPtr(120<<16),IntPtr.Zero);
   check(Page.AutoScrollPosition.Y>before,"nested list top passes wheel to page");
  }
  Navigate("Списки");check(ListEditor is RoundedTextBox&&ListEditor.Region!=null,"list editor rounded");
  Navigate("Проверки");var output=(RichTextBox)Page.Controls["check-output"];
  check(output is RoundedTextBox&&output.Region!=null,"check output rounded");
  check(output.Height<=110*RenderZoom,"check status compact");
  Navigate("Настройки");
  Page.AutoScrollPosition=Point.Empty;
  var combo=Page.Controls.OfType<Card>().SelectMany(c=>c.Controls.OfType<FCombo>()).First(c=>c.Items.Count>8);
  combo.ShowPopup();var dropdown=combo.PopupListForTest;
  check(dropdown!=null&&combo.PopupBoundsForTest.Width>0,"game packet dropdown opens");
  using(var router=new PageWheelRouter(Page)){
   dropdown.SelectedIndex=0;Point point=new Point(combo.PopupBoundsForTest.Left+20,combo.PopupBoundsForTest.Top+20);
   IntPtr location=new IntPtr(((long)(ushort)point.Y<<16)|(ushort)point.X);
   var down=Message.Create(dropdown.Handle,0x020A,new IntPtr(unchecked(-120<<16)),location);
   check(router.PreFilterMessage(ref down)&&dropdown.ScrollOffsetForTest>0&&Page.AutoScrollPosition.Y==0,"wheel over open dropdown scrolls list first");
   dropdown.SelectedIndex=dropdown.Items.Count-1;
   check(router.PreFilterMessage(ref down)&&Page.AutoScrollPosition.Y<0,"dropdown bottom passes wheel to page");
   var up=Message.Create(dropdown.Handle,0x020A,new IntPtr(120<<16),location);
   dropdown.SelectedIndex=0;int before=Page.AutoScrollPosition.Y;
   check(router.PreFilterMessage(ref up)&&Page.AutoScrollPosition.Y>before,"dropdown top passes wheel to page");
  }
  combo.ClosePopupForTest();
 }
}
}
