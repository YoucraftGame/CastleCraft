package dev.skycraft.combat;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class CastleHunterTacticsTest {
 @Test void lowHealthPrioritizesRealHealingConsumables(){assertTrue(CastleHunterTactics.score(true,true,true,false,false,false)>CastleHunterTactics.score(true,true,false,true,false,false));}
 @Test void rangeSelectsWeaponWithoutPlayerSelection(){assertTrue(CastleHunterTactics.score(false,true,false,true,false,false)>CastleHunterTactics.score(false,true,false,false,true,false));assertTrue(CastleHunterTactics.score(false,false,false,false,true,false)>CastleHunterTactics.score(false,false,false,true,false,false));}
 @Test void ordinaryFoodIsNotInventedMobHealing(){assertEquals(0,CastleHunterTactics.score(false,false,false,false,false,false));}
}
