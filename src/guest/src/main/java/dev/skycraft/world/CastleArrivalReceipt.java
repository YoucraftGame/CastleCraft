package dev.skycraft.world;
/** Exact receipt identity: neither an old scene nor a previous teleport may restore controls. */
public final class CastleArrivalReceipt {
 public record Ticket(long session,int sequence,int epoch,int teleportId){}
 public record Room(long session,int sequence,int epoch){}
 private CastleArrivalReceipt(){}
 public static boolean matches(Ticket expected,long session,int sequence,int epoch,int receipt){
  return expected!=null&&expected.session==session&&expected.sequence==sequence&&expected.epoch==epoch&&expected.teleportId==receipt;
 }
 /** A focus/menu pause cannot create another arrival in an already acknowledged room. */
 public static boolean canResume(Room completed,long session,int sequence,int epoch,int acknowledgement,boolean pending){
  return !pending&&completed!=null&&completed.session==session&&completed.sequence==sequence
   &&completed.epoch==epoch&&acknowledgement==sequence;
 }
}
