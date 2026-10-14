using System;
using System.Collections.Generic;
namespace PeakCraft.World;

// Display-only prediction. Minecraft remains the authority; acknowledgements retire input once.
internal sealed class CastleLook {
 private readonly Queue<(uint seq,int x,int y)> pending=new();
 private uint sequence;
 internal uint Add(int x,int y,uint ack=0){if(pending.Count==0&&unchecked((int)(ack-sequence))>0)sequence=ack;uint seq=++sequence;if(seq==0)seq=++sequence;pending.Enqueue((seq,x,y));if(pending.Count>256)pending.Clear();return seq;}
 internal void Reset()=>pending.Clear();
 internal void Resolve(float nativeYaw,float nativePitch,uint ack,float sensitivity,uint flags,out float yaw,out float pitch){
  while(pending.Count>0&&unchecked((int)(ack-pending.Peek().seq))>=0)pending.Dequeue();
  yaw=nativeYaw;pitch=nativePitch;
  // A stateful cinematic smoother cannot be predicted from raw counts alone.
  if((flags&4)==0||(flags&32)!=0)return;
  double f=sensitivity*.6000000238418579+.20000000298023224;
  double gain=f*f*f*((flags&64)!=0?1:8)*.15;
  foreach(var input in pending){yaw+=(float)(input.x*gain*((flags&8)!=0?-1:1));pitch=Math.Max(-90,Math.Min(90,pitch+(float)(input.y*gain*((flags&16)!=0?-1:1))));}
 }
}
