using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

// Read-only portable checker. It never starts a game, installs a mod or edits Windows.
public static class CastleCraftDoctor {
 public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength=16777216 };
 public static string Root { get { return AppDomain.CurrentDomain.BaseDirectory; } }
 public static Dictionary<string,object> Manifest { get { return ReadJson(Path.Combine(Root,"package-manifest.json")); } }
 public static Dictionary<string,object> ReadJson(string file) { return Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(file)); }
 public static string Str(Dictionary<string,object> data,string key) { object value;return data.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):""; }
 public static string Hash(string file) { using(var s=File.OpenRead(file))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant(); }
 public static IEnumerable<string> Dirs(string root) { try { return Directory.GetDirectories(root); } catch { return new string[0]; } }
 public static string VersionFile(string folder) {
  string preferred=Path.Combine(folder,new DirectoryInfo(folder).Name+".json");
  if(File.Exists(preferred))return preferred;
  try { foreach(var file in Directory.GetFiles(folder,"*.json")) { var d=ReadJson(file);if(d.ContainsKey("mainClass")||d.ContainsKey("inheritsFrom"))return file; } }catch{}
  return null;
 }
 public static List<Dictionary<string,object>> Chain(string folder) {
  var list=new List<Dictionary<string,object>>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  string versions=Directory.GetParent(folder).FullName;
  for(int i=0;i<12;i++) {
   if(!seen.Add(Path.GetFullPath(folder)))throw new InvalidDataException("版本继承存在循环");
   string f=VersionFile(folder);if(f==null)break;
   var d=ReadJson(f);list.Add(d);string parent=Str(d,"inheritsFrom");if(parent=="")break;
   if(parent=="26.3"&&!Directory.Exists(Path.Combine(versions,parent))) { list.Add(new Dictionary<string,object>{{"id","26.3"}});break; }
   if(parent.IndexOfAny(new char[]{'\\','/'})>=0||parent=="..")throw new InvalidDataException("无效的版本继承路径");
   folder=Path.Combine(versions,parent);
  }
  return list;
 }
 public static string McVersion(List<Dictionary<string,object>> chain) {
  foreach(var d in chain) { string v=Str(d,"clientVersion");if(v!="")return v; }
  for(int i=chain.Count-1;i>=0;i--) {string id=Str(chain[i],"id");if(Regex.IsMatch(id,@"^\d+\.\d+(?:\.\d+)?$"))return id;}
  return "未知";
 }
 public static string Loader(List<Dictionary<string,object>> chain) {
  foreach(var d in chain) {
   object libs;if(!d.TryGetValue("libraries",out libs))continue;
   foreach(object obj in (IEnumerable)libs) {var lib=obj as Dictionary<string,object>;if(lib==null)continue;string n=Str(lib,"name");if(n.StartsWith("net.fabricmc:fabric-loader:"))return n.Substring("net.fabricmc:fabric-loader:".Length);}
  }
  return "";
 }
 public static string DefaultGameDir(string version) {
  if(Directory.Exists(Path.Combine(version,"mods"))||File.Exists(Path.Combine(version,"options.txt")))return version;
  return Directory.GetParent(Directory.GetParent(version).FullName).FullName;
 }
 public static List<string> MinecraftCandidates() {
  var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".minecraft"));
  roots.Add(Path.Combine(Root,".minecraft"));roots.Add(Path.Combine(Directory.GetParent(Root.TrimEnd('\\')).FullName,".minecraft"));
  foreach(var drive in DriveInfo.GetDrives()) {
   if(!drive.IsReady||drive.DriveType!=DriveType.Fixed)continue;
   roots.Add(Path.Combine(drive.RootDirectory.FullName,".minecraft"));
   foreach(string parent in Dirs(drive.RootDirectory.FullName).Take(200)) {
    roots.Add(Path.Combine(parent,".minecraft"));
    if(new DirectoryInfo(parent).Name.Equals("Minecraft",StringComparison.OrdinalIgnoreCase))roots.Add(parent);
   }
  }
  var results=new List<string>();
  foreach(string root in roots) foreach(string version in Dirs(Path.Combine(root,"versions"))) {
   try {var c=Chain(version);if(c.Count>0&&McVersion(c)=="26.3"&&Loader(c)!="")results.Add(version);}catch{}
  }
  return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 }
 public static List<string> CastleCandidates() {
  var steam=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var hive in new RegistryKey[]{Registry.CurrentUser,Registry.LocalMachine})foreach(var k in new string[]{@"Software\Valve\Steam",@"Software\WOW6432Node\Valve\Steam"}) {
   using(var key=hive.OpenSubKey(k))if(key!=null) {var p=Convert.ToString(key.GetValue("SteamPath")??key.GetValue("InstallPath"));if(p!="")steam.Add(p);}
  }
  foreach(var drive in DriveInfo.GetDrives())if(drive.IsReady&&drive.DriveType==DriveType.Fixed) foreach(string sub in new string[]{"Steam",@"Games\Steam",@"Program Files (x86)\Steam"})steam.Add(Path.Combine(drive.RootDirectory.FullName,sub));
  foreach(string s in steam.ToArray()) {
   string file=Path.Combine(s,@"steamapps\libraryfolders.vdf");if(!File.Exists(file))continue;
   try {foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"([^\"]+)\""))steam.Add(m.Groups[1].Value.Replace("\\\\","\\"));}catch{}
  }
  var result=steam.Select(s=>Path.Combine(s,@"steamapps\common\Lost Castle")).ToList();
  result.Add(Directory.GetParent(Root.TrimEnd('\\')).FullName);
  return result.Where(p=>File.Exists(Path.Combine(p,"Lost_Castle.exe"))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 }
 public sealed class Report {
  public bool ready;public string castle,minecraft,gameDir,build;public bool vulkanPreferred;public string vulkanRuntime="未确认";
  public List<string> passed=new List<string>();public List<string> errors=new List<string>();public List<string> notes=new List<string>();
  public override string ToString() {return (ready?"安装识别成功：请用户自行启动两个游戏。":"识别未通过：请按教程补齐以下项目。")+"\r\n\r\n"+string.Join("\r\n",passed.Select(x=>"[通过] "+x).Concat(errors.Select(x=>"[待处理] "+x)).Concat(notes.Select(x=>"[提示] "+x)));}
 }
 public static Report Check(string castle,string minecraft,string gameDir,string javaPath) {
  var r=new Report {castle=castle,minecraft=minecraft,gameDir=gameDir};
  try {
   var manifest=Manifest;r.build=Str(manifest,"build");
   Require(r,File.Exists(Path.Combine(castle,"Lost_Castle.exe")),"Lost Castle 游戏目录","请选择包含 Lost_Castle.exe 的目录");
   string unity=Path.Combine(castle,"UnityPlayer.dll");
   Require(r,File.Exists(unity)&&FileVersionInfo.GetVersionInfo(unity).ProductVersion.Contains(Str(manifest,"unity_version")),"Unity "+Str(manifest,"unity_version"),"Unity 版本与此包已核验基准不符");
   MatchFile(r,Path.Combine(castle,@"Lost_Castle_Data\Managed\Assembly-CSharp.dll"),Str(manifest,"game_assembly_sha256"),"Lost Castle 游戏脚本版本");
   Require(r,File.Exists(Path.Combine(castle,@"BepInEx\core\BepInEx.Preloader.dll")),"BepInEx 入口存在","安装 Lost Castle_首次安装包，或官方 BepInEx 5.4.23.5 Windows x64");
   string door=Path.Combine(castle,"doorstop_config.ini");
   Require(r,File.Exists(Path.Combine(castle,"winhttp.dll"))&&File.Exists(door)&&Regex.IsMatch(File.ReadAllText(door),@"(?m)^\s*enabled\s*=\s*true\s*$",RegexOptions.IgnoreCase)&&Regex.IsMatch(File.ReadAllText(door),@"target_assembly\s*=\s*BepInEx[\\/]core[\\/]BepInEx\.Preloader\.dll",RegexOptions.IgnoreCase),"Doorstop 加载入口启用","检查 winhttp.dll 和 doorstop_config.ini；.disabled/.melty-off 不会加载");
   MatchFile(r,Path.Combine(castle,@"BepInEx\plugins\CastleCraft\CastleCraft.Passthrough.dll"),Str(manifest,"host_sha256"),"城堡桥接 DLL");
   MatchFile(r,Path.Combine(castle,@"BepInEx\plugins\CastleCraft\CastleCraft.GpuBridge.dll"),Str(manifest,"gpu_sha256"),"原生 GPU 桥接 DLL");
   var chain=Chain(minecraft);
   Require(r,McVersion(chain)=="26.3","Minecraft Java 26.3","Minecraft 版本不是 26.3（不能只看文件夹名称）");
   string loader=Loader(chain);Version parsed;
   Require(r,Version.TryParse(loader,out parsed)&&parsed>=new Version("0.19.5"),"Fabric Loader "+loader,"需要 Fabric Loader >=0.19.5；NeoForge/Forge 不适用");
   bool fabricMain=chain.Any(d=>Str(d,"mainClass").StartsWith("net.fabricmc.loader"));
   Require(r,fabricMain,"Fabric 客户端启动入口","版本 JSON 没有 Fabric 启动入口");
   MatchFile(r,Path.Combine(gameDir,@"mods\skycraft-0.1.2.jar"),Str(manifest,"guest_sha256"),"Minecraft 桥接 JAR");
   bool api=false;
   foreach(string f in Directory.Exists(Path.Combine(gameDir,"mods"))?Directory.GetFiles(Path.Combine(gameDir,"mods"),"fabric-api-*.jar"):new string[0]) {
    using(var zip=System.IO.Compression.ZipFile.OpenRead(f)) {
     var e=zip.GetEntry("fabric.mod.json");if(e==null)continue;
     using(var sr=new StreamReader(e.Open())) {var md=Json.Deserialize<Dictionary<string,object>>(sr.ReadToEnd());if(Str(md,"id")=="fabric-api" && Hash(f)==Str(manifest,"fabric_api_sha256"))api=true;}
    }
   }
   Require(r,api,"Fabric API 存在","将本包附带的 Fabric API 放进实际游戏目录的 mods");
   if(!string.IsNullOrWhiteSpace(javaPath)) {
    string home=Directory.GetParent(Path.GetDirectoryName(javaPath)).FullName;string release=Path.Combine(home,"release");
    string value=File.Exists(release)?File.ReadAllText(release):"";var v=Regex.Match(value,"JAVA_VERSION=\"(\\d+)");
    Require(r,File.Exists(javaPath)&&v.Success&&int.Parse(v.Groups[1].Value)>=25,"所选 Java 版本 >=25","选择启动器实际使用的 Java 25 javaw.exe；本程序不运行 Java");
   }else r.notes.Add("未指定 Java 路径：请在启动器确认实际使用 64 位 Java 25；版本声明不代表实际运行版本。");
   string options=Path.Combine(gameDir,"options.txt");r.vulkanPreferred=File.Exists(options)&&Regex.IsMatch(File.ReadAllText(options),"(?m)^preferredGraphicsBackend:\\s*\"?vulkan\"?\\s*$",RegexOptions.IgnoreCase);
   r.notes.Add(r.vulkanPreferred?"已选择 Vulkan 优先；实际启用仍以游戏 F3 为准。":"建议在 Minecraft 视频设置→图形 API 选择 Prefer Vulkan。default 自动模式也可能启用 Vulkan。");
   string log=Path.Combine(gameDir,@"logs\latest.log");
   if(File.Exists(log)&&File.ReadAllText(log).Contains("Vulkan GPU overlay ACTIVE"))r.vulkanRuntime="最近日志确认过 Vulkan GPU 共享 ACTIVE，仍需确认属于本次启动";
   r.notes.Add(r.vulkanRuntime+"；小程序不会启动游戏、安装模组或修改注册表/设置。");
   r.notes.Add("若显示成功：先自行启动 Lost Castle，再启动这个 Minecraft 实例；在城堡按 F8 接管。");
  }catch(Exception e){r.errors.Add(e.Message);}
  r.ready=r.errors.Count==0;return r;
 }
 static void Require(Report r,bool ok,string passed,string error){if(ok)r.passed.Add(passed);else r.errors.Add(error);}
 static void MatchFile(Report r,string file,string expected,string label){Require(r,File.Exists(file)&&Hash(file)==expected,label+"校验一致",label+"缺失或版本不同，请按本包教程检查");}
 [STAThread] public static int Main(string[] args) {
  if(args.Contains("--check")) {
   Func<string,string> arg=name=>{int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:"";};
   var r=Check(arg("--castle"),arg("--minecraft"),arg("--game-dir"),arg("--java"));Console.WriteLine(Json.Serialize(r));return r.ready?0:2;
  }
  if(args.Contains("--discover")){Console.WriteLine(Json.Serialize(new {castle=CastleCandidates(),minecraft=MinecraftCandidates()}));return 0;}
  if(args.Contains("--vulkan")){Console.WriteLine(VulkanDoctor.Detect());return 0;}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new DoctorWindow());return 0;
 }
 public sealed class DoctorWindow:Form {
  ComboBox castle=new ComboBox(),mc=new ComboBox();TextBox game=new TextBox(),java=new TextBox();RichTextBox output=new RichTextBox();
  public DoctorWindow() {
   Text="CastleCraft 个人版 · 安装与版本识别（不会启动游戏）";ClientSize=new Size(900,630);MinimumSize=new Size(760,540);Font=new Font("Microsoft YaHei UI",10);
   var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=7,Padding=new Padding(18)};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));Controls.Add(layout);
   AddRow(layout,0,"Lost Castle 目录",castle,()=>Browse(castle));AddRow(layout,1,"Minecraft 版本目录",mc,()=>Browse(mc));AddRow(layout,2,"实际游戏目录",game,()=>Browse(game));
   AddRow(layout,3,"Java javaw.exe（可选）",java,()=>{using(var d=new OpenFileDialog {Filter="Java|javaw.exe;java.exe"})if(d.ShowDialog()==DialogResult.OK)java.Text=d.FileName;});
   mc.SelectedIndexChanged+=(s,e)=>{try{game.Text=DefaultGameDir(mc.Text);}catch{}};
   var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true};layout.Controls.Add(buttons,0,4);layout.SetColumnSpan(buttons,3);
   var discover=new Button {Text="自动识别",AutoSize=true};discover.Click+=(s,e)=>Discover();buttons.Controls.Add(discover);
   var check=new Button {Text="检查安装",AutoSize=true};check.Click+=(s,e)=>{var r=Check(castle.Text,mc.Text,game.Text,java.Text);output.Text=r.ToString();output.ForeColor=r.ready?Color.DarkGreen:Color.DarkRed;};buttons.Controls.Add(check);
   var docs=new Button {Text="打开完整教程",AutoSize=true};docs.Click+=(s,e)=>Process.Start(new ProcessStartInfo(Path.Combine(Root,@"教程\个人使用维基教程.html")) {UseShellExecute=true});buttons.Controls.Add(docs);
   var vk=new Button {Text="Vulkan / 驱动助手",AutoSize=true};vk.Click+=(s,e)=>new VulkanDoctor().ShowDialog(this);buttons.Controls.Add(vk);
   output.Dock=DockStyle.Fill;output.ReadOnly=true;output.BackColor=Color.White;layout.Controls.Add(output,0,5);layout.SetColumnSpan(output,3);layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var note=new Label {Text="只读识别 · 不联网下载 · 不自动启动/关闭游戏 · 不改注册表 · Vulkan 是否启用请看 F3",Dock=DockStyle.Fill,AutoSize=true};layout.Controls.Add(note,0,6);layout.SetColumnSpan(note,3);
   Shown+=(s,e)=>Discover();
  }
  void Discover(){castle.Items.Clear();mc.Items.Clear();foreach(var p in CastleCandidates())castle.Items.Add(p);foreach(var p in MinecraftCandidates())mc.Items.Add(p);if(castle.Items.Count>0)castle.SelectedIndex=0;if(mc.Items.Count>0)mc.SelectedIndex=0;output.Text="请选择要玩的 Minecraft 实例，再点击“检查安装”。\r\n\r\n自动发现不到时可手动选择。版本目录负责识别26.3/Fabric；实际游戏目录负责检查mods和options.txt。";}
  static void AddRow(TableLayoutPanel p,int row,string label,Control input,Action browse){p.Controls.Add(new Label {Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);input.Dock=DockStyle.Fill;p.Controls.Add(input,1,row);var b=new Button {Text="选择",Dock=DockStyle.Fill};b.Click+=(s,e)=>browse();p.Controls.Add(b,2,row);}
  static void Browse(Control target){using(var d=new FolderBrowserDialog {Description="选择实际目录；不会安装或启动游戏"})if(d.ShowDialog()==DialogResult.OK)target.Text=d.SelectedPath;}
 }
}
