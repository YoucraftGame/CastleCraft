using System;
using System.Threading;
namespace PeakCraft.World;
internal sealed class CastleSpritePixelCopy {
 internal int Width,Height;
 internal uint[] Pixels=Array.Empty<uint>(); // R | G<<8 | B<<16 | A<<24
 internal byte[] Alpha=Array.Empty<byte>();
 internal bool Translucent;
}
internal static class CastleSpritePixels {
 // Pure managed sampler used by the bounded background jobs. No invented colors,
 // scaling filter, alpha replacement or native atlas mutation.
 internal static CastleSpritePixelCopy Unpack(CastleSpriteUv map,int width,int height,double px,double py,double ppu,uint[] source,int aw,int ah,uint[]? split,int sw,int sh,CancellationToken token){
  if(width<=0||height<=0||ppu<=0||double.IsNaN(ppu)||double.IsInfinity(ppu)||source.Length!=checked(aw*ah)||split!=null&&split.Length!=checked(sw*sh))throw new ArgumentException("Invalid native pixel input");
  var pixels=new uint[checked(width*height)];var alpha=new byte[pixels.Length];bool translucent=false;
  for(int y=0;y<height;y++){
   if((y&15)==0)token.ThrowIfCancellationRequested();
   for(int x=0;x<width;x++){
    double lx=(x+.5-px)/ppu,ly=(y+.5-py)/ppu;
    if(!map.TryTexel(lx,ly,aw,ah,out int index))continue;
    uint pixel=source[index];if(split!=null&&map.TryTexel(lx,ly,sw,sh,out int ai))pixel=(pixel&0xFFFFFFu)|((split[ai]&255u)<<24);
    int dest=y*width+x;pixels[dest]=pixel;alpha[dest]=(byte)(pixel>>24);if(alpha[dest]>=26&&alpha[dest]<250)translucent=true;
   }
  }
  return new CastleSpritePixelCopy{Width=width,Height=height,Pixels=pixels,Alpha=alpha,Translucent=translucent};
 }
}
