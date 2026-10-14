using System;
namespace PeakCraft.World;

internal enum CastleChestFrame { Unknown,Body,ClosedLid,OpenedLid }
// Native chest_gold controller uses a different-sized sprite when the cap opens.
// This contract is display-only. Native Animator, sound and item-drop events retain ownership.
internal static class CastleChestFrames {
 internal static CastleChestFrame Resolve(string name,int width,int height)=>name switch {
  "bossChest_0" when width==252&&height==133=>CastleChestFrame.Body,
  "bossChest_2" when width==262&&height==136=>CastleChestFrame.ClosedLid,
  "bossChest_1" when width==249&&height==125=>CastleChestFrame.OpenedLid,
  _=>CastleChestFrame.Unknown
 };
 internal static bool TryNativeOffset(CastleChestFrame frame,double capX,double capY,double bodyPivotX,double bodyPivotY,double framePivotX,double framePivotY,double ppu,out double x,out double y){
  x=y=0;
  if(frame!=CastleChestFrame.ClosedLid&&frame!=CastleChestFrame.OpenedLid)return false;
  if(!Finite(capX)||!Finite(capY)||!Finite(bodyPivotX)||!Finite(bodyPivotY)||!Finite(framePivotX)||!Finite(framePivotY)||!Finite(ppu))return false;
  if(ppu<=0)return false;
  x=bodyPivotX+capX*ppu-framePivotX;y=bodyPivotY+capY*ppu-framePivotY;
  return true;
 }
 private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
}
