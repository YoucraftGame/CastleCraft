using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace CastleCraft {
internal static class MeltyLauncher {
    static string Value(string[] args,string key,string fallback) {
        int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;
    }
    // Windows argv quoting; never assemble a shell command or read account credentials.
    internal static string Quote(string value) {
        var s=new StringBuilder("\"");int slash=0;
        foreach(char c in value){if(c=='\\'){slash++;continue;}if(c=='\"'){s.Append('\\',slash*2+1);s.Append(c);}else{s.Append('\\',slash);s.Append(c);}slash=0;}
        s.Append('\\',slash*2);s.Append('"');return s.ToString();
    }
    internal static string UpdateIni(string text,IDictionary<string,string> values) {
        var lines=new List<string>(text.Replace("\r","").Split('\n'));int begin=lines.FindIndex(l=>l.Trim()=="[General]");
        if(begin<0){lines.Insert(0,"[General]");begin=0;}int end=begin+1;
        while(end<lines.Count&&!lines[end].StartsWith("["))end++;
        foreach(var kv in values){int at=-1;for(int i=begin+1;i<end;i++){int eq=lines[i].IndexOf('=');if(eq>0&&lines[i].Substring(0,eq).Trim()==kv.Key){at=i;break;}}
            if(at>=0)lines[at]=kv.Key+"="+kv.Value;else{lines.Insert(end,kv.Key+"="+kv.Value);end++;}}
        return string.Join("\n",lines).TrimEnd()+"\n";
    }
    static void SetIni(string file,Dictionary<string,string> values){
        string before=File.Exists(file)?File.ReadAllText(file):"[General]\n";
        string after=UpdateIni(before,values);if(before!=after)File.WriteAllText(file,after,new UTF8Encoding(false));
    }
    static string Runtime {get{return AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);}}
    static string Prism {get{return Path.Combine(Runtime,"Prism");}}
    static string Instance {get{return Path.Combine(Prism,"instances","CastleCraft");}}
    static string Game {get{return Path.Combine(Instance,".minecraft");}}
    static string Ready {get{return Path.Combine(Runtime,"setup.ready");}}
    static void ExtractPinned(string archive,string destination,bool stripRoot){
        string fingerprint;using(var h=SHA256.Create())using(var f=File.OpenRead(archive))fingerprint=BitConverter.ToString(h.ComputeHash(f));
        string stamp=Path.Combine(destination,".castlecraft-payload-sha256");
        if(File.Exists(stamp)&&File.ReadAllText(stamp)==fingerprint)return;
        string root=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        Directory.CreateDirectory(destination);
        using(var zip=ZipFile.OpenRead(archive))foreach(var e in zip.Entries){
            string name=e.FullName.Replace('\\','/');if(stripRoot){int first=name.IndexOf('/');if(first<0)continue;name=name.Substring(first+1);}
            if(name.Length==0||name.EndsWith("/"))continue;
            string target=Path.GetFullPath(Path.Combine(destination,name));
            if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe package path; reinstall CastleCraft from Melty.");
            Directory.CreateDirectory(Path.GetDirectoryName(target));using(var input=e.Open())using(var output=File.Create(target))input.CopyTo(output);
        }
        File.WriteAllText(stamp,fingerprint);
    }
    static void CopyPayload(string source,string target){
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        bool same=false;if(File.Exists(target)){using(var h=SHA256.Create())using(var a=File.OpenRead(source))using(var b=File.OpenRead(target))same=h.ComputeHash(a).SequenceEqual(h.ComputeHash(b));}
        if(!same)File.Copy(source,target,true);
    }
    static void Unpack(){
        ExtractPinned(Path.Combine(Runtime,"Payload","PrismLauncher-Windows-MSVC-Portable-11.1.1.zip"),Prism,false);
        ExtractPinned(Path.Combine(Runtime,"Payload","OpenJDK25U-jre_x64_windows_hotspot_25.0.2_10.zip"),Path.Combine(Runtime,"Runtime","Java"),true);
        ExtractPinned(Path.Combine(Runtime,"Payload","FabricLibraries.zip"),Path.Combine(Prism,"libraries"),false);
        // Temurin ships these redistributables and their notices. A portable Prism must not
        // silently depend on a VC++ runtime already installed on the creator's PC.
        foreach(string dll in new[]{"msvcp140.dll","vcruntime140.dll","vcruntime140_1.dll"})
            CopyPayload(Path.Combine(Runtime,"Runtime","Java","bin",dll),Path.Combine(Prism,dll));
        CopyPayload(Path.Combine(Runtime,"Templates","mmc-pack.json"),Path.Combine(Instance,"mmc-pack.json"));
        string config=Path.Combine(Instance,"instance.cfg");Directory.CreateDirectory(Instance);
        if(!File.Exists(config))File.Copy(Path.Combine(Runtime,"Templates","instance.cfg"),config);
        config=Path.Combine(Prism,"prismlauncher.cfg");if(!File.Exists(config))File.Copy(Path.Combine(Runtime,"Templates","prismlauncher.cfg"),config);
        foreach(string name in new[]{"skycraft-0.1.2.jar","fabric-api-0.161.0+26.3.jar"}) {
            string source=Path.Combine(Runtime,"Mods",name);
            // New recipes install jars directly in the actual instance so loaders and tests see them.
            if(File.Exists(source))CopyPayload(source,Path.Combine(Game,"mods",name));
        }
    }

    static void Configure(){
        Unpack();
        string java=Path.Combine(Runtime,"Runtime","Java","bin","javaw.exe").Replace('\\','/');
        foreach(string f in new[]{java,Path.Combine(Prism,"prismlauncher.exe"),Path.Combine(Game,"mods","skycraft-0.1.2.jar"),Path.Combine(Game,"mods","fabric-api-0.161.0+26.3.jar")})
            if(!File.Exists(f))throw new InvalidOperationException("The CastleCraft package is incomplete. Reinstall it from Melty.");
        SetIni(Path.Combine(Prism,"prismlauncher.cfg"),new Dictionary<string,string>{{"JavaPath",java},{"IgnoreJavaWizard","true"},{"AutomaticJavaSwitch","false"},{"AutomaticJavaDownload","false"},{"UserAskedAboutAutomaticJavaDownload","true"},{"ShowConsole","false"},{"ShowConsoleOnError","true"}});
        SetIni(Path.Combine(Instance,"instance.cfg"),new Dictionary<string,string>{{"OverrideJavaLocation","true"},{"JavaPath",java},{"OverrideJavaArgs","true"},{"JvmArgs","--enable-native-access=ALL-UNNAMED -Dskycraft.startHidden=true -Dskycraft.quitWithSkyrim=true"}});
        // Dedicated mod instance only. Do not touch the player's existing Minecraft profiles,
        // sound levels, framerate policy, saves, host window position or host resolution.
        string options=Path.Combine(Game,"options.txt");if(!File.Exists(options))File.WriteAllText(options,"pauseOnLostFocus:false\n",new UTF8Encoding(false));
    }
    static Process StartPrism(){
        var info=new ProcessStartInfo(Path.Combine(Prism,"prismlauncher.exe"),"--dir "+Quote(Prism)+" --launch CastleCraft");
        info.WorkingDirectory=Prism;info.UseShellExecute=false;return Process.Start(info);
    }
    static bool FreshClientReady(DateTime start){
        string log=Path.Combine(Game,"logs","latest.log");
        try {if(!File.Exists(log)||File.GetLastWriteTimeUtc(log)<start.AddSeconds(-2))return false;
            using(var stream=new FileStream(log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(stream)){
                string s=reader.ReadToEnd();return s.Contains("Backend library:")&&s.Contains("skycraft 0.1.2")&&!s.Contains("Mixin apply for mod skycraft failed");}}
        catch(IOException){return false;}
    }
    static int Setup(){
        if(File.Exists(Ready))File.Delete(Ready);
        DateTime start=DateTime.UtcNow;var prism=StartPrism();
        // Authentication happens only in Prism's own Microsoft account UI. We never copy
        // PCL launch commands, accounts.json, tokens or another launcher's account cache.
        while((DateTime.UtcNow-start).TotalMinutes<30){
            if(FreshClientReady(start)){
                File.WriteAllText(Ready,"Minecraft 26.3 ready\nFabric Loader 0.19.5; CastleCraft 0.3.0-dev.20261013\n",new UTF8Encoding(false));
                // Setup readiness is NOT gameplay proof. Melty tests the real host separately.
                // Leave the companion alive: the following Play attaches the host immediately.
                return 0;
            }
            if(prism.HasExited&&prism.ExitCode!=0)throw new InvalidOperationException("Minecraft setup did not complete. Open CastleCraft in Prism and sign in to the Microsoft account that owns Minecraft Java, then retry in Melty.");
            System.Threading.Thread.Sleep(1000);
        }
        throw new InvalidOperationException("Minecraft setup is waiting for sign-in or a game download. Complete it in Prism, then retry in Melty.");
    }
    static int Play(string[] args){
        string host=Value(args,"--host",null);
        if(string.IsNullOrEmpty(host)||!File.Exists(Path.Combine(host,"Lost_Castle.exe")))throw new InvalidOperationException("Melty must pass the player's installed Lost Castle folder.");
        if(!File.Exists(Ready))throw new InvalidOperationException("Minecraft first-run setup has not completed. Use Play in Melty to sign in and finish it.");
        // Prism single-instance handling refuses a duplicate running instance and keeps the
        // setup-started game; no separate background window arrangement or volume overwrite.
        StartPrism();
        string session=Path.Combine(Runtime,"sessions","manual");Directory.CreateDirectory(session);
        var info=new ProcessStartInfo(Path.Combine(host,"Lost_Castle.exe"));info.WorkingDirectory=host;info.UseShellExecute=false;
        info.EnvironmentVariables["CASTLECRAFT_SESSION"]=session;
        if(args.Contains("--test"))info.EnvironmentVariables["CASTLECRAFT_TEST"]="1";
        var castle=Process.Start(info);File.WriteAllText(Path.Combine(session,"castle.pid"),castle.Id.ToString());
        File.WriteAllText(Path.Combine(Runtime,"active-session.txt"),session);
        castle.WaitForExit();return castle.ExitCode;
    }
    static int SelfTest(){
        if(Quote("C:\\folder space\\")!="\"C:\\folder space\\\\\"")throw new Exception("argv quoting");
        if(Quote("a\"b")!="\"a\\\"b\"")throw new Exception("embedded quote");
        string s=UpdateIni("[General]\nJavaPath=old\nUserSetting=keep\n[Other]\nx=1\n",new Dictionary<string,string>{{"JavaPath","new"},{"JvmArgs","args"}});
        if(!s.Contains("UserSetting=keep")||!s.Contains("JavaPath=new")||!s.Contains("[Other]\nx=1")||s.Contains("JavaPath=old"))throw new Exception("non-destructive config update");
        return 0;
    }
    [STAThread] static int Main(string[] args){
        try{if(args.Contains("--self-test"))return SelfTest();Configure();if(args.Contains("--configure-only"))return 0;if(args.Contains("--companion")){var companion=StartPrism();companion.WaitForExit();return companion.ExitCode;}return args.Contains("--setup")?Setup():Play(args);}
        catch(Exception e){
            // Only our generic errors, never a command line, account identity or private file.
            try{File.AppendAllText(Path.Combine(Runtime,"launcher-errors.log"),DateTime.UtcNow.ToString("O")+" "+e.GetType().Name+"\n");}catch{}
            MessageBox.Show(e is InvalidOperationException?e.Message:"CastleCraft could not start. Check its package and retry from Melty.","CastleCraft");return 1;
        }
    }
}
}
