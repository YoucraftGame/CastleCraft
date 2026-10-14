using System;
using System.Collections.Generic;
using System.Threading;
namespace PeakCraft.World;

internal readonly struct CastleSkinVertex {
 internal readonly float X,Y,Z,U,V,NX,NY,NZ;
 internal CastleSkinVertex(double x,double y,double z,double u,double v,float nx,float ny,float nz){X=(float)x;Y=(float)y;Z=(float)z;U=(float)u;V=(float)v;NX=nx;NY=ny;NZ=nz;}
}
internal sealed class CastleVoxelSkin {
 internal CastleSkinVertex[] Vertices=Array.Empty<CastleSkinVertex>();
 internal int[] Opaque=Array.Empty<int>(),Blended=Array.Empty<int>();
 internal float[] Depths=Array.Empty<float>();
 internal int NX,NY,Width,Height;
 internal int FrontVertexCount;
 internal double Ppu,PivotX,PivotY;
 internal int BackSample;
 internal float HalfDepthAt(double x,double y){
  double px=x*Ppu+PivotX,py=y*Ppu+PivotY;if(px<0||py<0||px>=Width||py>=Height)return 0;
  return Depths[Math.Min(NY-1,(int)(py*NY/Height))*NX+Math.Min(NX-1,(int)(px*NX/Width))];
 }
 // This is a voxel reconstruction from the owner's pixels, not an authored anatomical model.
 // Native front UV is one-to-one in physical pixel units; unseen surfaces reuse a real native swatch.
 internal static CastleVoxelSkin Build(uint[] pixels,int width,int height,double ppu,double px,double py,double maximumDepth,bool rounded,CancellationToken token){
  if(width<=0||height<=0||pixels.Length!=checked(width*height)||ppu<=0||maximumDepth<=0||double.IsNaN(maximumDepth)||double.IsInfinity(maximumDepth))throw new ArgumentException("Invalid native voxel input");
  int nx=Math.Min(24,width),ny=Math.Min(24,height);var solid=new bool[nx*ny];var samples=new int[solid.Length];var distances=new int[solid.Length];
  var histogram=new Dictionary<int,(int count,int sample)>();int back=-1;
  for(int i=0;i<pixels.Length;i++){
   uint p=pixels[i];if((p>>24)<250)continue;if(back<0)back=i;
   int r=(int)(p&255),g=(int)((p>>8)&255),b=(int)((p>>16)&255),light=r+g+b;
   if(light<90||light>660)continue;
   int bin=(r>>4)|((g>>4)<<4)|((b>>4)<<8);histogram.TryGetValue(bin,out var h);histogram[bin]=(h.count+1,h.count==0?i:h.sample);
  }
  int best=0;foreach(var h in histogram.Values)if(h.count>best){best=h.count;back=h.sample;}
  if(back<0)for(int i=0;i<pixels.Length;i++)if((pixels[i]>>24)>=26){back=i;break;}
  if(back<0)return new CastleVoxelSkin();
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
   token.ThrowIfCancellationRequested();int x0=x*width/nx,x1=(x+1)*width/nx,y0=y*height/ny,y1=(y+1)*height/ny,index=y*nx+x;double score=double.MaxValue;
   for(int iy=y0;iy<y1;iy++)for(int ix=x0;ix<x1;ix++){
    int i=iy*width+ix;if((pixels[i]>>24)<26)continue;
    double s=Math.Abs(ix+.5-(x0+x1)*.5)+Math.Abs(iy+.5-(y0+y1)*.5);
    if(s<score){score=s;solid[index]=true;samples[index]=i;}
   }
   distances[index]=solid[index]?Math.Min(Math.Min(x+1,nx-x),Math.Min(y+1,ny-y)):0;
  }
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int i=y*nx+x;if(!solid[i])continue;if(x>0)distances[i]=Math.Min(distances[i],distances[i-1]+1);if(y>0)distances[i]=Math.Min(distances[i],distances[i-nx]+1);}
  int maxDistance=1;
  for(int y=ny-1;y>=0;y--)for(int x=nx-1;x>=0;x--){int i=y*nx+x;if(!solid[i])continue;if(x+1<nx)distances[i]=Math.Min(distances[i],distances[i+1]+1);if(y+1<ny)distances[i]=Math.Min(distances[i],distances[i+nx]+1);maxDistance=Math.Max(maxDistance,distances[i]);}
  var depths=new float[solid.Length];for(int i=0;i<depths.Length;i++)if(solid[i]){double t=rounded ? .35+.65*distances[i]/maxDistance:1;depths[i]=(float)(Math.Ceiling(t*8)/8*maximumDepth*.5);}
  var vertices=new List<CastleSkinVertex>();var opaque=new List<int>();var blended=new List<int>();
  double LX(double x)=>(x-px)/ppu;double LY(double y)=>(y-py)/ppu;
  void Quad(double x0,double y0,double z0,double x1,double y1,double z1,double x2,double y2,double z2,double x3,double y3,double z3,double u0,double v0,double u1,double v1,double u2,double v2,double u3,double v3,float ax,float ay,float az,bool soft){
   int n=vertices.Count;vertices.Add(new CastleSkinVertex(x0,y0,z0,u0,v0,ax,ay,az));vertices.Add(new CastleSkinVertex(x1,y1,z1,u1,v1,ax,ay,az));vertices.Add(new CastleSkinVertex(x2,y2,z2,u2,v2,ax,ay,az));vertices.Add(new CastleSkinVertex(x3,y3,z3,u3,v3,ax,ay,az));
   (soft?blended:opaque).AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+2,n+1,n,n+3,n+2,n});
  }
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
   token.ThrowIfCancellationRequested();int cell=y*nx+x;if(!solid[cell])continue;
   int x0=x*width/nx,x1=(x+1)*width/nx,y0=y*height/ny,y1=(y+1)*height/ny;double d=depths[cell];
   // Merge equal-opacity horizontal runs vertically: exact native holes and RGBA retained,
   // without emitting two triangles for every fully opaque native pixel.
   foreach(bool soft in new[]{false,true}){
    var rectangles=new List<(int x0,int x1,int y0,int y1)>();var previous=new Dictionary<(int,int),int>();
    for(int iy=y0;iy<y1;iy++){
     var current=new Dictionary<(int,int),int>();
     bool Included(int ix){uint a=pixels[iy*width+ix]>>24;return soft?a>=26&&a<250:a>=250;}
     for(int ix=x0;ix<x1;){if(!Included(ix)){ix++;continue;}int start=ix++;while(ix<x1&&Included(ix))ix++;var key=(start,ix);
      if(previous.TryGetValue(key,out int index)){var r=rectangles[index];rectangles[index]=(r.x0,r.x1,r.y0,iy+1);current[key]=index;}
      else{current[key]=rectangles.Count;rectangles.Add((start,ix,iy,iy+1));}
     }previous=current;
    }
    foreach(var r in rectangles){double a=LX(r.x0),b=LX(r.x1),c=LY(r.y0),e=LY(r.y1),u0=r.x0/(double)width,u1=r.x1/(double)width,v0=r.y0/(double)height,v1=r.y1/(double)height;
     Quad(a,c,-d,b,c,-d,b,e,-d,a,e,-d,u0,v0,u1,v0,u1,v1,u0,v1,0,0,-1,soft);
     int backPixel=soft?r.y0*width+r.x0:back;double bu=(backPixel%width+.5)/width,bv=(backPixel/width+.5)/height;
     Quad(b,c,d,a,c,d,a,e,d,b,e,d,bu,bv,bu,bv,bu,bv,bu,bv,0,0,1,soft);
    }
   }
   // Neighbor columns hide internal faces. Only exposed depth steps form side quads.
   for(int side=0;side<4;side++){
    int xx=x+(side==0?-1:side==1?1:0),yy=y+(side==2?-1:side==3?1:0);double neighbor=xx<0||xx>=nx||yy<0||yy>=ny?0:depths[yy*nx+xx];if(neighbor>=d)continue;
    int sample=samples[cell];double u=(sample%width+.5)/width,v=(sample/width+.5)/height;bool soft=(pixels[sample]>>24)<250;
    void Edge(double z0,double z1){
     if(side==0)Quad(LX(x0),LY(y0),z1,LX(x0),LY(y0),z0,LX(x0),LY(y1),z0,LX(x0),LY(y1),z1,u,v,u,v,u,v,u,v,-1,0,0,soft);
     else if(side==1)Quad(LX(x1),LY(y0),z0,LX(x1),LY(y0),z1,LX(x1),LY(y1),z1,LX(x1),LY(y1),z0,u,v,u,v,u,v,u,v,1,0,0,soft);
     else if(side==2)Quad(LX(x0),LY(y0),z1,LX(x1),LY(y0),z1,LX(x1),LY(y0),z0,LX(x0),LY(y0),z0,u,v,u,v,u,v,u,v,0,-1,0,soft);
     else Quad(LX(x0),LY(y1),z0,LX(x1),LY(y1),z0,LX(x1),LY(y1),z1,LX(x0),LY(y1),z1,u,v,u,v,u,v,u,v,0,1,0,soft);
    }
    if(neighbor==0)Edge(-d,d);else{Edge(-d,-neighbor);Edge(neighbor,d);}
   }
  }
  return new CastleVoxelSkin{Vertices=vertices.ToArray(),Opaque=opaque.ToArray(),Blended=blended.ToArray(),Depths=depths,NX=nx,NY=ny,Width=width,Height=height,Ppu=ppu,PivotX=px,PivotY=py,BackSample=back};
 }
}
