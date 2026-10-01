using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using Microsoft.Win32;
namespace FurZap {
public partial class Backend {
 public string ConflictReport(){
  var report=new StringBuilder("ПРОВЕРКА КОНФЛИКТОВ\r\nНичего автоматически не отключается. Наличие VPN/адаптера не доказывает конфликт.\r\n");
  foreach(string file in new[]{"bin/winws.exe","bin/WinDivert.dll","bin/WinDivert64.sys","bin/cygwin1.dll"})if(!File.Exists(P(file)))report.AppendLine("ОШИБКА: отсутствует "+file+". Распакуй полный ZIP или восстанови движок.");
  if(Preview){report.AppendLine("Предпросмотр: системные процессы и службы не проверяются.");return report.ToString();}
  try{CheckOwnership();report.AppendLine("Служба: "+(ServiceState()==0?"не установлена":"принадлежит этой папке, состояние "+ServiceState()));}catch(Exception ex){report.AppendLine("КОНФЛИКТ: "+ex.Message+" Открой раздел «Служба».");}
  foreach(string name in new[]{"winws","winws2","goodbyedpi","Adguard","openvpn","wireguard","radminvpn"})foreach(var process in Process.GetProcessesByName(name))using(process){
   bool owned=Child!=null&&process.Id==Child.Id;string path="путь недоступен";try{path=process.MainModule.FileName;}catch{}
   report.AppendLine((owned?"СВОЙ ПРОЦЕСС: ":"ПРОВЕРЬ: ")+name+" PID="+process.Id+" · "+path);
   if(!owned&&(name=="winws"||name=="winws2"))report.AppendLine("Если это не процесс твоей службы, закрой его через прежний Zapret перед запуском FurZap.");
  }
  try{foreach(var service in ServiceController.GetServices())using(service){string n=service.ServiceName.ToLowerInvariant();if(n.Contains("zapret")||n.Contains("windivert")||n.Contains("goodbyedpi")||n.Contains("vpn"))report.AppendLine("СЛУЖБА: "+service.ServiceName+" · "+service.Status);}}catch(Exception ex){report.AppendLine("Службы не удалось прочитать: "+ex.Message);}
  try{foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces()){string text=adapter.Name+" "+adapter.Description;string lower=text.ToLowerInvariant();if(lower.Contains("vpn")||lower.Contains("tap")||lower.Contains("tun")||lower.Contains("radmin")||lower.Contains("hyper-v")||lower.Contains("virtual"))report.AppendLine("АДАПТЕР: "+text+" · "+adapter.OperationalStatus+". При диагностике сравни подключение с ним и без него вручную.");}}catch(Exception ex){report.AppendLine("Адаптеры не удалось прочитать: "+ex.Message);}
  using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))if(key!=null&&Convert.ToInt32(key.GetValue("ProxyEnable",0))!=0)report.AppendLine("ПРОКСИ включён: "+key.GetValue("ProxyServer","")+". Проверь его доступность в параметрах Windows.");
  report.AppendLine("Если запуск завершается сразу: открой «Журнал». Если процесс работает, но сайт недоступен: выполни «Проверки», затем попробуй другую стратегию. Голос и игровые подключения проверяются отдельно.");return report.ToString();
 }
}
}
