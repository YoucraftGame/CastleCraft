namespace PeakCraft;
internal enum CastleLifeTransition { Wait, NativeDeath, NativeRevival, GuestDeath, Mirror }
internal static class CastleLifeGate {
 public static CastleLifeTransition Evaluate(bool sharedDead,bool nativeAlive,int nativeHp,bool owns,bool hasGuestHealth,bool acknowledged,float guestHealth,bool confirmedNativeLife=true){
  bool nativeDead=!nativeAlive||nativeHp<=0;
  if(sharedDead)return nativeDead?CastleLifeTransition.Wait:CastleLifeTransition.NativeRevival;
  if(nativeDead)return confirmedNativeLife&&nativeHp<=0?CastleLifeTransition.NativeDeath:CastleLifeTransition.Wait;
  if(!owns||!hasGuestHealth||!acknowledged)return CastleLifeTransition.Wait;
  return guestHealth<=0?CastleLifeTransition.GuestDeath:CastleLifeTransition.Mirror;
 }
 public static int NextSequence(int sequence)=>sequence==int.MaxValue?1:sequence+1;
}
