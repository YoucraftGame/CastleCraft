package dev.skycraft.client.mixin;

import dev.skycraft.client.SkyClient;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.Pseudo;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/** Optional Dynamic FPS 3.11.10 compatibility; no configuration is changed or saved. */
@Pseudo
@Mixin(targets = "dynamic_fps.impl.DynamicFPSMod", remap = false)
public abstract class DynamicFpsCompatMixin {
    @Inject(method = "volumeMultiplier(Lnet/minecraft/sounds/SoundSource;)F", at = @At("HEAD"), cancellable = true, require = 0, remap = false)
    private static void castlecraft$bridgeAudio(net.minecraft.sounds.SoundSource source, CallbackInfoReturnable<Float> ci) {
        if (SkyClient.linked()) ci.setReturnValue(1f);
    }
    @Inject(method = "checkForRender(J)Z", at = @At("HEAD"), cancellable = true, require = 0, remap = false)
    private static void castlecraft$renderBridge(long elapsed, CallbackInfoReturnable<Boolean> ci) {
        if (SkyClient.linked()) ci.setReturnValue(true);
    }
    @Inject(method = "targetFrameRate()I", at = @At("HEAD"), cancellable = true, require = 0, remap = false)
    private static void castlecraft$bridgeRate(CallbackInfoReturnable<Integer> ci) {
        if (SkyClient.linked()) ci.setReturnValue(SkyClient.nativeFrameLimit());
    }
}
