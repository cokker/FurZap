using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace FurZap {
public static class Motion {
 static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
 public static double Now{get{return clock.Elapsed.TotalSeconds;}}public static double Delta;
 public static event Action<double> Frame;
 public static void Pulse(double now){var callback=Frame;if(callback!=null)callback(now);}
 public static float Ease(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
 public static float Approach(float value,float target,double rate){return target+(value-target)*(float)Math.Exp(-rate*Delta);}
 public static bool Enabled=true; public static Color Mix(Color a,Color b,float t){t=Math.Max(0,Math.Min(1,t));return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}}
public class FButton:Control {
 public bool Accent,Selected;public string NavIcon="";float hover,press,ripple=1;bool over,down;Point origin;double success;bool animating;
 public FButton(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.Selectable|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;Cursor=Cursors.Hand;Font=Theme.F(10,true);TabStop=true;Size=new Size(160,44);Motion.Frame+=Animate;}
 void Wake(){animating=true;Invalidate();}
 void Animate(double now){if(!animating)return;hover=Motion.Enabled?Motion.Approach(hover,over?1:0,16):over?1:0;press=Motion.Enabled?Motion.Approach(press,down?1:0,22):down?1:0;ripple=Motion.Enabled?Math.Min(1,ripple+(float)(Motion.Delta/.35)):1;Invalidate();if(Math.Abs(hover-(over?1:0))<.01&&Math.Abs(press-(down?1:0))<.01&&ripple>=1&&now>success)animating=false;}
 public void Confirmed(){success=Motion.Now+1.8;Wake();Invalidate();}
 protected override void OnMouseEnter(EventArgs e){over=true;Wake();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){over=false;down=false;Wake();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){down=true;origin=e.Location;ripple=0;Wake();Focus();base.OnMouseDown(e);}protected override void OnMouseUp(MouseEventArgs e){down=false;Wake();base.OnMouseUp(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){origin=new Point(Width/2,Height/2);ripple=0;Wake();OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
 public void ActivateForPreview(){OnClick(EventArgs.Empty);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;bool ok=Motion.Now<success;float inset=Motion.Enabled?press*2:0;Color bg=ok?Color.FromArgb(31,64,55):Accent?Theme.Orange:Selected?Motion.Mix(Theme.Side,Theme.Orange,.15f):Motion.Mix(Theme.Card,Color.FromArgb(49,55,72),Motion.Enabled?hover:over?1:0);using(var p=Theme.Round(new RectangleF(1+inset,1+inset,Width-3-inset*2,Height-3-inset*2),17)){using(var b=new SolidBrush(bg))g.FillPath(b,p);using(var pen=new Pen(ok?Theme.Mint:Focused?Theme.Orange:Selected?Color.FromArgb(122,76,48):Theme.Line))g.DrawPath(pen,p);if(Motion.Enabled&&ripple<1){var state=g.Save();g.SetClip(p);float r=ripple*Width*1.4f;using(var b=new SolidBrush(Color.FromArgb((int)(38*(1-ripple)),255,255,255)))g.FillEllipse(b,origin.X-r,origin.Y-r,r*2,r*2);g.Restore(state);}}
  Color fg=!Enabled?Theme.Muted:ok?Theme.Mint:Accent?Color.FromArgb(43,28,22):Selected?Theme.Orange:Theme.Text;
  if(NavIcon.Length>0){using(var iconFont=new Font("Segoe MDL2 Assets",16,GraphicsUnit.Pixel))using(var brush=new SolidBrush(fg))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(NavIcon,iconFont,brush,new RectangleF(11,0,30,Height),format);TextRenderer.DrawText(g,Text,Font,new Rectangle(43,0,Width-49,Height),fg,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
  else TextRenderer.DrawText(g,ok?(Text=="Выбрать"?"✓  Выбрано":"✓  Готово"):Text,Font,new Rectangle(12,0,Width-24,Height),fg,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);}
 protected override void Dispose(bool disposing){if(disposing)Motion.Frame-=Animate;base.Dispose(disposing);}
}
public class FProgressBar:Control {
 int value=0;
 public int Value{get{return value;}set{value=Math.Max(0,Math.Min(100,value));Invalidate();}}
 public FProgressBar(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);Height=13;Visible=false;AccessibleName="Ход загрузки обновления";}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
  using(var rail=Theme.Round(new RectangleF(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1)),Height))using(var brush=new SolidBrush(Theme.Line))e.Graphics.FillPath(brush,rail);
  float filled=(Width-1)*value/100f;
  if(filled>2)using(var fill=Theme.Round(new RectangleF(0,0,filled,Math.Max(1,Height-1)),Math.Min(Height,filled)))using(var brush=new SolidBrush(Theme.Orange))e.Graphics.FillPath(brush,fill);
 }
}
public class FInput:Control {
 readonly TextBox box=new TextBox{BorderStyle=BorderStyle.None,BackColor=Theme.Side,ForeColor=Theme.Text,Font=Theme.F(11)};
 public void SetZoom(float zoom){box.Font=Theme.F(11*zoom);box.SetBounds(12,(Height-box.PreferredHeight)/2,Math.Max(1,Width-24),box.PreferredHeight);}
 public override string Text {get{return box.Text;}set{box.Text=value;}}
 public FInput(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;Height=36;Controls.Add(box);box.TextChanged+=(s,e)=>OnTextChanged(e);box.GotFocus+=(s,e)=>Invalidate();box.LostFocus+=(s,e)=>Invalidate();Resize+=(s,e)=>box.SetBounds(12,(Height-box.PreferredHeight)/2,Math.Max(1,Width-24),box.PreferredHeight);Click+=(s,e)=>box.Focus();}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.Round(new RectangleF(1,1,Width-3,Height-3),14)){using(var b=new SolidBrush(Theme.Side))e.Graphics.FillPath(b,p);using(var pen=new Pen(box.Focused?Theme.Orange:Theme.Line))e.Graphics.DrawPath(pen,p);}}
}
public class FList:Control {
 public Func<string,string> Caption;
 public List<string> Items=new List<string>();public string Applied="";public bool FileNames;public int RowHeight=38;int selected=-1,hover=-1,offset;bool drag;int dragY,dragOffset;
 public event EventHandler SelectedIndexChanged;public event EventHandler Commit;
 public int SelectedIndex{get{return selected;}set{int v=Math.Max(-1,Math.Min(Items.Count-1,value));if(v==selected)return;selected=v;int rows=Math.Max(1,(Height-16)/RowHeight);if(selected<offset)offset=Math.Max(0,selected);if(selected>=offset+rows)offset=selected-rows+1;Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
 internal void CommitForTest(){if(Commit!=null)Commit(this,EventArgs.Empty);}
 public object SelectedItem{get{return selected>=0&&selected<Items.Count?Items[selected]:null;}set{SelectedIndex=Items.IndexOf(Convert.ToString(value));}}
 int Rows{get{return Math.Max(1,(Height-16)/RowHeight);}}int MaxOffset{get{return Math.Max(0,Items.Count-Rows);}}
 public FList(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.Selectable|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;Font=Theme.F(11);TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.List;}
 protected override bool IsInputKey(Keys key){return (key&Keys.KeyCode)==Keys.Up||(key&Keys.KeyCode)==Keys.Down||(key&Keys.KeyCode)==Keys.Home||(key&Keys.KeyCode)==Keys.End||base.IsInputKey(key);}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Down)SelectedIndex=Math.Min(Items.Count-1,selected+1);else if(e.KeyCode==Keys.Up)SelectedIndex=Math.Max(0,selected-1);else if(e.KeyCode==Keys.Home)SelectedIndex=0;else if(e.KeyCode==Keys.End)SelectedIndex=Items.Count-1;else if(e.KeyCode==Keys.Enter&&Commit!=null)Commit(this,e);else{base.OnKeyDown(e);return;}e.Handled=true;}
 internal bool ScrollWheel(int delta){int previous=offset;int lines=SystemInformation.MouseWheelScrollLines;if(lines==0)return true;int step=lines<0?Rows:lines;int amount=(int)Math.Round(delta/120.0*step);if(amount==0&&delta!=0)amount=Math.Sign(delta);offset=Math.Max(0,Math.Min(MaxOffset,offset-amount));Invalidate();return offset!=previous;}
 protected override void OnMouseWheel(MouseEventArgs e){if(!ScrollWheel(e.Delta))base.OnMouseWheel(e);}
 protected override void OnMouseMove(MouseEventArgs e){if(drag){offset=Math.Max(0,Math.Min(MaxOffset,dragOffset+(int)((e.Y-dragY)*(double)Items.Count/Math.Max(1,Height-16))));}else hover=(e.Y-8)/RowHeight+offset;Invalidate();base.OnMouseMove(e);}
 protected override void OnMouseLeave(EventArgs e){hover=-1;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){Focus();if(e.X>=Width-18&&MaxOffset>0){drag=true;dragY=e.Y;dragOffset=offset;Capture=true;}else{int i=(e.Y-8)/RowHeight+offset;if(e.Y>=8&&i<Items.Count){SelectedIndex=i;if(Commit!=null)Commit(this,e);}}base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){drag=false;Capture=false;base.OnMouseUp(e);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;offset=Math.Max(0,Math.Min(offset,MaxOffset));using(var p=Theme.Round(new RectangleF(1,1,Width-3,Height-3),18)){using(var b=new SolidBrush(Theme.Card))g.FillPath(b,p);using(var pen=new Pen(Focused?Color.FromArgb(94,73,57):Theme.Line))g.DrawPath(pen,p);}if(Items.Count==0){Theme.Txt(g,"Ничего не найдено",18,20,11,Theme.Muted);return;}
 for(int row=0;row<Rows&&row+offset<Items.Count;row++){int i=row+offset;var rect=new Rectangle(8,8+row*RowHeight,Width-28,RowHeight-3);if(i==selected||i==hover){using(var p=Theme.Round(rect,12))using(var b=new SolidBrush(i==selected?Motion.Mix(Theme.Card,Theme.Orange,.18f):Color.FromArgb(37,43,58)))g.FillPath(b,p);}bool applied=Items[i]==Applied;string text=FileNames?System.IO.Path.GetFileNameWithoutExtension(Items[i]):Items[i];if(Caption!=null)text=Caption(Items[i]);TextRenderer.DrawText(g,text,Font,new Rectangle(20,rect.Y,Width-(applied?160:50),rect.Height),i==selected?Theme.Orange:Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(applied)TextRenderer.DrawText(g,"✓ Выбрано",Theme.F(9,true),new Rectangle(Width-132,rect.Y,105,rect.Height),Theme.Mint,TextFormatFlags.VerticalCenter);}
 if(MaxOffset>0){float h=Math.Max(25,(Height-16)*Rows/(float)Items.Count),y=8+(Height-16-h)*offset/(float)MaxOffset;using(var p=Theme.Round(new RectangleF(Width-12,y,5,h),5))using(var b=new SolidBrush(Color.FromArgb(89,98,119)))g.FillPath(b,p);}}
}
public class DarkPopupRenderer:ToolStripProfessionalRenderer {protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){}protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){using(var b=new SolidBrush(Theme.Card))e.Graphics.FillRectangle(b,e.AffectedBounds);}}
public class FCombo:Control {
 public List<string> Items=new List<string>();int selected=-1;bool over;ToolStripDropDown popup;public event EventHandler SelectedIndexChanged;
 public int SelectedIndex{get{return selected;}set{int v=Math.Max(-1,Math.Min(Items.Count-1,value));if(v==selected)return;selected=v;Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
 public object SelectedItem{get{return selected>=0?Items[selected]:null;}set{SelectedIndex=Items.IndexOf(Convert.ToString(value));}}
 public override string Text{get{return selected>=0&&selected<Items.Count?Items[selected]:"";}set{SelectedItem=value;}}
 public FCombo(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.Selectable|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;Font=Theme.F(10);Cursor=Cursors.Hand;TabStop=true;Height=36;AccessibleRole=AccessibleRole.ComboBox;}
 protected override bool IsInputKey(Keys key){return key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Down&&!e.Alt)SelectedIndex=Math.Min(Items.Count-1,selected+1);else if(e.KeyCode==Keys.Up)SelectedIndex=Math.Max(0,selected-1);else if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space||(e.Alt&&e.KeyCode==Keys.Down))ShowPopup();else{base.OnKeyDown(e);return;}e.Handled=true;}
 protected override void OnClick(EventArgs e){Focus();ShowPopup();base.OnClick(e);}protected override void OnMouseEnter(EventArgs e){over=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){over=false;Invalidate();base.OnMouseLeave(e);}protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 FList popupList;
 public void ShowPopup(){
  if(IsDisposed||Disposing||!IsHandleCreated||Items.Count==0)return;
  if(popup!=null&&popup.Visible){popup.Close();return;}
  if(popup==null){
   popupList=new FList();var list=popupList;
   var host=new ToolStripControlHost(list){Padding=Padding.Empty,Margin=Padding.Empty,AutoSize=false};
   var window=new ToolStripDropDown{Padding=Padding.Empty,Margin=Padding.Empty,AutoSize=false,BackColor=Theme.Bg,DropShadowEnabled=false,Renderer=new DarkPopupRenderer()};popup=window;window.Items.Add(host);
   // Closed is still inside ToolStrip's native close path. Keep the popup alive
   // until its owner is disposed; never dispose it from Closed/Commit.
   window.Closed+=PopupClosed;
   list.Commit+=(s,e)=>{int chosen=list.SelectedIndex;window.Close();if(!IsDisposed&&!Disposing){SelectedIndex=chosen;if(!IsDisposed&&!Disposing){Focus();Invalidate();}}};
  }
  int row=Math.Max(38,Font.Height+19);popupList.Font=Font;popupList.RowHeight=row;popupList.Items.Clear();popupList.Items.AddRange(Items);popupList.SelectedIndex=selected;
  popupList.Size=new Size(Math.Max(Width,250),Math.Min(8,Items.Count)*row+16);popup.Items[0].Size=popupList.Size;popup.Size=popupList.Size;
  Region oldRegion=popup.Region;using(var shape=Theme.Round(new RectangleF(0,0,popup.Width,popup.Height),18))popup.Region=new Region(shape);if(oldRegion!=null)oldRegion.Dispose();
  popup.Show(this,new Point(0,Height+4));popupList.Focus();Invalidate();
 }
 void PopupClosed(object sender,ToolStripDropDownClosedEventArgs e){if(!IsDisposed&&!Disposing)Invalidate();}
 internal void CommitForTest(int index){popupList.SelectedIndex=index;popupList.CommitForTest();}
 internal bool PopupDisposedForTest{get{return popup!=null&&popup.IsDisposed;}}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.Round(new RectangleF(1,1,Width-3,Height-3),14)){using(var b=new SolidBrush(over?Color.FromArgb(32,37,49):Theme.Side))g.FillPath(b,p);using(var pen=new Pen(Focused||(popup!=null&&popup.Visible)?Theme.Orange:Theme.Line))g.DrawPath(pen,p);}TextRenderer.DrawText(g,Text,Font,new Rectangle(12,0,Width-42,Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);using(var pen=new Pen(Theme.Orange,1.7f)){int x=Width-23,y=Height/2-2;g.DrawLines(pen,new[]{new Point(x-4,y),new Point(x,y+4),new Point(x+4,y)});}}
 protected override void Dispose(bool disposing){if(disposing&&popup!=null){var old=popup;popup=null;popupList=null;old.Closed-=PopupClosed;old.Dispose();}base.Dispose(disposing);}
}
public class FToggle:Control {
 bool isChecked; public event EventHandler CheckedChanged;
 public bool Checked {get{return isChecked;}set{if(isChecked==value)return;isChecked=value;if(Motion.Enabled)Wake();else position=value?1:0;Invalidate();if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);}}
 protected override void OnClick(EventArgs e){Checked=!Checked;Focus();base.OnClick(e);}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){Checked=!Checked;e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
 protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Theme.Card);}

 float position;bool animating;
 void Wake(){animating=true;Invalidate();}
 void Animate(double now){if(!animating)return;position=Motion.Enabled?Motion.Approach(position,Checked?1:0,18):Checked?1:0;Invalidate();if(Math.Abs(position-(Checked?1:0))<.005){position=Checked?1:0;animating=false;}}
 public FToggle(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.Selectable|ControlStyles.SupportsTransparentBackColor,true);BackColor=Theme.Card;TabStop=true;AccessibleRole=AccessibleRole.CheckButton;Cursor=Cursors.Hand;Motion.Frame+=Animate;}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float t=Motion.Enabled?position:Checked?1:0;int y=(Height-22)/2;using(var p=Theme.Round(new RectangleF(1,y,38,22),22))using(var b=new SolidBrush(Motion.Mix(Theme.Line,Theme.Orange,t)))g.FillPath(b,p);using(var b=new SolidBrush(Checked?Theme.Side:Theme.Text))g.FillEllipse(b,4+16*t,y+3,16,16);TextRenderer.DrawText(g,Text,Font,new Rectangle(50,0,Width-54,Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(Focused&&ShowFocusCues){using(var path=Theme.Round(new RectangleF(0,y-2,42,26),26))using(var pen=new Pen(Theme.Muted))g.DrawPath(pen,path);}}
 protected override void Dispose(bool disposing){if(disposing)Motion.Frame-=Animate;base.Dispose(disposing);}
}
public class ScrollRail:Control {
 Panel page;bool drag;public ScrollRail(Panel p){page=p;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);BackColor=Theme.Bg;Cursor=Cursors.Hand;page.Scroll+=(s,e)=>Invalidate();page.Layout+=(s,e)=>Invalidate();page.Resize+=(s,e)=>Invalidate();page.Invalidated+=PageInvalidated;}
 protected override void OnPaint(PaintEventArgs e){int full=page.DisplayRectangle.Height,view=page.ClientSize.Height;if(full<=view)return;float h=Math.Max(32,Height*view/(float)full),y=(Height-h)*(-page.AutoScrollPosition.Y)/(float)Math.Max(1,full-view);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.Round(new RectangleF(3,y,5,h),5))using(var b=new SolidBrush(Color.FromArgb(83,91,112)))e.Graphics.FillPath(b,p);}
 void PageInvalidated(object sender,InvalidateEventArgs e){Invalidate();}
 protected override void Dispose(bool disposing){if(disposing)page.Invalidated-=PageInvalidated;base.Dispose(disposing);}
 internal float ThumbFraction{get{return Math.Max(0,Math.Min(1,-page.AutoScrollPosition.Y/(float)Math.Max(1,page.DisplayRectangle.Height-page.ClientSize.Height)));}}
 void MoveTo(int y){int full=page.DisplayRectangle.Height,view=page.ClientSize.Height;page.AutoScrollPosition=new Point(0,Math.Max(0,(int)((y/(float)Math.Max(1,Height))*(full-view))));Invalidate();}
 protected override void OnMouseDown(MouseEventArgs e){drag=true;Capture=true;MoveTo(e.Y);base.OnMouseDown(e);}protected override void OnMouseMove(MouseEventArgs e){if(drag)MoveTo(e.Y);base.OnMouseMove(e);}protected override void OnMouseUp(MouseEventArgs e){drag=false;Capture=false;base.OnMouseUp(e);}
}
public class FeedbackCard:Control {
 public string Message="",Heading="Готово";public bool Failure;
 public FeedbackCard(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;Visible=false;Height=79;Width=470;}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color color=Failure?Color.FromArgb(255,143,135):Theme.Mint;using(var p=Theme.Round(new RectangleF(1,1,Width-3,Height-3),20)){using(var b=new SolidBrush(Color.FromArgb(30,37,45)))g.FillPath(b,p);using(var pen=new Pen(color))g.DrawPath(pen,p);}Theme.Txt(g,Failure?"!":"✓",17,20,23,color,true);Theme.Txt(g,Heading,58,12,11,color,true);TextRenderer.DrawText(g,Message,Theme.F(9),new Rectangle(59,37,Width-76,29),Theme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);}
}
}
