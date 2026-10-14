using UnityEngine;
namespace PeakCraft.World;
internal static class Coords {
 public const float Scale=0.5f;
 public const float DepthScale=Scale*.6f;
 public const int GeometryLayer=30;
 public const int AvatarLayer=31;
 public const int PortraitLayer=28;
 public static double YOffset=0;
 public static double RoomX=0;
 public static int RenderLayer=0;
 public static int SortingLayer=0;
 public static int SortingOrder=0;
 public static float ZPlane=0;
 public static float ZBase=0;
 // Canonical Unity space uses MC +X/+Y/-Z at Scale. Castle projection shears floor depth into Y.
 public static Matrix4x4 Unproject {
  get {var m=Matrix4x4.identity;float factor=DepthScale*.6f/Scale;
   m.m12=-1f/.6f;m.m13=ZBase/.6f;m.m22=1f/factor;m.m23=-ZBase/factor;return m;}
 }
 public static Vector3 Canonical(double x,double y,double z)=>new((float)(x-RoomX)*Scale,(float)(y-YOffset)*Scale,(float)-z*Scale);
 public static bool CheckProjection(){
  foreach(var point in new[]{new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(0,0,1),new Vector3(2,3,-4)}){
   var projected=ToUnity(RoomX+point.x,YOffset+point.y,point.z);
   var expected=new Vector3(point.x,point.y,-point.z)*Scale;
   if((Unproject.MultiplyPoint3x4(projected)-expected).sqrMagnitude>1e-8f)return false;
  }return true;
 }
 public static Vector3 Relative(double x,double y,double z)=>new((float)x*Scale,(float)y*Scale-(float)z*DepthScale,(float)-z*DepthScale*.6f);
 public static Vector3 ToUnity(double x,double y,double z)=>new((float)(x-RoomX)*Scale,(float)(y-YOffset)*Scale-(float)z*DepthScale,ZBase-(float)z*DepthScale*.6f);
 public static Vector3 Feet(double x,double z)=>new((float)(x-RoomX)*Scale,(float)-z*DepthScale,ZBase-(float)z*DepthScale*.6f);
 public static void ToMc(Vector3 v,out double x,out double y,out double z){x=v.x/Scale+RoomX;y=YOffset;z=-v.y/DepthScale;}
}
