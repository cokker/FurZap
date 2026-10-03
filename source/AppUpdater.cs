using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
namespace FurZap {
public sealed class AppRelease {public Version Version;public string Notes,Url,Digest;public long Size;}
// Bounded JSON reader: no dynamic code or deserialization of CLR types.
public sealed class ReleaseJson {
 readonly string text;int pos;ReleaseJson(string s){text=s;}
 void Space(){while(pos<text.Length&&Char.IsWhiteSpace(text[pos]))pos++;}
 char Take(){if(pos>=text.Length)throw new FormatException("Неполный JSON.");return text[pos++];}
 void Expect(char c){Space();if(Take()!=c)throw new FormatException("Неверный JSON.");}
 string String(){Expect('"');var b=new StringBuilder();while(true){char c=Take();if(c=='"')return b.ToString();if(c<32)throw new FormatException();if(c=='\\'){c=Take();switch(c){case '"':case '\\':case '/':break;case 'b':c='\b';break;case 'f':c='\f';break;case 'n':c='\n';break;case 'r':c='\r';break;case 't':c='\t';break;case 'u':if(pos+4>text.Length)throw new FormatException();c=(char)UInt16.Parse(text.Substring(pos,4),NumberStyles.HexNumber);pos+=4;break;default:throw new FormatException();}}b.Append(c);}}
 object Value(int depth){if(depth>32)throw new FormatException();Space();if(pos>=text.Length)throw new FormatException();char c=text[pos];if(c=='"')return String();if(c=='{'){pos++;var d=new Dictionary<string,object>();Space();if(text[pos]=='}'){pos++;return d;}while(true){string k=String();Expect(':');d.Add(k,Value(depth+1));Space();c=Take();if(c=='}')return d;if(c!=',')throw new FormatException();}}if(c=='['){pos++;var a=new List<object>();Space();if(text[pos]==']'){pos++;return a;}while(true){a.Add(Value(depth+1));Space();c=Take();if(c==']')return a;if(c!=',')throw new FormatException();}}foreach(string word in new[]{"true","false","null"})if(text.Substring(pos).StartsWith(word,StringComparison.Ordinal)){pos+=word.Length;return word=="null"?null:(object)(word=="true");}int start=pos;while(pos<text.Length&&"-+0123456789.eE".IndexOf(text[pos])>=0)pos++;if(pos==start)throw new FormatException();return Double.Parse(text.Substring(start,pos-start),CultureInfo.InvariantCulture);}
 public static Dictionary<string,object> Parse(string s){if(s.Length>2000000)throw new FormatException("Слишком большой ответ.");var p=new ReleaseJson(s);var value=p.Value(0);p.Space();if(p.pos!=s.Length)throw new FormatException();return (Dictionary<string,object>)value;}
}
public static class AppUpdater {
 const string Api="https://api.github.com/repos/cokker/FurZap/releases/latest";
 public static string LatestChanges(string notes){
  var lines=new List<string>();int length=0;
  foreach(string raw in (notes??"").Replace("\r","").Split('\n')){
   string line=raw.Trim();
   if(System.Text.RegularExpressions.Regex.IsMatch(line,@"^#{1,4}\s*(Ранее|Предыдущ|История версий|Previous|Older)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase))break;
   if(line.StartsWith("#"))continue;
   if(line.Length==0){if(lines.Count>0&&lines[lines.Count-1]!="")lines.Add("");continue;}
   if(line.StartsWith("- "))line="• "+line.Substring(2);
   line=line.Replace("`","");
   if(length+line.Length>1800||lines.Count(x=>x.StartsWith("• "))>=8)break;
   lines.Add(line);length+=line.Length;
  }
  string result=String.Join("\r\n",lines).Trim();
  return result.Length==0?"Описание последнего выпуска не опубликовано.":result;
 }
 public static AppRelease ParseRelease(string json){var root=ReleaseJson.Parse(json);if((bool)root["draft"]||(bool)root["prerelease"])throw new Exception("Ожидается стабильный опубликованный релиз.");var version=new Version(((string)root["tag_name"]).TrimStart('v'));if(version.Build<0)throw new Exception("Неверная версия.");var asset=((List<object>)root["assets"]).Cast<Dictionary<string,object>>().SingleOrDefault(x=>(string)x["name"]=="FurZap.exe");if(asset==null)throw new Exception("В этом релизе нет файла автообновления. Скачай ZIP из GitHub Releases.");string url=(string)asset["browser_download_url"],digest=asset.ContainsKey("digest")?asset["digest"] as string:null;Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/cokker/FurZap/releases/download/",StringComparison.Ordinal)||!uri.AbsolutePath.EndsWith("/FurZap.exe",StringComparison.Ordinal)||!String.IsNullOrEmpty(uri.Query))throw new Exception("Неожиданный адрес файла.");if(digest==null||!System.Text.RegularExpressions.Regex.IsMatch(digest,@"^sha256:[0-9a-fA-F]{64}$"))throw new Exception("GitHub не предоставил SHA-256; установка отменена.");long size=Convert.ToInt64(asset["size"]);if(size<1024||size>100*1024*1024)throw new Exception("Неожиданный размер обновления.");return new AppRelease{Version=new Version(version.Major,version.Minor,version.Build,Math.Max(0,version.Revision)),Notes=root["body"] as string??"",Url=url,Digest=digest.Substring(7),Size=size};}
 static HttpWebRequest Request(string url){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var r=(HttpWebRequest)WebRequest.Create(url);r.UserAgent="FurZap-Updater/1.3";r.Accept="application/vnd.github+json";r.Timeout=20000;r.ReadWriteTimeout=20000;return r;}
 public static AppRelease Latest(){using(var response=Request(Api).GetResponse())using(var stream=response.GetResponseStream())using(var reader=new StreamReader(stream)){var b=new StringBuilder();char[] chunk=new char[4096];int n;while((n=reader.Read(chunk,0,chunk.Length))>0){b.Append(chunk,0,n);if(b.Length>2000000)throw new Exception("Слишком большой ответ GitHub.");}return ParseRelease(b.ToString());}}
 public static string FileHash(string path){using(var sha=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
 public static void ValidateFile(string file,AppRelease release){if(new FileInfo(file).Length!=release.Size||!String.Equals(FileHash(file),release.Digest,StringComparison.OrdinalIgnoreCase))throw new Exception("Контрольная сумма обновления не совпала. Установка отменена.");var assembly=AssemblyName.GetAssemblyName(file);if(assembly.Name!="FurZap"||assembly.Version!=release.Version)throw new Exception("Версия файла не совпадает с релизом.");}
 public static string Download(AppRelease release,string data,CancellationToken token,Action<int> progress){string dir=Path.Combine(Path.GetFullPath(data),"updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string file=Path.Combine(dir,"FurZap.exe");try{var request=Request(release.Url);request.Accept="application/octet-stream";using(token.Register(()=>request.Abort()))using(var response=request.GetResponse())using(var input=response.GetResponseStream())using(var output=File.Create(file)){byte[] bytes=new byte[65536];int n,last=-1;long total=0;while((n=input.Read(bytes,0,bytes.Length))>0){token.ThrowIfCancellationRequested();total+=n;if(total>release.Size)throw new Exception("Размер загрузки превышен.");output.Write(bytes,0,n);int percent=(int)(total*100/release.Size);if(percent!=last){last=percent;progress(percent);}}}token.ThrowIfCancellationRequested();ValidateFile(file,release);return file;}catch{Directory.Delete(dir,true);token.ThrowIfCancellationRequested();throw;}}
 public static void LaunchInstaller(string staged){string helper=Path.Combine(Path.GetDirectoryName(staged),"FurZap.Update.exe");File.Copy(Application.ExecutablePath,helper,true);string digest=FileHash(staged);Process.Start(new ProcessStartInfo(helper,"--install-update "+Backend.Quote(Application.ExecutablePath)+" "+Backend.Quote(staged)+" "+Process.GetCurrentProcess().Id+" "+digest){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(staged)});}
 public static int Install(string[] args){try{if(args.Length!=5||args[0]!="--install-update"||Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new Exception("Неверный вызов обновления.");string target=Path.GetFullPath(args[1]),staged=Path.GetFullPath(args[2]);if(Path.GetFileName(target)!="FurZap.exe"||Path.GetFileName(staged)!="FurZap.exe"||target==staged||FileHash(staged)!=args[4]||AssemblyName.GetAssemblyName(staged).Name!="FurZap")throw new Exception("Проверка обновления не пройдена.");try{var parent=Process.GetProcessById(Int32.Parse(args[3]));if(!parent.WaitForExit(60000))throw new Exception("Закрой FurZap и повтори обновление.");}catch(ArgumentException){}string next=target+".new";File.Copy(staged,next,true);if(FileHash(next)!=args[4])throw new Exception("Файл изменился во время копирования.");string backup=target+"."+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".bak";File.Replace(next,target,backup);try{string data=Path.Combine(Path.GetDirectoryName(target),"data");Directory.CreateDirectory(data);File.AppendAllText(Path.Combine(data,"updates-history.txt"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" | FurZap | "+AssemblyName.GetAssemblyName(backup).Version+" → "+AssemblyName.GetAssemblyName(target).Version+" | Установлено\r\n",Encoding.UTF8);}catch{}try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(target)});}catch{File.Copy(backup,target,true);throw;}return 0;}catch(Exception ex){MessageBox.Show("Не удалось установить обновление.\n"+ex.Message,"FurZap");return 1;}}
}
}
