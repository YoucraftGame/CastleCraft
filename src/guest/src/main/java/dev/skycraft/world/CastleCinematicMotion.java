package dev.skycraft.world;

/** Presentation only: measure native scripted feet without injecting player velocity or inputs. */
public final class CastleCinematicMotion {
    private boolean active;
    private int epoch;
    private double x, z, time;
    private float phase, speed, yaw;

    public void sample(boolean enabled, int roomEpoch, double nextX, double nextZ, double now) {
        if (!enabled || !Double.isFinite(nextX) || !Double.isFinite(nextZ) || !Double.isFinite(now)) {
            active = false; speed = phase = 0; return;
        }
        double dt = now - time, dx = nextX - x, dz = nextZ - z;
        if (!active || epoch != roomEpoch || dt <= 0 || dt > .25 || dx*dx+dz*dz > 4) {
            phase = speed = 0;
        } else {
            double distance = Math.sqrt(dx*dx+dz*dz);
            speed = (float)Math.min(1, distance / dt * .2);
            phase += (float)(distance * 4);
            if (distance > .001) yaw = (float)Math.toDegrees(Math.atan2(-dx, dz));
        }
        active = true; epoch = roomEpoch; x = nextX; z = nextZ; time = now;
    }
    public boolean active() { return active; }
    public float speed() { return speed; }
    public float yaw() { return yaw; }
    public float phase(double now) { return phase + speed * (float)Math.max(0, Math.min(.05, now-time)) * 20; }
}
