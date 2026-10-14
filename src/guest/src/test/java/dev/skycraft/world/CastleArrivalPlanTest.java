package dev.skycraft.world;
import static org.junit.jupiter.api.Assertions.*;
import org.junit.jupiter.api.Test;
class CastleArrivalPlanTest {
 @Test void safeOriginalFeetRemainExact(){
  var p=CastleArrivalPlan.choose(13312.123,-8.75,(x,z)->true);
  assertEquals(13312.123,p.x());assertEquals(-8.75,p.z());
 }
 @Test void bossGateUsesNearestHorizontalClearance(){
  var p=CastleArrivalPlan.choose(42,8,(x,z)->x>=42.5);
  assertNotNull(p);assertEquals(42.5,p.x());assertEquals(8,p.z());
 }
 @Test void unsupportedRoomNeverAcknowledgesArrival(){
  assertNull(CastleArrivalPlan.choose(10,20,(x,z)->false));
 }
 @Test void blockedDoorDoesNotSearchRoofOrOutsideTwoBlocks(){
  assertNull(CastleArrivalPlan.choose(10,20,(x,z)->x>12));
  var p=CastleArrivalPlan.choose(10,20,(x,z)->x>=12);
  assertEquals(12,p.x());assertEquals(20,p.z());
 }
 @Test void diagonalClearanceStaysInsideCircle(){
  var p=CastleArrivalPlan.choose(100,-80,(x,z)->x>=101&&z>=-79);
  assertNotNull(p);assertTrue(Math.hypot(p.x()-100,p.z()+80)<=2);
 }
 @Test void nonFiniteRequestIsRejectedBeforeCollisionQuery(){
  assertNull(CastleArrivalPlan.choose(Double.NaN,0,(x,z)->{fail("invalid query");return true;}));
  assertNull(CastleArrivalPlan.choose(0,Double.POSITIVE_INFINITY,(x,z)->true));
 }
}
