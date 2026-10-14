package dev.skycraft.world;
import org.junit.jupiter.api.Test;
import java.util.HashSet;
import static org.junit.jupiter.api.Assertions.*;

class CastleResumeAndBlockUpdatesTest {
 @Test void acknowledgedRoomResumesWithoutRevalidatingFeet(){
  var completed=new CastleArrivalReceipt.Room(5,10,2);
  // Focus changes, jumping, standing on placed blocks do not change receipt identity.
  for(int i=0;i<20;i++)assertTrue(CastleArrivalReceipt.canResume(completed,5,10,2,10,false));
 }
 @Test void newRoomSessionUnacknowledgedOrPendingMustStillWait(){
  var c=new CastleArrivalReceipt.Room(5,10,2);
  assertFalse(CastleArrivalReceipt.canResume(c,6,10,2,10,false));
  assertFalse(CastleArrivalReceipt.canResume(c,5,11,2,10,false));
  assertFalse(CastleArrivalReceipt.canResume(c,5,10,3,10,false));
  assertFalse(CastleArrivalReceipt.canResume(c,5,10,2,0,false));
  assertFalse(CastleArrivalReceipt.canResume(c,5,10,2,10,true));
  assertFalse(CastleArrivalReceipt.canResume(null,5,10,2,10,false));
 }
 private HashSet<String> dirty(int x,int y,int z){
  var s=new HashSet<String>();CastleBlockUpdates.changed(x,y,z,(a,b,c)->assertTrue(s.add(a+","+b+","+c)));return s;
 }
 @Test void interiorChangeUpdatesOnlyItsSection(){assertEquals(java.util.Set.of("2,0,-1"),dirty(35,5,-5));}
 @Test void positiveCornerIncludesAllEightNeighbourSections(){
  var s=dirty(15,15,15);assertEquals(8,s.size());assertTrue(s.contains("0,0,0"));assertTrue(s.contains("1,1,1"));
 }
 @Test void negativeCoordinatesAndZeroBoundaryUseFloorDivision(){
  var s=dirty(0,0,0);assertEquals(8,s.size());assertTrue(s.contains("-1,-1,-1"));assertTrue(s.contains("0,0,0"));
  s=dirty(-16,-16,-16);assertEquals(8,s.size());assertTrue(s.contains("-2,-2,-2"));assertTrue(s.contains("-1,-1,-1"));
 }
 @Test void consecutiveChangesAlwaysEnqueueCurrentAndPreviousSections(){
  var queue=new HashSet<String>();
  CastleBlockUpdates.Dirty add=(x,y,z)->queue.add(x+","+y+","+z);
  CastleBlockUpdates.changed(5,5,5,add);assertTrue(queue.remove("0,0,0"));
  CastleBlockUpdates.changed(35,5,5,add);assertTrue(queue.remove("2,0,0"));
  CastleBlockUpdates.changed(5,5,5,add);assertTrue(queue.remove("0,0,0"));assertTrue(queue.isEmpty());
 }
}
