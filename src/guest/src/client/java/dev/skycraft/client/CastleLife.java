package dev.skycraft.client;

import dev.skycraft.SkyCraft;
import net.minecraft.client.Minecraft;
import net.minecraft.world.entity.Entity;

/** Host life messages are idempotent; only a native rebirth/new run may respawn the mirror. */
public final class CastleLife {
    private static volatile int acknowledged;
    private static volatile long acknowledgedSession = -1;
    private CastleLife() {}
    public static int acknowledged() { return acknowledgedSession == SkyClient.sessionId() ? acknowledged : 0; }
    public static void accept(Minecraft mc, int code, int health, int absorption, int sequence) {
        var server = mc.getSingleplayerServer();
        var local = mc.player;
        if (!SkyClient.linked() || !SkyCraft.isMirrorServer(server) || local == null || sequence == acknowledged()) return;
        long session = SkyClient.sessionId();
        var uuid = local.getUUID();
        server.execute(() -> {
            if (session != SkyClient.sessionId() || !SkyClient.linked() || mc.getSingleplayerServer() != server) return;
            if (sequence == acknowledged()) return;
            var player = server.getPlayerList().getPlayer(uuid);
            if (player == null) return;
            if (player.isDeadOrDying()) {
                if (health <= 0) { acknowledged = sequence; acknowledgedSession = session; return; }
                if (code != 1) return;
                var position = player.position();
                player = server.getPlayerList().respawn(player, false, Entity.RemovalReason.KILLED);
                player.teleportTo(position.x, position.y, position.z);
                player.resetFallDistance();
            }
            float maximum = player.getMaxHealth();
            if (health <= 0) player.hurtServer(player.level(), player.damageSources().genericKill(), Float.MAX_VALUE);
            else player.setHealth(Math.max(0, Math.min(maximum, maximum * health / 1_000_000f)));
            player.setAbsorptionAmount(Math.max(0, maximum * absorption / 1_000_000f));
            if (code == 1 && health > 0) {
                player.deathTime = 0;
                player.setDeltaMovement(net.minecraft.world.phys.Vec3.ZERO);
                player.resetFallDistance();
            }
            player.connection.send(new net.minecraft.network.protocol.game.ClientboundSetHealthPacket(player.getHealth(),player.getFoodData().getFoodLevel(),player.getFoodData().getSaturationLevel()));
            acknowledged = sequence; acknowledgedSession = session;
            float authoritativeHealth=player.getHealth();
            if (code == 1 && health > 0) mc.execute(() -> {
                if (session == SkyClient.sessionId() && SkyClient.linked()&&acknowledged()==sequence && mc.player != null && mc.player.getUUID().equals(uuid)) {
                    mc.player.setHealth(authoritativeHealth);mc.player.deathTime=0;mc.player.resetFallDistance();
                    if(mc.gui.screen() instanceof net.minecraft.client.gui.screens.DeathScreen)mc.gui.setScreen(null);
                    InputBridge.releaseAll();
                }
            });
            SkyCraft.LOG.info("CastleCraft life applied: sequence={}, health={}, nativeRevival={}", sequence, player.getHealth(), code == 1);
        });
    }
}
