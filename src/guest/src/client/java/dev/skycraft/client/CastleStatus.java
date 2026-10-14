package dev.skycraft.client;

import dev.skycraft.combat.SkyCombat;
import dev.skycraft.link.SkyLink;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicReference;
import net.minecraft.client.Minecraft;
import net.minecraft.server.MinecraftServer;

/** Life acknowledgements and health always describe the same authoritative server snapshot. */
final class CastleStatus {
    private static final long SAMPLE_INTERVAL_NS = 50_000_000L;
    private static final long RESEND_INTERVAL_NS = 1_000_000_000L;
    private static final AtomicReference<SampleRequest> pending = new AtomicReference<>();
    private static volatile long generation;
    private static volatile ServerSnapshot snapshot;
    private static long next, lastSent;
    private static String last = "";

    private record SampleRequest(long generation, long session, MinecraftServer server, UUID player) {}
    private record ServerSnapshot(SampleRequest request, long capturedAt, String text) {}

    private CastleStatus() {}

    static void reset() {
        generation++;
        snapshot = null;
        pending.set(null);
        next = lastSent = 0;
        last = "";
    }

    static void frame(Minecraft mc) {
        if (!SkyClient.linked() || mc.player == null) return;
        long now = System.nanoTime();
        if (now < next) return;
        next = now + SAMPLE_INTERVAL_NS;
        var server = mc.getSingleplayerServer();
        var playerId = mc.player.getUUID();
        long session = SkyClient.sessionId();
        var request = new SampleRequest(generation, session, server, playerId);
        if (pending.compareAndSet(null, request)) {
            server.execute(() -> capture(request));
        }
        var ready = snapshot;
        if (ready == null || ready.request.generation != generation || ready.request.session != session
            || ready.request.server != server || !ready.request.player.equals(playerId)
            || now - ready.capturedAt > RESEND_INTERVAL_NS) return;
        String text = ready.text.substring(0,ready.text.length()-1)+SkyClient.arrivalStatus()+"}";
        if ((!text.equals(last) || now - lastSent >= RESEND_INTERVAL_NS)
            && SkyLink.tryWriteRender(0xCC02, ByteBuffer.wrap(text.getBytes(StandardCharsets.UTF_8)), null)) {
            last = text;
            lastSent = now;
        }
    }

    private static void capture(SampleRequest request) {
        try {
            if (request.generation != generation || request.session != SkyClient.sessionId()) return;
            var player = request.server.getPlayerList().getPlayer(request.player);
            if (player == null || !SkyCombat.isBridgeLevel(player.level())) return;
            // This runs after prior life commands on the same server executor. A volatile ACK
            // paired with client-side health can acknowledge a revive before its health packet.
            StringBuilder text = new StringBuilder("{\"health\":").append(player.getHealth())
                .append(",\"maximum\":").append(player.getMaxHealth())
                .append(",\"lifeAck\":").append(CastleLife.acknowledged())
                .append(",\"absorption\":").append(player.getAbsorptionAmount())
                .append(",\"armor\":").append(player.getArmorValue())
                .append(",\"hurt\":").append(player.hurtTime)
                .append(",\"burning\":").append(player.isOnFire())
                .append(",\"creative\":").append(player.getAbilities().instabuild)
                .append(",\"effects\":[");
            boolean comma = false;
            for (var effect : player.getActiveEffects()) {
                if (comma) text.append(',');
                comma = true;
                text.append("{\"id\":\"").append(effect.getEffect().value().getDescriptionId())
                    .append("\",\"level\":").append(effect.getAmplifier())
                    .append(",\"ticks\":").append(effect.getDuration()).append('}');
            }
            text.append("]}");
            if (request.generation == generation && request.session == SkyClient.sessionId()) {
                snapshot = new ServerSnapshot(request, System.nanoTime(), text.toString());
            }
        } finally {
            pending.compareAndSet(request, null);
        }
    }
}
