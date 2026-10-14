using System;
namespace PeakCraft;
/// A heartbeat never proves arrival. Accept only the current room and actual server feet.
internal static class CastleHandoff {
 public static bool Accept(bool collisionReady,bool inWorld,uint seq,uint epoch,uint teleportAck,uint arrivalSeq,uint arrivalEpoch,int lifeSeq,int lifeAck,
  double originX,double originZ,double floorY,double x,double y,double z,double serverX,double serverY,double serverZ){
  if(!collisionReady||!inWorld||seq==0||epoch==0||lifeSeq<=0||lifeAck!=lifeSeq||teleportAck!=seq||arrivalSeq!=seq||arrivalEpoch!=epoch)return false;
  foreach(var value in new[]{originX,originZ,floorY,x,y,z,serverX,serverY,serverZ})if(!CastleExternalMotion.Finite(value))return false;
  double dx=x-originX,dz=z-originZ;
  return dx*dx+dz*dz<=2.01*2.01&&Math.Abs(x-serverX)<=.125&&Math.Abs(y-serverY)<=.125&&Math.Abs(z-serverZ)<=.125&&Math.Abs(y-floorY)<=.25;
 }
}
