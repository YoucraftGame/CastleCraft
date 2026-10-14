package dev.skycraft.mixin;

import dev.skycraft.world.BridgeSimulation;
import dev.skycraft.world.SkyCollision;
import com.llamalad7.mixinextras.injector.wrapmethod.WrapMethod;
import com.llamalad7.mixinextras.injector.wrapoperation.Operation;
import java.util.function.BooleanSupplier;
import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.entity.Entity;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(ServerLevel.class)
public abstract class CastleSimulationMixin {
    @Inject(method="tick", at=@At("HEAD"))
    private void castlecraft$roomState(BooleanSupplier time, CallbackInfo ci) {
        BridgeSimulation.update((ServerLevel)(Object)this);
    }
    @WrapMethod(method="tickNonPassenger")
    private void castlecraft$holdEntities(Entity entity, Operation<Void> original) {
        synchronized(SkyCollision.class) {
            if (!BridgeSimulation.paused(entity)) {
                BridgeSimulation.followCinematicPlayer(entity);
                original.call(entity);
                BridgeSimulation.followCinematicPlayer(entity);
            }
        }
    }
}
