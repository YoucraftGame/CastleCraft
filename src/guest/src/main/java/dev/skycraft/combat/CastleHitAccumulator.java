package dev.skycraft.combat;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Preserve different damage sources in the same tick; fire immunity must not swallow a sword hit. */
final class CastleHitAccumulator {
    record Hit(float damage, float pushX, float pushZ, float pushStrength, int flags, int weapon) {}
    private final Map<Long, Hit> pending = new LinkedHashMap<>();

    void add(float damage, int flags, int weapon) {
        if (!Float.isFinite(damage) || damage <= 0) return;
        long key = ((long)flags << 32) | (weapon & 0xffffffffL);
        Hit before = pending.get(key);
        float total = (float)Math.min(Float.MAX_VALUE, (double)damage + (before == null ? 0 : before.damage));
        pending.put(key, new Hit(total, 0, 0, 0, flags, weapon));
    }

    List<Hit> drain(float pushX, float pushZ, float pushStrength, boolean critical) {
        List<Hit> result = new ArrayList<>(pending.size());
        // A fire/poison tick may arrive before the sword in this tick. Give the physical
        // hit its knockback, instead of losing it when the host suppresses DOT stagger.
        boolean impactUsed = false;
        for (Hit hit : pending.values()) {
            boolean impact=!impactUsed&&(hit.flags&(8|256|512|1024))==0;
            result.add(new Hit(hit.damage, impact ? pushX : 0, impact ? pushZ : 0,
                impact ? pushStrength : 0, hit.flags | (impact&&critical ? 1 : 0), hit.weapon));
            if(impact)impactUsed=true;
        }
        if (!impactUsed && pushStrength > 0) result.add(new Hit(0, pushX, pushZ, pushStrength, 0, 0));
        pending.clear();
        return result;
    }
}
