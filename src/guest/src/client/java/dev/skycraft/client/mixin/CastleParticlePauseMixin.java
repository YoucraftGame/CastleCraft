package dev.skycraft.client.mixin;
import dev.skycraft.client.SkyClient;
import net.minecraft.client.particle.ParticleEngine;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
/** Native pause must not age out mirror spells while the native simulation is held. */
@Mixin(ParticleEngine.class)
public abstract class CastleParticlePauseMixin {
 @Inject(method="tick",at=@At("HEAD"),cancellable=true)
 private void castlecraft$mirrorPause(CallbackInfo ci){if(SkyClient.mirrorSuspended())ci.cancel();}
}
