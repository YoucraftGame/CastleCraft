package dev.skycraft.client.mixin;
import com.mojang.renderpearl.backend.vulkan.VulkanGpuTexture;
import com.mojang.renderpearl.backend.vulkan.VulkanDevice;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.gen.Accessor;
@Mixin(VulkanGpuTexture.class)
public interface CastleVulkanTextureAccessor {
 @Accessor("device") VulkanDevice castlecraft$device();
}
