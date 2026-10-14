package dev.skycraft.combat;

/** Math used by both body hit boxes and fluid contact, independent of a running client. */
final class CastleContactGeometry {
    private CastleContactGeometry() {}
    static float dimension(float value, float fallback) {
        return Float.isFinite(value) && value > 0 ? Math.max(.1f, Math.min(64f, value)) : fallback;
    }
    static boolean touchesFluid(double bodyMinY, double bodyMaxY, int blockY, double fluidHeight) {
        return Double.isFinite(bodyMinY) && Double.isFinite(bodyMaxY) && Double.isFinite(fluidHeight)
            && bodyMaxY > bodyMinY && fluidHeight > 0 && bodyMaxY > blockY + 1e-6
            && bodyMinY < blockY + Math.min(1, fluidHeight) - 1e-6;
    }
}
