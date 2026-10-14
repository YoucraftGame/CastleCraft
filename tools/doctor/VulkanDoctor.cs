using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Management;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

// Uses the installed system loader. No registry edits, game launches or DLL downloads.
public class VulkanDoctor:Form {
 [StructLayout(LayoutKind.Sequential)] struct InstanceInfo {public uint type;public IntPtr next;public uint flags;public IntPtr app;public uint layers;public IntPtr layerNames;public uint extensions;public IntPtr extensionNames;}
 [StructLayout(LayoutKind.Sequential)] struct SurfaceInfo {public uint type;public IntPtr next;public uint flags;public IntPtr instance;public IntPtr window;}
 [DllImport("vulkan-1.dll")] static extern int vkEnumerateInstanceExtensionProperties(IntPtr layer,ref uint count,IntPtr data);
 [DllImport("vulkan-1.dll")] static extern int vkCreateInstance(ref InstanceInfo info,IntPtr allocator,out IntPtr instance);
 [DllImport("vulkan-1.dll")] static extern void vkDestroyInstance(IntPtr instance,IntPtr allocator);
 [DllImport("vulkan-1.dll")] static extern int vkEnumeratePhysicalDevices(IntPtr instance,ref uint count,IntPtr devices);
 [DllImport("vulkan-1.dll")] static extern void vkGetPhysicalDeviceProperties(IntPtr device,IntPtr properties);
 [DllImport("vulkan-1.dll")] static extern IntPtr vkGetInstanceProcAddr(IntPtr instance,string name);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int CreateSurface(IntPtr instance,ref SurfaceInfo info,IntPtr allocator,out ulong surface);
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void DestroySurface(IntPtr instance,ulong surface,IntPtr allocator);
 public static string Detect(){
  var text=new StringBuilder();
  try{using(var q=new ManagementObjectSearcher("SELECT Name,DriverVersion,DriverDate,PNPDeviceID FROM Win32_VideoController"))foreach(ManagementObject gpu in q.Get()){text.AppendLine("显卡："+gpu["Name"]+"\r\nWindows 驱动版本："+gpu["DriverVersion"]+"\r\n驱动日期："+gpu["DriverDate"]);}}
  catch(Exception e){text.AppendLine("WMI 驱动信息受限，改读显示设备登记："+e.Message);try{using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}")){if(key!=null)foreach(var name in key.GetSubKeyNames()){using(var gpu=key.OpenSubKey(name)){if(gpu==null||gpu.GetValue("DriverVersion")==null)continue;text.AppendLine("显卡："+gpu.GetValue("DriverDesc")+"，Windows 驱动版本："+gpu.GetValue("DriverVersion")+"，日期："+gpu.GetValue("DriverDate"));}}}}catch(Exception fallback){text.AppendLine("登记信息无法读取："+fallback.Message);}}
  IntPtr data=IntPtr.Zero,instance=IntPtr.Zero,names=IntPtr.Zero,props=IntPtr.Zero,devices=IntPtr.Zero;var allocated=new System.Collections.Generic.List<IntPtr>();ulong surface=0;DestroySurface destroy=null;
  try{
   uint count=0;int result=vkEnumerateInstanceExtensionProperties(IntPtr.Zero,ref count,IntPtr.Zero);if(result!=0)throw new Exception("枚举扩展失败 VkResult="+result);
   if(count>4096)throw new Exception("异常扩展数量");data=Marshal.AllocHGlobal(checked((int)count*260));result=vkEnumerateInstanceExtensionProperties(IntPtr.Zero,ref count,data);if(result!=0)throw new Exception("读取扩展失败 VkResult="+result);
   string[] required={"VK_KHR_surface","VK_KHR_win32_surface"};bool ok=true;
   foreach(string name in required){bool found=false;for(int i=0;i<count;i++)if(Marshal.PtrToStringAnsi(IntPtr.Add(data,i*260))==name)found=true;text.AppendLine(name+"："+(found?"存在":"缺失"));ok &= found;}
   if(!ok){text.AppendLine("未通过：安装/修复显卡官方驱动后重新检测。不能单独安装显示扩展。");return text.ToString();}
   names=Marshal.AllocHGlobal(IntPtr.Size*2);for(int i=0;i<2;i++){var p=Marshal.StringToHGlobalAnsi(required[i]);allocated.Add(p);Marshal.WriteIntPtr(names,i*IntPtr.Size,p);}
   var info=new InstanceInfo{type=1,extensions=2,extensionNames=names};result=vkCreateInstance(ref info,IntPtr.Zero,out instance);if(result!=0)throw new Exception("创建 Vulkan 实例失败 VkResult="+result);
   var cp=vkGetInstanceProcAddr(instance,"vkCreateWin32SurfaceKHR");var dp=vkGetInstanceProcAddr(instance,"vkDestroySurfaceKHR");if(cp==IntPtr.Zero||dp==IntPtr.Zero)throw new Exception("显示扩展入口缺失");
   destroy=(DestroySurface)Marshal.GetDelegateForFunctionPointer(dp,typeof(DestroySurface));
   using(var window=new Form()){var si=new SurfaceInfo{type=1000009000,instance=Marshal.GetHINSTANCE(typeof(VulkanDoctor).Module),window=window.Handle};result=((CreateSurface)Marshal.GetDelegateForFunctionPointer(cp,typeof(CreateSurface)))(instance,ref si,IntPtr.Zero,out surface);if(result!=0)throw new Exception("Win32 Surface 创建失败 VkResult="+result);
    uint n=0;result=vkEnumeratePhysicalDevices(instance,ref n,IntPtr.Zero);if(result!=0||n==0||n>256)throw new Exception("未找到可用 Vulkan 显卡");devices=Marshal.AllocHGlobal((int)n*IntPtr.Size);result=vkEnumeratePhysicalDevices(instance,ref n,devices);if(result!=0)throw new Exception("读取 Vulkan 显卡失败");props=Marshal.AllocHGlobal(4096);bool compatible=false;
    for(int i=0;i<n;i++){vkGetPhysicalDeviceProperties(Marshal.ReadIntPtr(devices,i*IntPtr.Size),props);uint api=unchecked((uint)Marshal.ReadInt32(props));string name=Marshal.PtrToStringAnsi(IntPtr.Add(props,20));uint major=api>>22,minor=(api>>12)&1023;compatible |= major>1||(major==1&&minor>=3);text.AppendLine("Vulkan GPU："+name+"，API "+major+"."+minor+"."+(api&4095)+"，原始驱动编码 "+unchecked((uint)Marshal.ReadInt32(props,4)));}
    text.AppendLine(compatible?"显示扩展与 Surface 创建通过；至少一张显卡支持 Vulkan 1.3。Minecraft 实际启用以 F3 为准，尚未验证交换链呈现及桥接共享。":"显示扩展存在，但未找到 Vulkan 1.3 显卡；升级驱动后复测，旧硬件可能不支持。");
    destroy(instance,surface,IntPtr.Zero);surface=0;
   }
  }catch(Exception e){text.AppendLine("Vulkan 检测未通过："+e.Message+"\r\n请使用下方对应品牌官方安装助手，完成驱动安装后重启并复测。");}
  finally{if(surface!=0&&destroy!=null)destroy(instance,surface,IntPtr.Zero);if(instance!=IntPtr.Zero)vkDestroyInstance(instance,IntPtr.Zero);foreach(var p in allocated)Marshal.FreeHGlobal(p);foreach(var p in new[]{data,names,props,devices})if(p!=IntPtr.Zero)Marshal.FreeHGlobal(p);}
  return text.ToString();
 }
 public VulkanDoctor(){Text="Vulkan 显示扩展与显卡驱动助手";ClientSize=new Size(940,620);Font=new Font("Microsoft YaHei UI",10);var output=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true};Controls.Add(output);var panel=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=110};Controls.Add(panel);
  Action<string,string> link=(title,url)=>{var button=new Button{Text=title,AutoSize=true};button.Click+=(s,e)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});panel.Controls.Add(button);};
  link("NVIDIA 官方安装助手","https://www.nvidia.com/en-us/software/nvidia-app/");link("AMD 官方自动安装","https://www.amd.com/en/resources/support-articles/faqs/GPU-Driver-Autodetect.html");link("Intel 官方自动安装","https://www.intel.com/content/www/us/en/support/detect.html");
  var install=new Button{Text="运行已下载的官方安装器",AutoSize=true};install.Click+=(s,e)=>InstallOfficial();panel.Controls.Add(install);var check=new Button{Text="重新检测",AutoSize=true};check.Click+=(s,e)=>output.Text=Detect();panel.Controls.Add(check);panel.Controls.Add(new Label{Text="按检测结果选择对应品牌。按钮打开官方页面，由官方助手匹配驱动。安装器需有效品牌签名；安装会请求 Windows 管理员权限。",AutoSize=true,MaximumSize=new Size(900,0)});Shown+=(s,e)=>output.Text=Detect();
 }
 static void InstallOfficial(){using(var picker=new OpenFileDialog{Filter="官方驱动安装器|*.exe"}){if(picker.ShowDialog()!=DialogResult.OK)return;try{
   // Literal path travels as UTF-8 Base64, never interpolated into shell syntax.
   string path=Convert.ToBase64String(Encoding.UTF8.GetBytes(picker.FileName));string code="$p=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+path+"'));$s=Get-AuthenticodeSignature -LiteralPath $p;if($s.Status -eq 'Valid' -and $s.SignerCertificate.Subject -match 'NVIDIA|Advanced Micro Devices|Intel Corporation'){exit 0}else{exit 3}";
   var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),"-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(code))){UseShellExecute=false,CreateNoWindow=true};using(var p=Process.Start(start)){if(!p.WaitForExit(20000)){p.Kill();throw new Exception("签名校验超时");}if(p.ExitCode!=0)throw new Exception("不是签名有效的 NVIDIA / AMD / Intel 安装器");}
   Process.Start(new ProcessStartInfo(picker.FileName){UseShellExecute=true,Verb="runas"});
  }catch(Exception e){MessageBox.Show(e.Message,"安装未启动");}}}
}
