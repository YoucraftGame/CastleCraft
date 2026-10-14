package dev.skycraft.combat;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class CastleHunterActionClockTest {
 @Test void distinctNativeAttacksDoNotNeedAnIdleFrame(){
  var clock=new CastleHunterActionClock();assertFalse(clock.advance(260,.1F,true));assertTrue(clock.advance(260,.4F,true));
  assertFalse(clock.advance(260,.6F,true));assertTrue(clock.advance(261,.4F,true));
 }
 @Test void phaseAndLoopChangesRearmExactlyOnce(){
  var clock=new CastleHunterActionClock();assertTrue(clock.advance(260,.5F,true));assertTrue(clock.advance(516,.5F,true));
  assertFalse(clock.advance(516,.1F,true));assertTrue(clock.advance(516,.5F,true));
 }
 @Test void cinematicsNeverReplayAPastActionWhenCombatResumes(){
  var clock=new CastleHunterActionClock();assertFalse(clock.advance(260,.4F,false));assertFalse(clock.advance(260,.8F,true));
  assertTrue(clock.advance(261,.4F,true));
 }
 @Test void birthHurtAndDeathArePresentationOnly(){
  for(int action:new int[]{0,1,2,3,10,11,12,13,14,15,16,17})assertFalse(CastleHunterActionClock.attacks(256|action));
  for(int action=4;action<=9;action++)assertTrue(CastleHunterActionClock.attacks(512|action));
 }
}
