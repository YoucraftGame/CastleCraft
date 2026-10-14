package dev.skycraft.world;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class CastleMirrorLifecycleTest {
 @Test void disconnectedMirrorStaysSuspended(){assertTrue(CastleMirrorLifecycle.suspended(true,false,true,false));}
 @Test void ordinaryWorldIsNeverMutedOrSuspended(){assertFalse(CastleMirrorLifecycle.suspended(false,false,false,true));}
 @Test void nativeTransitionSuspendsButReadyMirrorResumes(){assertTrue(CastleMirrorLifecycle.suspended(true,true,true,true));assertFalse(CastleMirrorLifecycle.suspended(true,true,true,false));}
 @Test void suspendedEntityAudioStopsButMenusRemainNative(){assertTrue(CastleMirrorLifecycle.worldAudio(true,"HOSTILE"));assertTrue(CastleMirrorLifecycle.worldAudio(true,"NEUTRAL"));assertFalse(CastleMirrorLifecycle.worldAudio(true,"MASTER"));assertFalse(CastleMirrorLifecycle.worldAudio(false,"HOSTILE"));}
}
