using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
namespace PeakCraft.World;

/// <summary>Reads relative Windows mouse counts without relying on the game's InputManager axes.</summary>
internal sealed class CastleRawMouse : IDisposable {
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProc(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
 [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr previous,IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam);
 [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
 [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
 private IntPtr window,previous;
 private WindowProc? callback;
 private readonly IntPtr buffer=Marshal.AllocHGlobal(64);
 private int dx,dy;
 internal long Events;
 internal bool Enabled;
 public bool Install(){
  if(previous!=IntPtr.Zero)return true;
  window=Process.GetCurrentProcess().MainWindowHandle;if(window==IntPtr.Zero)return false;
  callback=Receive;previous=SetWindowLongPtr(window,-4,Marshal.GetFunctionPointerForDelegate(callback));return previous!=IntPtr.Zero;
 }
 private IntPtr Receive(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam){
  if(message==0xFF&&Enabled&&GetForegroundWindow()==window){
   uint size=64,header=(uint)(IntPtr.Size==8?24:16);
   if(GetRawInputData(lparam,0x10000003,buffer,ref size,header)!=uint.MaxValue&&size>=header+24&&Marshal.ReadInt32(buffer)==0){
    int offset=(int)header;
    if((Marshal.ReadInt16(buffer,offset)&1)==0){Interlocked.Add(ref dx,Marshal.ReadInt32(buffer,offset+12));Interlocked.Add(ref dy,Marshal.ReadInt32(buffer,offset+16));Events++;}
   }
  }
  return CallWindowProc(previous,hwnd,message,wparam,lparam);
 }
 public Vector2 Drain()=>new(Interlocked.Exchange(ref dx,0),Interlocked.Exchange(ref dy,0));
 public void Dispose(){Enabled=false;if(previous!=IntPtr.Zero){SetWindowLongPtr(window,-4,previous);previous=IntPtr.Zero;}Marshal.FreeHGlobal(buffer);callback=null;}
}
