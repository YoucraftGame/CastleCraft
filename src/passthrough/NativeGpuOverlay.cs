using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
namespace PeakCraft.Render;
// GPU copies execute on Unity's render thread. Main thread only binds/owns the output texture.
internal sealed class NativeGpuOverlay {
 private Texture2D? texture;private bool initialized,failed;private IntPtr callback;
 private float nextDiagnostic;
 private readonly bool enabled=Environment.GetEnvironmentVariable("CASTLECRAFT_GPU_OVERLAY")!="0"&&Plugin.Instance.Config.Bind("Render","GpuOverlayEnabled",true,"Use optional OpenGL/D3D11 GPU overlay sharing; unsupported devices retain CPU transport.").Value;
 [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr LoadLibraryW(string path);
 [DllImport("CastleCraft.GpuBridge.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int CC_HostInit(IntPtr texture);
 [DllImport("CastleCraft.GpuBridge.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int CC_HostInfo(out int width,out int height);
 [DllImport("CastleCraft.GpuBridge.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int CC_HostBind(IntPtr texture);
 [DllImport("CastleCraft.GpuBridge.dll",CallingConvention=CallingConvention.Cdecl)]private static extern IntPtr CC_RenderEvent();
 [DllImport("CastleCraft.GpuBridge.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int CC_HostReady();
 [DllImport("CastleCraft.GpuBridge.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int CC_Error();
 [DllImport("CastleCraft.GpuBridge.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int CC_HostTopDown();
 // The producer preserves Minecraft main-target row order (bottom-up on GL and Vulkan).
 // D3D CopyResource preserves it too; the API name is not an image-origin contract.
 public bool TopDown=>false;
 [DllImport("CastleCraft.GpuBridge.dll",CallingConvention=CallingConvention.Cdecl)]private static extern void CC_HostClose();
 public Texture2D? Ready=>initialized&&texture!=null&&CC_HostReady()!=0?texture:null;
 public void Update(){
  if(!enabled||failed||SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Direct3D11)return;
  try{
   if(!initialized){
    string path=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"CastleCraft.GpuBridge.dll");
    if(LoadLibraryW(path)==IntPtr.Zero)throw new DllNotFoundException("GPU native DLL unavailable: "+Marshal.GetLastWin32Error());
    var probe=new Texture2D(1,1,TextureFormat.RGBA32,false);probe.Apply(false);
    try{initialized=CC_HostInit(probe.GetNativeTexturePtr())!=0;}finally{UnityEngine.Object.Destroy(probe);}
    if(!initialized)throw new InvalidOperationException("GPU host device initialization failed");
    callback=CC_RenderEvent();Plugin.Log.LogInfo("GPU overlay: D3D11 consumer initialized; waiting for OpenGL/Vulkan interop, CPU fallback retained.");
   }
   if(CC_HostInfo(out int w,out int h)==0)return;
   if(texture==null||texture.width!=w||texture.height!=h){
    var next=new Texture2D(w,h,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};next.Apply(false);
    CC_HostBind(next.GetNativeTexturePtr());if(texture!=null)UnityEngine.Object.Destroy(texture);texture=next;
    Plugin.Log.LogInfo($"GPU overlay: shared {w}x{h}, zero-timeout keyed mutex, GPU copy queued.");
   }
   GL.IssuePluginEvent(callback,1);
   if(Time.unscaledTime>=nextDiagnostic){nextDiagnostic=Time.unscaledTime+10f;int code=CC_Error();if(code!=0)Plugin.Log.LogWarning($"GPU consumer copy pending: native reason {code}; CPU fallback active={CC_HostReady()==0}");}
  }catch(Exception e){failed=true;Plugin.Log.LogWarning("GPU overlay disabled; retaining CPU transport: "+e.Message);if(initialized){CC_HostClose();initialized=false;}}
 }
 public void Clear(){if(initialized)CC_HostBind(IntPtr.Zero);if(texture!=null)UnityEngine.Object.Destroy(texture);texture=null;}
 public void Dispose(){Clear();if(initialized)CC_HostClose();initialized=false;}
}
