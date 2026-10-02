using System;
using System.IO;
using System.Drawing;
namespace FurZap {
public partial class MainForm {
 void TelegramProxyCard(){
  var card=Box(775,231);
  Title(card,"Telegram · TG WS Proxy","Локальный MTProto-прокси Flowseal. Работает отдельно от winws; подключи его в Telegram Desktop.");
  string status=B.Preview?"Предпросмотр":TgProxy.Running?"Работает":TgProxy.Executable==null?"Не установлен":"Готов к запуску";
  L(card,"Состояние: "+status,23,82,690,25,10,true).ForeColor=Theme.Mint;
  Btn(card,"Скачать / обновить",23,120,219,38,()=>Work(()=>TgProxy.InstallLatest(System.Threading.CancellationToken.None,p=>{if(p%20==0)Log("TG WS Proxy: "+p+"%");}),()=>ReloadPage("Инструменты")));
  Btn(card,"Запустить",254,120,172,38,()=>Work(()=>{if(TgProxy.Executable==null)TgProxy.InstallLatest(System.Threading.CancellationToken.None,p=>{});TgProxy.Start();},()=>ReloadPage("Инструменты")),true);
  Btn(card,"Остановить",438,120,172,38,()=>Work(()=>TgProxy.Stop(),()=>ReloadPage("Инструменты")));
  Btn(card,"Папка прокси",23,172,219,36,()=>{string path=TgProxy.Executable;if(path!=null)Open(Path.GetDirectoryName(path));else Feedback("Прокси не установлен","Сначала скачай официальный EXE.");});
  Btn(card,"Инструкция Flowseal",254,172,250,36,()=>Open("https://github.com/Flowseal/tg-ws-proxy#настройка-telegram-desktop"));
  L(card,"При первом запуске прокси покажет ссылку для Telegram. Затем управление доступно через его значок в трее.",23,210,700,18,8).ForeColor=Theme.Muted;
 }
}
}
