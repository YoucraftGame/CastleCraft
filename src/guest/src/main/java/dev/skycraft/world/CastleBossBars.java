package dev.skycraft.world;

import dev.skycraft.SkyCraft;
import java.util.ArrayList;
import java.util.WeakHashMap;
import net.minecraft.server.level.ServerBossEvent;
import net.minecraft.server.level.ServerLevel;

/** Room-bound subscriptions, not boss deletion. Runs only on the integrated server thread. */
public final class CastleBossBars {
    private static final WeakHashMap<ServerBossEvent, Boolean> EVENTS = new WeakHashMap<>();
    private static java.lang.ref.WeakReference<ServerLevel> level = new java.lang.ref.WeakReference<>(null);
    private static Integer epoch;
    private CastleBossBars() {}
    public static void observe(ServerBossEvent event) { EVENTS.put(event, Boolean.TRUE); }
    public static void update(ServerLevel current, int currentEpoch) {
        if (!SkyCraft.isMirrorServer(current.getServer())) return;
        if (level.get() == current && epoch != null && epoch == currentEpoch) return;
        // Native room epoch changes before arrival resumes. Old room creatures remain
        // paused, so vanilla tracking-leave callbacks otherwise never remove their bars.
        int removed = 0;
        for (var event : new ArrayList<>(EVENTS.keySet())) {
            for (var player : new ArrayList<>(event.getPlayers())) {
                if (player.level() == current) { event.removePlayer(player); removed++; }
            }
        }
        level = new java.lang.ref.WeakReference<>(current); epoch = currentEpoch;
        if (removed > 0) SkyCraft.LOG.info("CastleCraft room boss subscriptions removed={} epoch={}", removed, currentEpoch);
    }
}
