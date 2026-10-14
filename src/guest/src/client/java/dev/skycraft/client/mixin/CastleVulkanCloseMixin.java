package dev.skycraft.client.mixin;
import com.mojang.renderpearl.backend.vulkan.VulkanDevice;
import dev.skycraft.client.VulkanGpuOverlay;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
@Mixin(VulkanDevice.class)
public abstract class CastleVulkanCloseMixin {
 @Inject(method="close",at=@At("HEAD"))
 private void castlecraft$close(CallbackInfo ci){VulkanGpuOverlay.closeForDevice((VulkanDevice)(Object)this);}
}
