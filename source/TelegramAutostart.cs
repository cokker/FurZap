using System;
using System.IO;
using Microsoft.Win32;
namespace FurZap {
public static class TelegramAutostart {
 const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 const string ValueName="FurZap TG WS Proxy";
 public static string Command(string executable){return Backend.Quote(Path.GetFullPath(executable));}
 public static bool Enabled(TelegramProxyManager proxy){
  string file=proxy.Executable;if(file==null)return false;
  using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return key!=null&&String.Equals(Convert.ToString(key.GetValue(ValueName,"")),Command(file),StringComparison.OrdinalIgnoreCase);
 }
 public static void Set(TelegramProxyManager proxy,Backend backend,bool enabled){
  if(backend.Preview)throw new Exception("Автозапуск доступен только в Windows.");
  string command=enabled?Command(proxy.Executable??TelegramProxyUpdater.ExtractEmbedded(backend.Data)):null;
  using(var key=Registry.CurrentUser.CreateSubKey(RunKey)){
   if(key==null)throw new Exception("Не удалось открыть параметры автозапуска Windows.");
   if(enabled)key.SetValue(ValueName,command,RegistryValueKind.String);else key.DeleteValue(ValueName,false);
  }
 }
}
}
