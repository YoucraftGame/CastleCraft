using System;
namespace PeakCraft.World;

internal readonly struct CastleSpriteVertex {
 internal readonly double X,Y,U,V;
 internal CastleSpriteVertex(double x,double y,double u,double v){X=x;Y=y;U=u;V=v;}
}

// Recover the actual local-sprite -> packed-atlas affine transform from Unity's mesh.
// Never substitute a min/max UV rectangle: an atlas can rotate or mirror a packed sprite.
internal sealed class CastleSpriteUv {
 private readonly CastleSpriteVertex[] vertices;
 private readonly int[] triangles;
 private readonly double ox,oy,ou,ov,ux,uy,vx,vy;
 private CastleSpriteUv(CastleSpriteVertex[] vertices,int[] triangles,CastleSpriteVertex origin,double ux,double uy,double vx,double vy){
  this.vertices=(CastleSpriteVertex[])vertices.Clone();this.triangles=(int[])triangles.Clone();
  ox=origin.X;oy=origin.Y;ou=origin.U;ov=origin.V;this.ux=ux;this.uy=uy;this.vx=vx;this.vy=vy;
 }
 private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
 internal static bool TryCreate(CastleSpriteVertex[] vertices,int[] triangles,out CastleSpriteUv? result){
  result=null;if(vertices==null||triangles==null||vertices.Length<3||triangles.Length<3||triangles.Length%3!=0)return false;
  foreach(var p in vertices)if(!Finite(p.X)||!Finite(p.Y)||!Finite(p.U)||!Finite(p.V)||p.U<0||p.U>1||p.V<0||p.V>1)return false;
  foreach(int i in triangles)if(i<0||i>=vertices.Length)return false;
  for(int i=0;i<triangles.Length;i+=3){
   var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
   double bx=b.X-a.X,by=b.Y-a.Y,cx=c.X-a.X,cy=c.Y-a.Y,det=bx*cy-by*cx;
   if(Math.Abs(det)<1e-12)continue;
   double ux=((b.U-a.U)*cy-(c.U-a.U)*by)/det,uy=(bx*(c.U-a.U)-cx*(b.U-a.U))/det;
   double vx=((b.V-a.V)*cy-(c.V-a.V)*by)/det,vy=(bx*(c.V-a.V)-cx*(b.V-a.V))/det;
   var map=new CastleSpriteUv(vertices,triangles,a,ux,uy,vx,vy);
   foreach(var p in vertices){map.Map(p.X,p.Y,out double u,out double v);if(Math.Abs(u-p.U)>1e-6||Math.Abs(v-p.V)>1e-6)return false;}
   result=map;return true;
  }
  return false;
 }
 internal void Map(double x,double y,out double u,out double v){u=ou+(x-ox)*ux+(y-oy)*uy;v=ov+(x-ox)*vx+(y-oy)*vy;}
 // Texture readback must pass this test before sampling: a tightly packed atlas can put
 // another sprite inside the UV bounding rectangle but outside this sprite's triangles.
 internal bool Owns(double x,double y){
  if(!Finite(x)||!Finite(y))return false;
  for(int i=0;i<triangles.Length;i+=3){
   var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
   double bx=b.X-a.X,by=b.Y-a.Y,cx=c.X-a.X,cy=c.Y-a.Y,det=bx*cy-by*cx;
   if(Math.Abs(det)<1e-12)continue;
   double dx=x-a.X,dy=y-a.Y,s=(dx*cy-dy*cx)/det,t=(bx*dy-by*dx)/det;
   if(s>=-1e-8&&t>=-1e-8&&s+t<=1+1e-8)return true;
  }
  return false;
 }
 internal bool TryTexel(double x,double y,int width,int height,out int index){
  index=-1;if(width<=0||height<=0||!Owns(x,y))return false;
  Map(x,y,out double u,out double v);if(!Finite(u)||!Finite(v)||u<0||u>1||v<0||v>1)return false;
  int px=Math.Min(width-1,(int)(u*width)),py=Math.Min(height-1,(int)(v*height));
  index=checked(py*width+px);return true;
 }
 internal double PixelDensityX(int width,int height)=>Math.Sqrt(ux*ux*width*width+vx*vx*height*height);
 internal double PixelDensityY(int width,int height)=>Math.Sqrt(uy*uy*width*width+vy*vy*height*height);
}
