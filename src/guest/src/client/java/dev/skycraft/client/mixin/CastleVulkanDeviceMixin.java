package dev.skycraft.client.mixin;
import com.mojang.renderpearl.backend.vulkan.VulkanBackend;
import org.lwjgl.vulkan.*;
import org.lwjgl.PointerBuffer;
import org.lwjgl.system.MemoryStack;
import org.lwjgl.system.MemoryUtil;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import com.llamalad7.mixinextras.injector.wrapoperation.WrapOperation;
import com.llamalad7.mixinextras.injector.wrapoperation.Operation;
import java.util.LinkedHashSet;
import dev.skycraft.SkyCraft;
@Mixin(VulkanBackend.class)
public abstract class CastleVulkanDeviceMixin {
 // The public createDevice overload contains no vkCreateDevice invocation.
 // Select only the private static factory, with the exact retail 26.3 descriptor.
 @WrapOperation(method="createDevice(Lcom/mojang/renderpearl/backend/vulkan/init/FeatureSet;Lcom/mojang/renderpearl/backend/vulkan/VulkanPhysicalDevice;)Lorg/lwjgl/vulkan/VkDevice;",remap=false,at=@At(value="INVOKE",remap=false,target="Lorg/lwjgl/vulkan/VK12;vkCreateDevice(Lorg/lwjgl/vulkan/VkPhysicalDevice;Lorg/lwjgl/vulkan/VkDeviceCreateInfo;Lorg/lwjgl/vulkan/VkAllocationCallbacks;Lorg/lwjgl/PointerBuffer;)I"))
 private static int castlecraft$externalMemory(VkPhysicalDevice physical,VkDeviceCreateInfo info,VkAllocationCallbacks allocator,PointerBuffer output,Operation<Integer> original){
  if(!System.getProperty("os.name").startsWith("Windows")||"0".equals(System.getenv("CASTLECRAFT_GPU_OVERLAY")))return original.call(physical,info,allocator,output);
  // Allocate on createDevice's existing stack frame so strings survive its VkDevice constructor.
  MemoryStack stack=MemoryStack.stackGet();var count=stack.ints(0);
  if(VK10.vkEnumerateDeviceExtensionProperties(physical,(java.nio.ByteBuffer)null,count,null)!=VK10.VK_SUCCESS)return original.call(physical,info,allocator,output);
  var names=new LinkedHashSet<String>();
  // NVIDIA exposes hundreds of extensions; don't exhaust createDevice's small native stack.
  try(var available=VkExtensionProperties.calloc(count.get(0))){
   if(VK10.vkEnumerateDeviceExtensionProperties(physical,(java.nio.ByteBuffer)null,count,available)!=VK10.VK_SUCCESS)return original.call(physical,info,allocator,output);
   for(var extension:available)names.add(extension.extensionNameString());
  }
  if(!names.contains("VK_KHR_external_memory_win32")||!names.contains("VK_KHR_win32_keyed_mutex"))return original.call(physical,info,allocator,output);
  var prior=info.ppEnabledExtensionNames();var enabled=new LinkedHashSet<String>();
  if(prior!=null)for(int i=0;i<prior.remaining();i++)enabled.add(MemoryUtil.memUTF8(prior.get(prior.position()+i)));
  enabled.add("VK_KHR_external_memory_win32");enabled.add("VK_KHR_win32_keyed_mutex");
  PointerBuffer pointers=stack.mallocPointer(enabled.size());for(String name:enabled)pointers.put(stack.UTF8(name));pointers.flip();info.ppEnabledExtensionNames(pointers);
  int result=original.call(physical,info,allocator,output);
  if(result!=VK10.VK_SUCCESS){info.ppEnabledExtensionNames(prior);output.put(output.position(),0L);return original.call(physical,info,allocator,output);}
  SkyCraft.LOG.info("CastleCraft Vulkan: enabled Win32 external-memory and keyed-mutex device extensions");return result;
 }
}
