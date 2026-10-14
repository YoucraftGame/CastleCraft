package dev.skycraft.combat;

/** Integrates native frame displacement until the guest's next physics step. */
public final class CastleMotionAccumulator {
    public record Step(double x, double z, boolean locked, boolean verticalLocked) {
        public Step(double x,double z,boolean locked){this(x,z,locked,false);}
    }
    private double x, z;
    private boolean locked, verticalLocked;
    private long updated;
    public void accept(int code, double dx, double dz, long now) {
        if (code == 2 || !Double.isFinite(dx) || !Double.isFinite(dz)) { reset(); return; }
        if (updated != 0 && now - updated > 250_000_000L) reset();
        verticalLocked = code == 3;
        if(verticalLocked) x=z=0;
        else { x += dx; z += dz; }
        locked = (code & 1) != 0; updated = now;
    }
    public Step drain(long now) {
        if (updated == 0 || now - updated > 250_000_000L) { reset(); return new Step(0,0,false); }
        var step = new Step(x,z,locked,verticalLocked); x=z=0; return step;
    }
    public void reset() { x=z=0; locked=verticalLocked=false; updated=0; }
}
