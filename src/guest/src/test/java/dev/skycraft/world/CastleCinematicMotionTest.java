package dev.skycraft.world;

import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

class CastleCinematicMotionTest {
    @Test void nativeWalkingAdvancesPoseWithoutPhysics() {
        var m = new CastleCinematicMotion();
        m.sample(true, 7, 100, 0, 1); m.sample(true, 7, 100.2, 0, 1.05);
        assertEquals(.8, m.speed(), 1e-5); assertEquals(-90, m.yaw(), 1e-5);
        assertTrue(m.phase(1.075) > m.phase(1.05));
    }
    @Test void stationaryFeetStopInsteadOfSlidingInPlace() {
        var m = new CastleCinematicMotion();
        m.sample(true, 7, 100, 0, 1); m.sample(true, 7, 100.2, 0, 1.05);
        m.sample(true, 7, 100.2, 0, 1.1);
        assertEquals(0, m.speed()); assertEquals(m.phase(1.1), m.phase(5));
    }
    @Test void roomChangeAndLargeTeleportDoNotBecomeRunning() {
        var m = new CastleCinematicMotion();
        m.sample(true, 7, 100, 0, 1); m.sample(true, 8, 1124, 0, 1.05);
        assertEquals(0, m.speed()); assertEquals(0, m.phase(1.05));
        m.sample(true, 8, 1140, 0, 1.1); assertEquals(0, m.speed());
    }
    @Test void pauseDisconnectAndNewCinematicResetMotion() {
        var m = new CastleCinematicMotion();
        m.sample(true, 7, 100, 0, 1); m.sample(true, 7, 100.2, 0, 1.05);
        m.sample(false, 7, 100.2, 0, 1.1); assertFalse(m.active());
        m.sample(true, 7, 105, 0, 2); assertEquals(0, m.speed());
        m.sample(true, 7, Double.NaN, 0, 2.05); assertFalse(m.active());
    }
}
