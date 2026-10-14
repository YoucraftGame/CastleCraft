package dev.skycraft.mixin;

import dev.skycraft.world.CastleBossBars;
import dev.skycraft.SkyCraft;
import net.minecraft.server.level.ServerBossEvent;
import net.minecraft.server.level.ServerPlayer;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(ServerBossEvent.class)
public abstract class CastleBossEventMixin {
    @Inject(method="addPlayer", at=@At("HEAD"))
    private void castlecraft$observe(ServerPlayer player, CallbackInfo ci) {
        if (SkyCraft.isMirrorServer(player.level().getServer()))
            CastleBossBars.observe((ServerBossEvent)(Object)this);
    }
}
