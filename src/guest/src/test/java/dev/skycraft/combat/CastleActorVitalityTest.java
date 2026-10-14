package dev.skycraft.combat;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class CastleActorVitalityTest {
 @Test void nativeBossUsesActualHealthUnitsForVanillaPoisonAndHitEffects(){
  assertEquals(400,CastleActorVitality.maximum(512,2000));
  assertEquals(80,CastleActorVitality.health(.2f,400));
 }
 @Test void untaggedLevelNeverChangesLegacyOrOtherHostHealth(){
  assertEquals(20,CastleActorVitality.maximum(0,500));
  assertEquals(20,CastleActorVitality.maximum(512,0));
 }
 @Test void snapshotsCannotReviveRemovedActorsOrPublishInvalidHealth(){
  assertEquals(.001f,CastleActorVitality.health(0,100));
  assertEquals(100,CastleActorVitality.health(2,100));
  assertTrue(Float.isNaN(CastleActorVitality.health(Float.NaN,100)));
 }
}
