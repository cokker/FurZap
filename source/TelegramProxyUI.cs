using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
namespace FurZap {
public partial class MainForm {
 Label TgStatus;
 void RefreshTelegramStatus(TelegramProxyState state){
  string stateText=state.Running?"работает":state.OtherRunning?"запущена другая копия":"остановлен";
  if(HomeProxyStatus!=null&&!HomeProxyStatus.IsDisposed)HomeProxyStatus.Text="TG WS Proxy · "+stateText;
  string endpoint="";
  if(state.Running||state.OtherRunning)try{var connection=TgProxy.Connection();endpoint=" · "+connection.Host+":"+connection.Port;}catch(Exception){endpoint=" · открой настройки прокси в трее";}
  if(HomeTgDetails!=null&&!HomeTgDetails.IsDisposed)HomeTgDetails.Text="Telegram: "+stateText+endpoint;
  if(TgStatus!=null&&!TgStatus.IsDisposed){TgStatus.Text="Состояние: "+stateText+endpoint+(state.Path==null?"":"\nФайл: "+state.Path);Hints.SetToolTip(TgStatus,state.Path??"Прокси из другой папки управляется через его собственный трей.");}
 }
 async void ConnectTelegram(){
  if(Busy||B.Preview)return;
  try{
   var state=TgProxy.State();
   if(!state.Running&&!state.OtherRunning){
    Busy=true;UseWaitCursor=true;
    try{await Task.Run(()=>TgProxy.Start());}finally{Busy=false;UseWaitCursor=false;RefreshState();}
   }
   var connection=TgProxy.Connection();Open(connection.Link);
   Feedback("Telegram открыт","Подтверди подключение к прокси в Telegram Desktop.");
  }catch(Exception ex){Error(ex);}
 }
 void CopyTelegramLink(){
  var state=TgProxy.State();if(!state.Running&&!state.OtherRunning){Feedback("Прокси остановлен","Сначала запусти TG WS Proxy.");return;}
  Clipboard.SetText(TgProxy.Connection().Link);
  Feedback("Ссылка скопирована","Вставь её в Telegram Desktop. Не публикуй ссылку с секретом.");
 }
 void TelegramProxyCard(){
  var card=Box(194,231);
  Title(card,"Telegram · TG WS Proxy","Локальный MTProto-прокси Flowseal. Работает отдельно от winws; подключи его в Telegram Desktop.");
  TgStatus=L(card,"Состояние: проверяю…",23,77,690,40,9,true);TgStatus.ForeColor=Theme.Mint;
  Btn(card,"Подключить Telegram",23,120,219,38,ConnectTelegram);
  Btn(card,"Запустить",254,120,172,38,()=>Work(()=>TgProxy.Start(),()=>ReloadPage("Инструменты")),true);
  Btn(card,"Остановить",438,120,172,38,()=>Work(()=>TgProxy.Stop(),()=>ReloadPage("Инструменты")));
  Btn(card,"Скопировать ссылку",23,172,219,36,CopyTelegramLink);
  Btn(card,"Папка прокси",254,172,205,36,()=>{string path=TgProxy.Executable;if(path!=null)Open(Path.GetDirectoryName(path));else Feedback("Прокси встроен в EXE","При первом запуске файл появится в data/tools.");});
  Btn(card,"Инструкция Flowseal",471,172,218,36,()=>Open("https://github.com/Flowseal/tg-ws-proxy/blob/main/docs/README.windows.md"));
  L(card,"Подключение откроется в Telegram. Ссылка содержит секрет — не публикуй её.",23,210,700,18,8).ForeColor=Theme.Muted;
  RefreshTelegramStatus(TgProxy.State());
 }
}
}
