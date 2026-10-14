package dev.skycraft.world;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class CastleScriptedPoseTest {
 @Test void nativeMoveLoopsInsteadOfFreezingAtNormalizedOne(){assertEquals(.05F,CastleScriptedPose.progress(6,.95F,1,.1F),1e-5);}
 @Test void nonLoopingRevivalEndsRatherThanRepeats(){assertEquals(1,CastleScriptedPose.progress(10,.95F,1,.1F));}
 @Test void pausedClockRetainsExactPose(){assertEquals(.4F,CastleScriptedPose.progress(6,.4F,2,0));}
 @Test void nativeCycleMapsToOneMinecraftLegCycle(){assertEquals(Math.PI*2,CastleScriptedPose.walkPhase(1)*.6662,1e-5);}
 @Test void jumpFlexesInFlightAndKeepsFiniteLandingPose(){assertTrue(CastleScriptedPose.jumpBend(.5F)>CastleScriptedPose.jumpBend(1));assertEquals(0,CastleScriptedPose.progress(8,Float.NaN,1,.1F));}
}
