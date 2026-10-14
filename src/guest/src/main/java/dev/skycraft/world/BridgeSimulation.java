package dev.skycraft.world;

import dev.skycraft.SkyCraft;
import dev.skycraft.combat.SkyCombat;
import dev.skycraft.link.SkyLink;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.player.Player;

/** Keeps saved entities in their own room while the native collision stream is unavailable. */
public final class BridgeSimulation {
    private static final SkyLink.SkyState INPUT = new SkyLink.SkyState();
    private static volatile Room room;
    private static volatile net.minecraft.world.level.Level clientMirror;
    public static void setClientMirror(net.minecraft.world.level.Level level){clientMirror=level;}
    public static boolean isClientMirror(net.minecraft.world.level.Level level){return level!=null&&level==clientMirror;}
    private static boolean haveInput;
    private record Room(ServerLevel level, boolean active, boolean cinematic, int epoch, double x, double y, double z) {}
    private BridgeSimulation() {}
    public static void update(ServerLevel level) {
        if (!SkyCraft.isMirrorServer(level.getServer()) || level.dimension() != net.minecraft.world.level.Level.OVERWORLD) return;
        boolean active = SkyLink.active();
        // A busy seqlock writer is not a disconnect. INPUT retains the last coherent state.
        if (active && SkyLink.readSkyState(INPUT)) haveInput = true;
        if (!active) haveInput = false;
        boolean valid = active && haveInput;
        if (valid) CastleBossBars.update(level, INPUT.collisionEpoch);
        room = new Room(level, valid && INPUT.inGame() && !INPUT.loading(), valid && INPUT.cinematic(), INPUT.collisionEpoch, INPUT.x, INPUT.y, INPUT.z);
    }
    public static boolean cinematic(Entity entity){
        var state=room;return state!=null&&state.cinematic&&state.active&&entity.level()==state.level&&SkyLink.active();
    }
    /** Native scripts own only the player's feet; other Minecraft entities keep ticking. */
    public static void followCinematicPlayer(Entity entity){
        var state=room;
        if(!(entity instanceof Player)||!cinematic(entity)||state.epoch!=SkyCollision.epoch())return;
        entity.setDeltaMovement(net.minecraft.world.phys.Vec3.ZERO);
        entity.setPos(state.x,state.y,state.z);entity.resetFallDistance();
    }
    public static boolean paused(Entity entity) {
        var state = room;
        if (entity.level() instanceof ServerLevel level) {
            if (!SkyCraft.isMirrorServer(level.getServer()) || level.dimension() != net.minecraft.world.level.Level.OVERWORLD) return false;
            if (state == null || state.level != level) return true;
        } else if (!isClientMirror(entity.level()) && !SkyCombat.isBridgeGeometry(entity.level())) return false;
        if (state == null || !state.active || !SkyLink.active() || state.epoch != SkyCollision.epoch()) return true;
        int floor = (int)Math.floor(state.y);
        int hx = (int)Math.floor(state.x), hz = (int)Math.floor(state.z);
        // The host deliberately sends the spawn body region last. Do not resume earlier.
        if (!SkyCollision.isKnown(hx, floor, hz) || !SkyCollision.isKnown(hx, floor-1, hz)) return true;
        // Server physics still waits for each entity's floor. Client presentation must
        // consume authoritative movement/interpolation packets even if its local floor
        // snapshot lags; otherwise visuals and entity-following sounds freeze at spawn.
        if (entity.level().isClientSide()) return false;
        int x = (int)Math.floor(entity.getX()), z = (int)Math.floor(entity.getZ());
        // Sample the room floor even for flying creatures/projectiles high above it.
        return !SkyCollision.isKnown(x, floor, z) || !SkyCollision.isKnown(x, floor-1, z);
    }
}
