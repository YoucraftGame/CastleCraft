package dev.skycraft.client.mixin;

import dev.skycraft.world.BridgeSimulation;
import net.minecraft.client.multiplayer.ClientLevel;
import net.minecraft.world.entity.Entity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(ClientLevel.class)
public abstract class CastleSimulationMixin {
    @Inject(method="tickNonPassenger", at=@At("HEAD"), cancellable=true)
    private void castlecraft$holdEntities(Entity entity, CallbackInfo ci) {
        if (BridgeSimulation.paused(entity)) {entity.xo=entity.getX();entity.yo=entity.getY();entity.zo=entity.getZ();ci.cancel();}
    }
}
