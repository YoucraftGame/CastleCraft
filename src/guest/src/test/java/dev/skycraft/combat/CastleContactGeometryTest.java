package dev.skycraft.combat;

import static org.junit.jupiter.api.Assertions.*;
import org.junit.jupiter.api.Test;

class CastleContactGeometryTest {
    @Test void standingInShallowFlowCountsButFlyingAboveItDoesNot() {
        assertTrue(CastleContactGeometry.touchesFluid(64, 66, 64, .125));
        assertFalse(CastleContactGeometry.touchesFluid(64.2, 66.2, 64, .125));
        assertFalse(CastleContactGeometry.touchesFluid(64.125, 66.125, 64, .125));
    }
    @Test void fluidBelowFootOrAboveHeadDoesNotCauseDamage() {
        assertFalse(CastleContactGeometry.touchesFluid(65, 67, 64, 1));
        assertFalse(CastleContactGeometry.touchesFluid(62, 64, 64, 1));
        assertTrue(CastleContactGeometry.touchesFluid(63, 64.25, 64, 1));
    }
    @Test void rejectsInvalidGeometryAndEmptyFluid() {
        assertFalse(CastleContactGeometry.touchesFluid(64, 66, 64, 0));
        assertFalse(CastleContactGeometry.touchesFluid(64, 66, 64, Double.NaN));
        assertFalse(CastleContactGeometry.touchesFluid(Double.NEGATIVE_INFINITY, 66, 64, 1));
        assertFalse(CastleContactGeometry.touchesFluid(66, 64, 64, 1));
    }
    @Test void bossDimensionsKeepWideAndShallowAxesIndependent() {
        assertEquals(12, CastleContactGeometry.dimension(12, .6f));
        assertEquals(.4f, CastleContactGeometry.dimension(.4f, 12));
        assertEquals(.6f, CastleContactGeometry.dimension(Float.NaN, .6f));
        assertEquals(64, CastleContactGeometry.dimension(2000, .6f));
        assertEquals(.1f, CastleContactGeometry.dimension(.001f, .6f));
    }
}
