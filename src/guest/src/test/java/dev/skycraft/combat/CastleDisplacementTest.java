package dev.skycraft.combat;
import static org.junit.jupiter.api.Assertions.*;
import java.util.List;
import org.junit.jupiter.api.Test;
import net.minecraft.world.phys.AABB;
import net.minecraft.world.phys.Vec3;
import net.minecraft.world.phys.shapes.Shapes;
class CastleDisplacementTest {
 private static final AABB BODY=new AABB(.2,0,.2,.8,1.8,.8);
 @Test void waterAndPushRetainHorizontalDirection(){
  var v=CastleDisplacement.solve(BODY,new Vec3(.04,3,-.08),List.of());
  assertEquals(.04,v.x,1e-9);assertEquals(-.08,v.z,1e-9);assertEquals(0,v.y);
 }
 @Test void nativeWallStopsPushUsingVanillaCollision(){
  var v=CastleDisplacement.solve(BODY,new Vec3(.25,0,0),List.of(Shapes.box(.9,-1,-1,2,4,2)));
  assertEquals(.1,v.x,1e-9);
 }
 @Test void diagonalPushSlidesAlongWall(){
  var v=CastleDisplacement.solve(BODY,new Vec3(.2,0,.1),List.of(Shapes.box(.9,-1,-1,2,4,2)));
  assertEquals(.1,v.x,1e-9);assertEquals(.1,v.z,1e-9);
 }
 @Test void largeImpulseIsBoundedAndVerticalGravityIsNotExported(){
  var v=CastleDisplacement.limit(new Vec3(3,-20,4));
  assertEquals(.25,v.length(),1e-9);assertEquals(0,v.y);assertEquals(.75,v.x/v.z,1e-9);
 }
 @Test void malformedImpulseCannotMoveNativeActor(){
  assertEquals(Vec3.ZERO,CastleDisplacement.limit(new Vec3(Double.NaN,0,1)));
  assertEquals(Vec3.ZERO,CastleDisplacement.limit(new Vec3(0,0,Double.POSITIVE_INFINITY)));
 }
 @Test void remoteRoomFloorDoesNotBlockHorizontalFlow(){
  var box=BODY.move(65536,0,-8);
  var v=CastleDisplacement.solve(box,new Vec3(.05,0,0),List.of(Shapes.box(65535,-1,-9,65538,0,-6)));
  assertEquals(.05,v.x,1e-9);
 }
}
