using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace FurZap {
public static class SettingsArchive {
 const string Format="FURZAP-SETTINGS-1";
 static readonly string[] Meta={"manifest.txt","strategy.txt","description.txt"};
 static readonly string[] SnapshotFiles=Meta.Concat(Backend.ConfigFiles).ToArray();
 static void Add(ZipArchive zip,string name,string path){
  if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new Exception("Ссылка на файл не включается в резервную копию: "+name);
  if(new FileInfo(path).Length>16000000)throw new Exception("Слишком большой файл настроек: "+name);
  var entry=zip.CreateEntry(name,CompressionLevel.Optimal);using(var input=File.OpenRead(path))using(var output=entry.Open())input.CopyTo(output);
 }
 static void AddSnapshot(Backend b,ZipArchive zip,string dir,string prefix){
  string strategy;b.ReadSnapshot(dir,out strategy);
  foreach(string rel in SnapshotFiles){string file=Path.Combine(dir,rel.Replace('/',Path.DirectorySeparatorChar));if(File.Exists(file))Add(zip,prefix+rel,file);}
 }
 public static void Export(Backend b,string destination){
  string staging=Path.Combine(b.Data,"export-"+Guid.NewGuid().ToString("N")),temp=destination+"."+Guid.NewGuid().ToString("N")+".new";
  try{
   b.Capture(staging,"Экспорт FurZap");
   using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create)){
    var format=zip.CreateEntry("format.txt");using(var w=new StreamWriter(format.Open(),new UTF8Encoding(false)))w.Write(Format);
    AddSnapshot(b,zip,staging,"snapshot/");
    var prefs=zip.CreateEntry("preferences.txt");using(var w=new StreamWriter(prefs.Open(),new UTF8Encoding(false)))foreach(var p in b.Pref)w.WriteLine(p.Key+"="+p.Value);
    string[] profiles=b.Profiles();if(profiles.Length>50)throw new Exception("Можно экспортировать не более 50 профилей.");
    foreach(string name in profiles){if(Backend.ProfileName(name)!=name)throw new Exception("Некорректное имя профиля: "+name);AddSnapshot(b,zip,Path.Combine(b.Data,"profiles",name),"profiles/"+name+"/");}
   }
   if(new FileInfo(temp).Length>64000000)throw new Exception("Архив настроек слишком большой.");
   if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);
  }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);if(File.Exists(temp))File.Delete(temp);}
 }
 static bool Allowed(string name){
  if(name=="format.txt"||name=="preferences.txt")return true;
  if(name.StartsWith("snapshot/",StringComparison.Ordinal))return SnapshotFiles.Contains(name.Substring(9));
  if(name.StartsWith("profiles/",StringComparison.Ordinal)){
   string[] parts=name.Split(new[]{'/'},3);if(parts.Length!=3||Backend.ProfileName(parts[1])!=parts[1])return false;
   return SnapshotFiles.Contains(parts[2]);
  }
  return false;
 }
 static Dictionary<string,string> Preferences(string file){
  var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  foreach(string line in File.ReadAllLines(file,Encoding.UTF8)){
   int separator=line.IndexOf('=');if(separator<1||separator>100||line.Length>20000||!Regex.IsMatch(line.Substring(0,separator),@"^[a-zA-Z0-9_.-]+$"))throw new Exception("Некорректные настройки в архиве.");
   string key=line.Substring(0,separator);if(result.ContainsKey(key))throw new Exception("Повторяющийся параметр в архиве: "+key);result.Add(key,line.Substring(separator+1));
  }
  return result;
 }
 static void Extract(string archive,string staging){
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
  using(var zip=ZipFile.OpenRead(archive)){
   if(zip.Entries.Count>1000)throw new Exception("В архиве слишком много файлов.");
   foreach(var entry in zip.Entries){string name=entry.FullName;
    if(name.Contains('\\')||!Allowed(name)||!names.Add(name)||entry.Length>16000000||entry.Length<0||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new Exception("В архиве неизвестный или небезопасный файл: "+name);
    total+=entry.Length;if(total>64000000)throw new Exception("Архив настроек слишком большой.");
    string target=Path.Combine(staging,name.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(target));
    using(var input=entry.Open())using(var output=File.Create(target)){byte[] buffer=new byte[32768];long count=0;int n;while((n=input.Read(buffer,0,buffer.Length))>0){count+=n;if(count>entry.Length)throw new Exception("Повреждён файл архива: "+name);output.Write(buffer,0,n);}if(count!=entry.Length)throw new Exception("Неполный файл архива: "+name);}
   }
  }
  string formatPath=Path.Combine(staging,"format.txt");if(!File.Exists(formatPath)||File.ReadAllText(formatPath,Encoding.UTF8)!=Format||!File.Exists(Path.Combine(staging,"preferences.txt")))throw new Exception("Это не архив настроек FurZap.");
 }
 public static void Import(Backend b,string archive){
  string staging=Path.Combine(b.Data,"import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
  try{
   Extract(archive,staging);string snapshot=Path.Combine(staging,"snapshot"),strategy;
   var files=b.ReadSnapshot(snapshot,out strategy);
   var prefs=Preferences(Path.Combine(staging,"preferences.txt"));prefs["strategy"]=strategy;
   foreach(var rel in Backend.ConfigFiles.Where(x=>x.StartsWith("lists/"))){byte[] bytes=files[rel];if(bytes!=null)Backend.ValidateList(Path.GetFileName(rel),Encoding.UTF8.GetString(bytes));}
   string profiles=Path.Combine(staging,"profiles");string[] names=Directory.Exists(profiles)?Directory.GetDirectories(profiles).Select(Path.GetFileName).ToArray():new string[0];
   if(names.Length>50)throw new Exception("В архиве слишком много профилей.");
   foreach(string name in names){string selected;b.ReadSnapshot(Path.Combine(profiles,name),out selected);}
   string backup=Path.Combine(staging,"previous-profiles");Directory.CreateDirectory(backup);
   foreach(string name in names){string target=Path.Combine(b.Data,"profiles",name);if(Directory.Exists(target))EngineUpdater.CopyTree(target,Path.Combine(backup,name));}
   try{
    b.ApplyChange("Импорт настроек",()=>{
     foreach(string name in names){string target=Path.Combine(b.Data,"profiles",name);if(Directory.Exists(target))Directory.Delete(target,true);EngineUpdater.CopyTree(Path.Combine(profiles,name),target);}
     b.Pref.Clear();foreach(var pair in prefs)b.Pref[pair.Key]=pair.Value;
     b.RestoreFiles(snapshot);
    },strategy);
   }catch(Exception original){
    try{foreach(string name in names){string target=Path.Combine(b.Data,"profiles",name),old=Path.Combine(backup,name);if(Directory.Exists(target))Directory.Delete(target,true);if(Directory.Exists(old))EngineUpdater.CopyTree(old,target);}}
    catch(Exception recovery){throw new Exception("Импорт не применён: "+original.Message+"\nНе удалось восстановить профили: "+recovery.Message,original);}
    throw;
   }
  }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
 }
}
}
