package dev.skycraft.client;

import dev.skycraft.combat.CastleMotionAccumulator;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.world.entity.MoverType;
import net.minecraft.world.phys.Vec3;

/** Native skills contribute displacement to Minecraft's existing collision motor. */
public final class CastleMotion {
    private static final CastleMotionAccumulator PENDING = new CastleMotionAccumulator();
    private CastleMotion() {}
    public static void accept(int code, int x, int z) {
        PENDING.accept(code,x/1_000_000.0,z/1_000_000.0,System.nanoTime());
    }
    public static void reset() { PENDING.reset(); }
    public static Vec3 apply(LocalPlayer player, MoverType type, Vec3 movement) {
        if (!SkyClient.linked() || player.isDeadOrDying()) { reset(); return movement; }
        if (type != MoverType.SELF) return movement;
        var step = PENDING.drain(System.nanoTime());
        if(step.verticalLocked()) player.setDeltaMovement(Vec3.ZERO);
        return new Vec3((step.locked()?0:movement.x)+step.x(), step.verticalLocked()?0:movement.y,
            (step.locked()?0:movement.z)+step.z());
    }
}
