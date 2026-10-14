using System;
using System.Collections.Generic;
using System.Threading;
namespace PeakCraft.World;

// Native-source candidates for bossChest_0 and bossChest_2. Not a generic sprite extrusion.
// Pixel coordinates use the untrimmed native sprite, bottom-left origin, 100 pixels/unit.
// Front ornament stays on one plane; unseen wood faces reuse a native wood region.
internal static class CastleChestSkin {
 internal static CastleVoxelSkin Build(uint[] pixels,int width,int height,double ppu,double px,double py,CancellationToken token){
  bool lid=width==262&&height==136;
  if(!lid&&(width!=252||height!=133))throw new ArgumentException("Native chest source dimensions changed");
  var front=CastleVoxelSkin.Build(pixels,width,height,ppu,px,py,160/ppu,false,token);
  var vertices=new List<CastleSkinVertex>();var opaque=new List<int>();var blended=new List<int>();
  // Preserve original artwork, transparency, and pixel-to-UV mapping without depth tiles.
  foreach(var batch in new[]{(front.Opaque,opaque),(front.Blended,blended)})
   for(int k=0;k<batch.Item1.Length;k+=12){int start=batch.Item1[k];if(front.Vertices[start].NZ!=-1)continue;
    int n=vertices.Count;for(int j=0;j<4;j++)vertices.Add(front.Vertices[start+j]);
    foreach(int i in new[]{0,1,2,0,2,3,2,1,0,3,2,0})batch.Item2.Add(n+i);
   }
  int frontVertexCount=vertices.Count;
  void Face(double[] a,double[] b,double[] c,double[] d,int tx,int ty,int tw,int th,float nx,float ny,float nz){
   double lenU=Math.Sqrt(Math.Pow(b[0]-a[0],2)+Math.Pow(b[1]-a[1],2)+Math.Pow(b[2]-a[2],2));
   double lenV=Math.Sqrt(Math.Pow(d[0]-a[0],2)+Math.Pow(d[1]-a[1],2)+Math.Pow(d[2]-a[2],2));
   // Mirror repeated native regions at their edges. Every full tile retains pixel density.
   for(int y=0;y<lenV;y+=th)for(int x=0;x<lenU;x+=tw){token.ThrowIfCancellationRequested();
    double u0=x/lenU,u1=Math.Min(x+tw,lenU)/lenU,v0=y/lenV,v1=Math.Min(y+th,lenV)/lenV;
    double su0=tx,su1=tx+Math.Min(tw,lenU-x),sv0=ty,sv1=ty+Math.Min(th,lenV-y);
    if((x/tw)%2==1){su0=tx+tw;su1=su0-Math.Min(tw,lenU-x);}if((y/th)%2==1){sv0=ty+th;sv1=sv0-Math.Min(th,lenV-y);}
    int n=vertices.Count;
    void V(double u,double v,double su,double sv){double X=a[0]+(b[0]-a[0])*u+(d[0]-a[0])*v,Y=a[1]+(b[1]-a[1])*u+(d[1]-a[1])*v,Z=a[2]+(b[2]-a[2])*u+(d[2]-a[2])*v;vertices.Add(new CastleSkinVertex((X-px)/ppu,(Y-py)/ppu,Z/ppu,su/width,sv/height,nx,ny,nz));}
    V(u0,v0,su0,sv0);V(u1,v0,su1,sv0);V(u1,v1,su1,sv1);V(u0,v1,su0,sv1);
    foreach(int i in new[]{0,1,2,0,2,3,2,1,0,3,2,0})opaque.Add(n+i);
   }
  }
  double[] P(double x,double y,double z)=>new[]{x,y,z};
  // One connected box shell. Front is the original native image, not a regenerated atlas.
  // Native lid origin is (-5, +62.5) source pixels relative to the body's bottom-left.
  // Its gold skull overlaps the body; the solid lid starts at the body top (126px).
  double l=lid?49:44,r=lid?213:208,bottom=lid?63.5:8,top=126,f=-80,back=80;
  int tx=lid?80:72,ty=lid?80:37,tw=24,th=lid?32:51;
  void FramedFace(double[] a,double[] b,double[] c,double[] d,float nx,float ny,float nz){
   if(lid){Face(a,b,c,d,tx,ty,tw,th,nx,ny,nz);return;}
   // Divide the physical face into wood panel plus metal edge bands. No coplanar
   // overlay and no gaps: all adjacent faces share the same geometric endpoints.
   double length=Math.Sqrt(Math.Pow(b[0]-a[0],2)+Math.Pow(b[1]-a[1],2)+Math.Pow(b[2]-a[2],2));
   double height=Math.Sqrt(Math.Pow(d[0]-a[0],2)+Math.Pow(d[1]-a[1],2)+Math.Pow(d[2]-a[2],2));
   double[] Point(double u,double v)=>new[]{a[0]+(b[0]-a[0])*u+(d[0]-a[0])*v,a[1]+(b[1]-a[1])*u+(d[1]-a[1])*v,a[2]+(b[2]-a[2])*u+(d[2]-a[2])*v};
   double[] Recess(double[] p)=>new[]{p[0]-nx*2,p[1]-ny*2,p[2]-nz*2};
   double[] us={0,8/length,1-8/length,1},vs={0,8/height,1-8/height,1};
   for(int y=0;y<3;y++)for(int x=0;x<3;x++){
    bool gold=x!=1||y!=1;
    var p0=Point(us[x],vs[y]);var p1=Point(us[x+1],vs[y]);var p2=Point(us[x+1],vs[y+1]);var p3=Point(us[x],vs[y+1]);
    if(!gold){p0=Recess(p0);p1=Recess(p1);p2=Recess(p2);p3=Recess(p3);}
    Face(p0,p1,p2,p3,gold?90:tx,gold?7:ty,gold?8:tw,gold?4:th,nx,ny,nz);
   }
   // Physical 2px reveal closes all four panel/frame joins; the frame is raised
   // geometry rather than a painted gold stripe floating over the wood.
   var q0=Point(us[1],vs[1]);var q1=Point(us[2],vs[1]);var q2=Point(us[2],vs[2]);var q3=Point(us[1],vs[2]);
   var q=new[]{q0,q1,q2,q3};
   for(int edge=0;edge<4;edge++){
    var p0=q[edge];var p1=q[(edge+1)%4];double[] normal;
    if(edge==0||edge==2){double sign=edge==0?1:-1;normal=new[]{(d[0]-a[0])/height*sign,(d[1]-a[1])/height*sign,(d[2]-a[2])/height*sign};}
    else{double sign=edge==3?1:-1;normal=new[]{(b[0]-a[0])/length*sign,(b[1]-a[1])/length*sign,(b[2]-a[2])/length*sign};}
    Face(p0,p1,Recess(p1),Recess(p0),90,7,8,4,(float)normal[0],(float)normal[1],(float)normal[2]);
   }
  }
  FramedFace(P(l,bottom,f),P(l,bottom,back),P(l,top,back),P(l,top,f),-1,0,0);
  FramedFace(P(r,bottom,back),P(r,bottom,f),P(r,top,f),P(r,top,back),1,0,0);
  FramedFace(P(l,bottom,back),P(r,bottom,back),P(r,top,back),P(l,top,back),0,0,1);
  Face(P(l,top,f),P(r,top,f),P(r,top,back),P(l,top,back),tx,ty,tw,th,0,1,0);
  Face(P(l,bottom,back),P(r,bottom,back),P(r,bottom,f),P(l,bottom,f),tx,ty,tw,th,0,-1,0);
  return new CastleVoxelSkin{Vertices=vertices.ToArray(),Opaque=opaque.ToArray(),Blended=blended.ToArray(),Depths=front.Depths,NX=front.NX,NY=front.NY,Width=width,Height=height,Ppu=ppu,PivotX=px,PivotY=py,BackSample=front.BackSample,FrontVertexCount=frontVertexCount};
 }
}
