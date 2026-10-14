package dev.skycraft.combat;
public final class CastleMobReceipt {
 private CastleMobReceipt(){}
 public static boolean matches(long contextSession,long session,int serial,int code,int epoch,int currentEpoch,long elapsed){return contextSession==session&&serial==(code>>>4)&&epoch==currentEpoch&&elapsed>=0&&elapsed<2_000_000_000L;}
}
