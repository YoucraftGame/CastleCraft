package dev.skycraft.combat;

/** Decode native HP only when explicitly tagged; legacy/other-host actors retain vanilla defaults. */
final class CastleActorVitality {
 static final int NATIVE_HEALTH = 512;
 static double maximum(int flags,int nativeMaximum) {
  return (flags&NATIVE_HEALTH)!=0&&nativeMaximum>0 ? nativeMaximum/5.0 : 20.0;
 }
 static float health(float fraction,float effectiveMaximum) {
  if(!Float.isFinite(fraction)||!Float.isFinite(effectiveMaximum)||effectiveMaximum<=0)return Float.NaN;
  return Math.max(.001F,effectiveMaximum*Math.clamp(fraction,0,1));
 }
}
