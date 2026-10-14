using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CastleCraft {
class Launcher {
    [DllImport("shell32.dll",SetLastError=true)] static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)]string command,out int count);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr ptr);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr window,int x,int y,int w,int h,bool repaint);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    static string Quote(string arg) { return "\""+Regex.Replace(arg,@"(\\*)\""",@"$1$1\""")+Regex.Replace("",@"(\\+)$","$1$1")+"\""; }
    static string[] Split(string command) {
        int count; IntPtr ptr=CommandLineToArgvW(command,out count);
        try {string[] result=new string[count];for(int i=0;i<count;i++)result[i]=Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr,i*IntPtr.Size));return result;}finally{LocalFree(ptr);}
    }
    static string Value(string[] args,string key,string fallback) {int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
    static string FindLaunch(string minecraft) {
        var folder=new DirectoryInfo(minecraft);
        for(int depth=0;folder!=null&&depth<5;depth++,folder=folder.Parent) {
            foreach(string rel in new[]{"PCL/LatestLaunch.bat","LatestLaunch.bat"}) {string path=Path.Combine(folder.FullName,rel);if(File.Exists(path))return path;}
        }
        throw new InvalidOperationException("CastleCraft requires an authenticated Minecraft 26.3 PCL installation with its saved launch configuration. No account token is included in this release.");
    }
    static Process StartMinecraft(string installation,string session,string runtime,bool test) {
        // Read the player's own authenticated launch in memory. Never write or log its command line.
        string launch=FindLaunch(installation);
        string line=File.ReadLines(launch).FirstOrDefault(x=>Regex.IsMatch(x,@"\""[^\""\r\n]*javaw?\.exe\""",RegexOptions.IgnoreCase));
        if(line==null)throw new InvalidOperationException("No Java command in the player's launch configuration");
        int start=line.IndexOf('"');string[] original=Split(line.Substring(start));string exe=original[0];
        var args=original.Skip(1).ToList();
        int token=args.IndexOf("--accessToken");if(token<0||token+1>=args.Count||args[token+1].Length<20)throw new InvalidOperationException("Sign in to your Minecraft launcher before using CastleCraft.");
        string gameDir=Path.Combine(session,"simulation-"+DateTime.UtcNow.ToString("HHmmss"));Directory.CreateDirectory(Path.Combine(gameDir,"mods"));
        string source=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"skycraft-0.1.2.jar");
        File.Copy(source,Path.Combine(gameDir,"mods","skycraft-0.1.2.jar"),true);
        File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fabric-api-0.161.0+26.3.jar"),Path.Combine(gameDir,"mods","fabric-api-0.161.0+26.3.jar"),true);
        int dir=args.IndexOf("--gameDir");if(dir<0)throw new InvalidOperationException("Minecraft game directory argument missing");args[dir+1]=gameDir;
        int w=args.IndexOf("--width"),h=args.IndexOf("--height");if(w>=0)args[w+1]="960";else{args.Add("--width");args.Add("960");}if(h>=0)args[h+1]="600";else{args.Add("--height");args.Add("600");}
        // Avoid importing any community mod or resource pack from the original instance.
        args.Insert(0,"-Dskycraft.link=Local\\CastleCraft_v3_"+Path.GetFileName(session));args.Insert(0,"-Dskycraft.firstPersonAvatar=true");args.Insert(0,"--enable-native-access=ALL-UNNAMED");
        File.WriteAllText(Path.Combine(gameDir,"options.txt"),"pauseOnLostFocus:false\nrenderDistance:6\nsimulationDistance:5\nmaxFps:60\nfullscreen:false\nskipMultiplayerWarning:true\nsoundCategory_master:0.0\nsoundCategory_music:0.0\nsoundCategory_ambient:0.0\n");
        var info=new ProcessStartInfo(exe,string.Join(" ",args.Select(Quote).ToArray()));info.WorkingDirectory=gameDir;info.UseShellExecute=false;info.CreateNoWindow=true;
        info.RedirectStandardOutput=true;info.RedirectStandardError=true;
        var process=new Process();process.StartInfo=info;
        string log=Path.Combine(session,"minecraft-launch.log");object sync=new object();
        DataReceivedEventHandler handler=(sender,e)=>{if(e.Data!=null){lock(sync){if(!e.Data.Contains("accessToken")&&!e.Data.Contains("Authorization")&&!e.Data.Contains("--uuid"))File.AppendAllText(log,e.Data+Environment.NewLine);}}};
        process.OutputDataReceived+=handler;process.ErrorDataReceived+=handler;process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
        File.WriteAllText(Path.Combine(session,"minecraft.pid"),process.Id.ToString());return process;
    }
    [STAThread] static int Main(string[] args) {
        string runtime=Value(args,"--runtime",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CastleCraft"));Directory.CreateDirectory(runtime);
        string session=Path.Combine(runtime,"sessions",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
        try {
            bool test=args.Contains("--test"),mcOnly=args.Contains("--minecraft-only"),castleOnly=args.Contains("--castle-only");
            string host=Value(args,"--host",null),mc=Value(args,"--minecraft",null);
            if(castleOnly||args.Contains("--reuse-session")) {string pointer=Path.Combine(runtime,"active-session.txt");if(!File.Exists(pointer))throw new InvalidOperationException("Minecraft companion has not started");session=File.ReadAllText(pointer).Trim();}
            Directory.CreateDirectory(session);Directory.CreateDirectory(Path.Combine(session,"to-castle"));Directory.CreateDirectory(Path.Combine(session,"to-minecraft"));
            File.WriteAllText(Path.Combine(runtime,"active-session.txt"),session);
            Process minecraft=null,castle=null;
            if(!castleOnly){if(mc==null)throw new InvalidOperationException("Melty must provide the Minecraft folder");minecraft=StartMinecraft(mc,session,runtime,test);}
            if(!mcOnly){
                if(host==null||!File.Exists(Path.Combine(host,"Lost_Castle.exe")))throw new InvalidOperationException("Melty must provide the Lost Castle folder");
                var info=new ProcessStartInfo(Path.Combine(host,"Lost_Castle.exe"),"-screen-fullscreen 0 -screen-width 960 -screen-height 600");info.WorkingDirectory=host;info.UseShellExecute=false;
                info.EnvironmentVariables["CASTLECRAFT_SESSION"]=session;if(test)info.EnvironmentVariables["CASTLECRAFT_TEST"]="1";
                castle=Process.Start(info);File.WriteAllText(Path.Combine(session,"castle.pid"),castle.Id.ToString());
            }
            File.AppendAllText(Path.Combine(session,"launcher.log"),"CastleCraft launched real installed games; session "+Path.GetFileName(session)+Environment.NewLine);
            var bounds=Screen.PrimaryScreen.WorkingArea;
            for(int i=0;i<90;i++) {
                System.Threading.Thread.Sleep(1000);
                if(castle!=null&&!castle.HasExited){castle.Refresh();if(castle.MainWindowHandle!=IntPtr.Zero)MoveWindow(castle.MainWindowHandle,bounds.X,bounds.Y,Math.Min(980,bounds.Width/2),Math.Min(640,bounds.Height),true);}
                if(minecraft!=null&&!minecraft.HasExited){minecraft.Refresh();if(minecraft.MainWindowHandle!=IntPtr.Zero)ShowWindow(minecraft.MainWindowHandle,0);}
                if((castle==null||castle.HasExited)&&(minecraft==null||minecraft.HasExited))break;
            }
            // Keep stdout drains alive for Minecraft's entire lifetime.
            if(castle!=null){castle.WaitForExit();if(minecraft!=null&&!minecraft.HasExited)minecraft.Kill();}
            if(minecraft!=null&&!minecraft.HasExited)minecraft.WaitForExit();
            return 0;
        }catch(Exception e){File.AppendAllText(Path.Combine(runtime,"launcher-errors.log"),DateTime.UtcNow.ToString("O")+" "+e.GetType().Name+": "+e.Message+Environment.NewLine);MessageBox.Show(e.Message,"CastleCraft launch");return 1;}
    }
}
}
