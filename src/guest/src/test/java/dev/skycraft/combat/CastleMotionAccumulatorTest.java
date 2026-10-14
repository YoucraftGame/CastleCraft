package dev.skycraft.combat;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class CastleMotionAccumulatorTest {
 @Test void allNativeFramesReachOnePhysicsStep(){var m=new CastleMotionAccumulator();for(int i=0;i<3;i++)m.accept(1,.1,-.2,1+i*16_000_000L);var s=m.drain(50_000_001L);assertEquals(.3,s.x(),1e-9);assertEquals(-.6,s.z(),1e-9);assertTrue(s.locked());assertEquals(0,m.drain(51_000_001L).x());}
 @Test void disconnectedOrExpiredMotionCannotMovePlayer(){var m=new CastleMotionAccumulator();m.accept(1,2,2,1);assertEquals(new CastleMotionAccumulator.Step(0,0,false),m.drain(300_000_001L));m.accept(1,2,2,400_000_001L);m.accept(2,0,0,400_000_002L);assertEquals(new CastleMotionAccumulator.Step(0,0,false),m.drain(400_000_003L));}
 @Test void newBurstDoesNotReplayStaleDisplacement(){var m=new CastleMotionAccumulator();m.accept(1,4,4,1);m.accept(0,.25,.5,500_000_001L);assertEquals(new CastleMotionAccumulator.Step(.25,.5,false),m.drain(500_000_002L));}
 @Test void restraintDiscardsPreviousDashAndHoldsAllAxesUntilItEnds(){var m=new CastleMotionAccumulator();m.accept(1,2,2,1);m.accept(3,0,0,2);assertEquals(new CastleMotionAccumulator.Step(0,0,true,true),m.drain(3));assertTrue(m.drain(4).verticalLocked());m.accept(0,.1,.2,5);assertEquals(new CastleMotionAccumulator.Step(.1,.2,false),m.drain(6));}
 @Test void staleRestraintCannotLeaveFlyingPlayerLocked(){var m=new CastleMotionAccumulator();m.accept(3,0,0,1);assertEquals(new CastleMotionAccumulator.Step(0,0,false),m.drain(300_000_001L));}
}
