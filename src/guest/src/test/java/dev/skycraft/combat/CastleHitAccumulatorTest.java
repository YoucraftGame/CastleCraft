package dev.skycraft.combat;

import static org.junit.jupiter.api.Assertions.*;
import org.junit.jupiter.api.Test;

class CastleHitAccumulatorTest {
    @Test void fireAndPhysicalHitsInOneTickRemainSeparate() {
        var hits = new CastleHitAccumulator();
        hits.add(4, 8, 0);
        hits.add(6, 0, 1);
        hits.add(3, 0, 1);
        var out = hits.drain(.6f, .8f, 1.5f, false);
        assertEquals(2, out.size());
        assertEquals(4, out.get(0).damage());
        assertEquals(8, out.get(0).flags());
        assertEquals(9, out.get(1).damage());
        assertEquals(0, out.get(1).flags());
        assertEquals(1, out.get(1).weapon());
        assertEquals(0, out.get(0).pushStrength(), "A fire tick must not consume melee knockback");
        assertEquals(1.5f, out.get(1).pushStrength(), "The physical hit receives native stagger once");
        assertTrue(hits.drain(0, 0, 0, false).isEmpty(), "Damage is sent only once");
    }
    @Test void projectileAndMeleeKeepTheirOwnImpactKind() {
        var hits = new CastleHitAccumulator();
        hits.add(4, 2, 5);
        hits.add(4, 0, 1);
        var out = hits.drain(0, 0, 0, false);
        assertEquals(2, out.size());
        assertEquals(2, out.get(0).flags());
        assertEquals(5, out.get(0).weapon());
        assertEquals(1, out.get(1).weapon());
    }
    @Test void rejectsInvalidDamageAndBoundsFiniteOverflow() {
        var hits = new CastleHitAccumulator();
        hits.add(Float.NaN, 0, 0);
        hits.add(Float.POSITIVE_INFINITY, 0, 0);
        hits.add(-1, 0, 0);
        hits.add(0, 0, 0);
        assertTrue(hits.drain(0, 0, 0, false).isEmpty());
        hits.add(Float.MAX_VALUE, 0, 0);
        hits.add(Float.MAX_VALUE, 0, 0);
        assertEquals(Float.MAX_VALUE, hits.drain(0, 0, 0, false).get(0).damage());
    }
    @Test void poisonAndInstantHarmStaySeparateAndOnlyTheImpactCanCrit() {
        var hits=new CastleHitAccumulator();
        hits.add(1,256,0);hits.add(6,0,0);
        var out=hits.drain(1,0,.8f,true);
        assertEquals(2,out.size());assertEquals(256,out.get(0).flags());
        assertEquals(0,out.get(0).pushStrength());assertEquals(1,out.get(1).flags());
        assertEquals(.8f,out.get(1).pushStrength());
    }
    @Test void knockbackOnlyDoesNotBecomeSyntheticDamage() {
        var hit = new CastleHitAccumulator().drain(1, 0, .5f, false).get(0);
        assertEquals(0, hit.damage());
        assertEquals(.5f, hit.pushStrength());
    }
}
