using System;
namespace PeakCraft.World;

/// <summary>Some native stages use +/-100000 to disable camera limits, not to size the floor.</summary>
internal static class CastleCollisionBounds {
 internal static bool Unbounded(double value)=>double.IsNaN(value)||double.IsInfinity(value)||Math.Abs(value)>=99999;
 internal static bool TryAxis(double cameraMin,double cameraMax,double padding,bool hasWalls,double wallMin,double wallMax,double wallMargin,out double min,out double max){
  min=Unbounded(cameraMin)?wallMin-wallMargin:cameraMin-padding;
  max=Unbounded(cameraMax)?wallMax+wallMargin:cameraMax+padding;
  if((Unbounded(cameraMin)||Unbounded(cameraMax))&&(!hasWalls||Unbounded(wallMin)||Unbounded(wallMax)||wallMin>=wallMax))return false;
  return !double.IsNaN(min)&&!double.IsNaN(max)&&!double.IsInfinity(min)&&!double.IsInfinity(max)&&min<max;
 }
}
