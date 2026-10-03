using System;
using System.IO;
using Microsoft.Win32;
namespace FurZap {
public static class TelegramAutostart {
 const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 const string ValueName="FurZap TG WS Proxy";
 const string NativeName="TgWsProxy";
 public static string Command(string executable){return Backend.Quote(Path.GetFullPath(executable));}
 public static bool Enabled(TelegramProxyManager proxy){
  using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return key!=null&&(!String.IsNullOrWhiteSpace(Convert.ToString(key.GetValue(NativeName,"")))||!String.IsNullOrWhiteSpace(Convert.ToString(key.GetValue(ValueName,""))));
 }
 public static void Reconcile(TelegramProxyManager proxy,Backend backend){
  if(backend.Preview)return;
  using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true)){
   if(key==null)return;
   string legacy=Convert.ToString(key.GetValue(ValueName,""));if(String.IsNullOrWhiteSpace(legacy))return;
   if(String.IsNullOrWhiteSpace(Convert.ToString(key.GetValue(NativeName,""))))key.SetValue(NativeName,legacy,RegistryValueKind.String);
   key.DeleteValue(ValueName,false);
  }
 }
 public static void Set(TelegramProxyManager proxy,Backend backend,bool enabled){
  if(backend.Preview)throw new Exception("Автозапуск доступен только в Windows.");
  string command=enabled?Command(proxy.Executable??TelegramProxyUpdater.ExtractEmbedded(backend.Data)):null;
  using(var key=Registry.CurrentUser.CreateSubKey(RunKey)){
   if(key==null)throw new Exception("Не удалось открыть параметры автозапуска Windows.");
   if(enabled)key.SetValue(NativeName,command,RegistryValueKind.String);else key.DeleteValue(NativeName,false);
   key.DeleteValue(ValueName,false);
  }
 }
}
}
