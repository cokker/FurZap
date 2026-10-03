using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Xml;

namespace FurZap {
public static class FurZapAutostart {
 const string TaskName="FurZap Cokker (tray)";
 static string TaskXml(string exe,string sid){
  string path=SecurityElement.Escape(Path.GetFullPath(exe));
  string folder=SecurityElement.Escape(Path.GetDirectoryName(Path.GetFullPath(exe)));
  sid=SecurityElement.Escape(sid);
  return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n"+
   "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">"+
   "<RegistrationInfo><Description>FurZap в трее при входе в Windows</Description></RegistrationInfo>"+
   "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>"+sid+"</UserId><Delay>PT15S</Delay></LogonTrigger></Triggers>"+
   "<Principals><Principal id=\"Author\"><UserId>"+sid+"</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>"+
   "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><ExecutionTimeLimit>PT0S</ExecutionTimeLimit></Settings>"+
   "<Actions Context=\"Author\"><Exec><Command>"+path+"</Command><Arguments>--tray</Arguments><WorkingDirectory>"+folder+"</WorkingDirectory></Exec></Actions></Task>";
 }
 static string Schtasks(string arguments,bool absentOk){
  using(var process=Process.Start(new ProcessStartInfo("schtasks.exe",arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){
   if(process==null)throw new Exception("Не удалось запустить планировщик заданий Windows.");
   if(!process.WaitForExit(10000)){process.Kill();throw new Exception("Планировщик заданий не ответил за 10 секунд.");}
   string output=process.StandardOutput.ReadToEnd(),error=process.StandardError.ReadToEnd();
   if(process.ExitCode!=0){if(absentOk)return null;throw new Exception("Планировщик заданий не изменил автозапуск FurZap (код "+process.ExitCode+"). "+error.Trim());}
   return output;
  }
 }
 static bool Enabled(string exe,string name){
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return false;
  string xml=Schtasks("/Query /TN "+Backend.Quote(name)+" /XML",true);if(String.IsNullOrWhiteSpace(xml))return false;
  var document=new XmlDocument();document.LoadXml(xml);var ns=new XmlNamespaceManager(document.NameTable);ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");
  var command=document.SelectSingleNode("//t:Actions/t:Exec/t:Command",ns);var arguments=document.SelectSingleNode("//t:Actions/t:Exec/t:Arguments",ns);
  return command!=null&&arguments!=null&&String.Equals(Path.GetFullPath(command.InnerText),Path.GetFullPath(exe),StringComparison.OrdinalIgnoreCase)&&arguments.InnerText=="--tray";
 }
 public static bool Enabled(string exe){return Enabled(exe,TaskName);}
 static void Set(string exe,bool enabled,string name){
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new Exception("Автозапуск доступен только в Windows.");
  if(!enabled){Schtasks("/Delete /TN "+Backend.Quote(name)+" /F",true);return;}
  string temp=Path.Combine(Path.GetTempPath(),"FurZap-task-"+Guid.NewGuid().ToString("N")+".xml");
  try{File.WriteAllText(temp,TaskXml(exe,WindowsIdentity.GetCurrent().User.Value),System.Text.Encoding.Unicode);
   Schtasks("/Create /TN "+Backend.Quote(name)+" /XML "+Backend.Quote(temp)+" /F",false);
   if(!Enabled(exe,name))throw new Exception("Задание создано, но проверка пути FurZap не прошла.");
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static void Set(string exe,bool enabled){Set(exe,enabled,TaskName);}
 public static void VerifyRegistration(string exe){
  string name=TaskName+" test "+Guid.NewGuid().ToString("N");
  try{Set(exe,true,name);if(!Enabled(exe,name))throw new Exception("Задание автозапуска отсутствует после регистрации.");}
  finally{Set(exe,false,name);}
 }
 internal static string XmlForTest(string exe,string sid){return TaskXml(exe,sid);}
}
}
