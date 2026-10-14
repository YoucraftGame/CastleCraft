package dev.skycraft.world;
import static org.junit.jupiter.api.Assertions.*;
import java.util.List;
import org.junit.jupiter.api.Test;
import net.minecraft.world.phys.shapes.Shapes;
class CastleShadowSupportTest {
 @Test void standingShadowUsesFloorTop(){
  assertEquals(0,CastleShadowSupport.height(.5,0,.5,List.of(Shapes.box(0,-1,0,1,0,1))).orElseThrow());
 }
 @Test void jumpingShadowStaysOnGround(){
  assertEquals(0,CastleShadowSupport.height(.5,2,.5,List.of(Shapes.box(0,-1,0,1,0,1))).orElseThrow());
 }
 @Test void highestSupportedStepWins(){
  assertEquals(.5,CastleShadowSupport.height(.5,.5,.5,List.of(Shapes.box(0,-1,0,1,0,1),Shapes.box(0,0,0,1,.5,1))).orElseThrow());
 }
 @Test void adjacentWallAndCeilingDoNotLiftShadow(){
  assertEquals(0,CastleShadowSupport.height(.5,0,.5,List.of(Shapes.box(0,-1,0,1,0,1),Shapes.box(1,0,0,2,8,1),Shapes.box(0,4,0,1,5,1))).orElseThrow());
 }
 @Test void voidAndVeryDistantFloorHaveNoShadow(){
  assertTrue(CastleShadowSupport.height(.5,0,.5,List.of()).isEmpty());
  assertTrue(CastleShadowSupport.height(.5,5,.5,List.of(Shapes.box(0,-1,0,1,0,1))).isEmpty());
 }
}
