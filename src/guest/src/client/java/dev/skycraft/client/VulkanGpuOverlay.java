package dev.skycraft.client;

import com.mojang.renderpearl.backend.vulkan.VulkanGpuTexture;
import com.mojang.renderpearl.backend.vulkan.VulkanDevice;
import dev.skycraft.client.mixin.CastleVulkanTextureAccessor;
import dev.skycraft.SkyCraft;
import java.lang.foreign.*;
import java.lang.invoke.MethodHandle;
import java.nio.ByteOrder;
import org.lwjgl.system.MemoryStack;
import org.lwjgl.vulkan.*;
import static org.lwjgl.vulkan.VK10.*;
import static org.lwjgl.vulkan.VK11.*;
import static org.lwjgl.vulkan.KHRExternalMemoryWin32.*;
import static org.lwjgl.vulkan.KHRWin32KeyedMutex.*;

/** Vulkan graphics-queue blit into imported D3D11 memory, fence publish, keyed-mutex handoff. */
public final class VulkanGpuOverlay {
 private static VulkanDevice backend;
 private static VkDevice device;
 private static MethodHandle prepare,handle,nativeGeneration,canSubmit,publish,ready,nativeClose;
 private static long image,memory,pool,fence,generation;
 private static VkCommandBuffer command;
 private static VulkanGpuTexture inFlightSource;
 private static int width,height;
 private static boolean pending,first=true,disabled,reported;
 private static long submittedAt;
 private VulkanGpuOverlay(){}
 private static void check(int result,String operation){if(result!=VK_SUCCESS)throw new IllegalStateException(operation+" failed: "+result);}
 private static void functions() throws Throwable {
  if(prepare!=null)return;
  var symbols=GpuOverlay.library();if(symbols==null)throw new IllegalStateException("native GPU DLL unavailable");
  Linker linker=Linker.nativeLinker();
  prepare=linker.downcallHandle(symbols.find("CC_VulkanPrepare").orElseThrow(),FunctionDescriptor.of(ValueLayout.JAVA_INT,ValueLayout.JAVA_INT,ValueLayout.JAVA_INT,ValueLayout.JAVA_INT,ValueLayout.JAVA_INT));
  handle=linker.downcallHandle(symbols.find("CC_VulkanHandle").orElseThrow(),FunctionDescriptor.of(ValueLayout.JAVA_LONG));
  nativeGeneration=linker.downcallHandle(symbols.find("CC_VulkanGeneration").orElseThrow(),FunctionDescriptor.of(ValueLayout.JAVA_LONG));
  canSubmit=linker.downcallHandle(symbols.find("CC_VulkanCanSubmit").orElseThrow(),FunctionDescriptor.of(ValueLayout.JAVA_INT,ValueLayout.JAVA_LONG));
  publish=linker.downcallHandle(symbols.find("CC_VulkanPublish").orElseThrow(),FunctionDescriptor.of(ValueLayout.JAVA_INT,ValueLayout.JAVA_LONG));
  ready=linker.downcallHandle(symbols.find("CC_HostReady").orElseThrow(),FunctionDescriptor.of(ValueLayout.JAVA_INT));
  nativeClose=linker.downcallHandle(symbols.find("CC_ProducerClose").orElseThrow(),FunctionDescriptor.ofVoid());
 }
 public static boolean capture(VulkanGpuTexture source,int w,int h){
  if(disabled)return false;
  try{
   VulkanDevice actual=((CastleVulkanTextureAccessor)(Object)source).castlecraft$device();
   if(backend!=null&&backend!=actual)throw new IllegalStateException("Vulkan device changed before close callback");
   backend=actual;device=actual.vkDevice();
   if(!device.getCapabilities().VK_KHR_external_memory_win32||!device.getCapabilities().VK_KHR_win32_keyed_mutex){disabled=true;SkyCraft.LOG.warn("Vulkan GPU overlay: required device extensions not enabled, CPU fallback retained");return false;}
   functions();
   if(pending){
    int status=vkGetFenceStatus(device,fence);
    if(status==VK_NOT_READY){if(System.nanoTime()-submittedAt>2_000_000_000L)throw new IllegalStateException("Vulkan shared-copy fence stalled");return (int)ready.invokeExact()!=0;}
    check(status,"copy fence");pending=false;if(inFlightSource!=null){inFlightSource.removeViews();inFlightSource=null;}int ignored=(int)publish.invokeExact(generation);
   }
   long current=(long)nativeGeneration.invokeExact();
   if(image==0||width!=w||height!=h||generation!=current){
    // Fence is complete here. Destroy imported memory BEFORE native D3D texture replacement.
    destroyImport();
    try(MemoryStack stack=MemoryStack.stackPush()){
     var id=VkPhysicalDeviceIDProperties.calloc(stack).sType$Default();var properties=VkPhysicalDeviceProperties2.calloc(stack).sType$Default().pNext(id.address());
     vkGetPhysicalDeviceProperties2(device.getPhysicalDevice(),properties);
     if(!id.deviceLUIDValid())throw new IllegalStateException("Vulkan device LUID unavailable");
     var luid=id.deviceLUID().order(ByteOrder.LITTLE_ENDIAN);
     if((int)prepare.invokeExact(w,h,luid.getInt(0),luid.getInt(4))==0)return false;
    }
    long shared=(long)handle.invokeExact();generation=(long)nativeGeneration.invokeExact();
    if(shared==0)return false;importTexture(shared,w,h);width=w;height=h;first=true;
   }
   boolean active=(int)ready.invokeExact()!=0;
   if(active&&!reported){reported=true;SkyCraft.LOG.info("CastleCraft Vulkan GPU overlay ACTIVE: D3D11 consumer confirmed; CPU overlay readback bypassed");}
   if((int)canSubmit.invokeExact(generation)==0)return active;
   // Flush Minecraft's own current render commands onto the SAME graphics queue first.
   backend.createCommandEncoder().submit();
   submitCopy(source);return active;
  }catch(Throwable error){disabled=true;SkyCraft.LOG.warn("Vulkan GPU overlay disabled; CPU fallback retained",error);return false;}
 }
 private static void importTexture(long shared,int w,int h){
  try(MemoryStack stack=MemoryStack.stackPush()){
   int type=VK_EXTERNAL_MEMORY_HANDLE_TYPE_D3D11_TEXTURE_KMT_BIT;
   var externalQuery=VkPhysicalDeviceExternalImageFormatInfo.calloc(stack).sType$Default().handleType(type);
   var query=VkPhysicalDeviceImageFormatInfo2.calloc(stack).sType$Default().pNext(externalQuery.address()).format(VK_FORMAT_R8G8B8A8_UNORM).type(VK_IMAGE_TYPE_2D).tiling(VK_IMAGE_TILING_OPTIMAL).usage(VK_IMAGE_USAGE_TRANSFER_DST_BIT);
   var externalProperties=VkExternalImageFormatProperties.calloc(stack).sType$Default();var properties=VkImageFormatProperties2.calloc(stack).sType$Default().pNext(externalProperties.address());
   check(vkGetPhysicalDeviceImageFormatProperties2(device.getPhysicalDevice(),query,properties),"external image compatibility");
   if((externalProperties.externalMemoryProperties().externalMemoryFeatures()&VK_EXTERNAL_MEMORY_FEATURE_IMPORTABLE_BIT)==0)throw new IllegalStateException("D3D11 KMT image not importable");
   var external=VkExternalMemoryImageCreateInfo.calloc(stack).sType$Default().handleTypes(type);
   var create=VkImageCreateInfo.calloc(stack).sType$Default().pNext(external.address()).imageType(VK_IMAGE_TYPE_2D).format(VK_FORMAT_R8G8B8A8_UNORM).mipLevels(1).arrayLayers(1).samples(VK_SAMPLE_COUNT_1_BIT).tiling(VK_IMAGE_TILING_OPTIMAL).usage(VK_IMAGE_USAGE_TRANSFER_DST_BIT).sharingMode(VK_SHARING_MODE_EXCLUSIVE).initialLayout(VK_IMAGE_LAYOUT_UNDEFINED);
   create.extent().set(w,h,1);var out=stack.longs(0);check(vkCreateImage(device,create,null,out),"external image creation");image=out.get(0);
   var requirements=VkMemoryRequirements.calloc(stack);vkGetImageMemoryRequirements(device,image,requirements);
   var handleProperties=VkMemoryWin32HandlePropertiesKHR.calloc(stack).sType$Default();check(vkGetMemoryWin32HandlePropertiesKHR(device,type,shared,handleProperties),"D3D11 memory type");
   int bits=requirements.memoryTypeBits()&handleProperties.memoryTypeBits();if(bits==0)throw new IllegalStateException("No compatible imported-memory type");
   int selected=Integer.numberOfTrailingZeros(bits);
   var physicalMemory=VkPhysicalDeviceMemoryProperties.calloc(stack);vkGetPhysicalDeviceMemoryProperties(device.getPhysicalDevice(),physicalMemory);
   for(int i=0;i<physicalMemory.memoryTypeCount();i++)if((bits&(1<<i))!=0&&(physicalMemory.memoryTypes(i).propertyFlags()&VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT)!=0){selected=i;break;}
   var dedicated=VkMemoryDedicatedAllocateInfo.calloc(stack).sType$Default().image(image);
   var imported=VkImportMemoryWin32HandleInfoKHR.calloc(stack).sType$Default().pNext(dedicated.address()).handleType(type).handle(shared);
   var allocate=VkMemoryAllocateInfo.calloc(stack).sType$Default().pNext(imported.address()).allocationSize(requirements.size()).memoryTypeIndex(selected);
   check(vkAllocateMemory(device,allocate,null,out),"D3D11 memory import");memory=out.get(0);check(vkBindImageMemory(device,image,memory,0),"external image binding");
   if(pool==0){
    var poolInfo=VkCommandPoolCreateInfo.calloc(stack).sType$Default().flags(VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT).queueFamilyIndex(backend.graphicsQueue().queueFamilyIndex());
    check(vkCreateCommandPool(device,poolInfo,null,out),"copy command pool");pool=out.get(0);
    var commandInfo=VkCommandBufferAllocateInfo.calloc(stack).sType$Default().commandPool(pool).level(VK_COMMAND_BUFFER_LEVEL_PRIMARY).commandBufferCount(1);var pointer=stack.mallocPointer(1);check(vkAllocateCommandBuffers(device,commandInfo,pointer),"copy command buffer");command=new VkCommandBuffer(pointer.get(0),device);
    check(vkCreateFence(device,VkFenceCreateInfo.calloc(stack).sType$Default(),null,out),"copy fence creation");fence=out.get(0);
   }
   SkyCraft.LOG.info("CastleCraft Vulkan GPU overlay imported D3D11 image {}x{}, generation {}",w,h,generation);
  }
 }
 private static void submitCopy(VulkanGpuTexture source){
  try(MemoryStack stack=MemoryStack.stackPush()){
   check(vkResetFences(device,fence),"copy fence reset");check(vkResetCommandBuffer(command,0),"copy command reset");
   check(vkBeginCommandBuffer(command,VkCommandBufferBeginInfo.calloc(stack).sType$Default().flags(VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT)),"copy command begin");
   int family=backend.graphicsQueue().queueFamilyIndex();
   var barriers=VkImageMemoryBarrier.calloc(2,stack);
   barriers.get(0).sType$Default().srcAccessMask(VK_ACCESS_MEMORY_WRITE_BIT).dstAccessMask(VK_ACCESS_TRANSFER_READ_BIT).oldLayout(VK_IMAGE_LAYOUT_GENERAL).newLayout(VK_IMAGE_LAYOUT_GENERAL).srcQueueFamilyIndex(VK_QUEUE_FAMILY_IGNORED).dstQueueFamilyIndex(VK_QUEUE_FAMILY_IGNORED).image(source.vkImage());
   barriers.get(1).sType$Default().srcAccessMask(0).dstAccessMask(VK_ACCESS_TRANSFER_WRITE_BIT).oldLayout(first?VK_IMAGE_LAYOUT_UNDEFINED:VK_IMAGE_LAYOUT_GENERAL).newLayout(VK_IMAGE_LAYOUT_GENERAL).srcQueueFamilyIndex(VK_QUEUE_FAMILY_EXTERNAL).dstQueueFamilyIndex(family).image(image);
   for(int i=0;i<2;i++)barriers.get(i).subresourceRange().aspectMask(VK_IMAGE_ASPECT_COLOR_BIT).baseMipLevel(0).levelCount(1).baseArrayLayer(0).layerCount(1);
   vkCmdPipelineBarrier(command,VK_PIPELINE_STAGE_ALL_COMMANDS_BIT,VK_PIPELINE_STAGE_TRANSFER_BIT,0,null,null,barriers);
   var region=VkImageBlit.calloc(1,stack);region.srcSubresource().aspectMask(VK_IMAGE_ASPECT_COLOR_BIT).mipLevel(0).baseArrayLayer(0).layerCount(1);region.dstSubresource().set(region.srcSubresource());
   region.srcOffsets(0).set(0,0,0);region.srcOffsets(1).set(width,height,1);region.dstOffsets(0).set(0,0,0);region.dstOffsets(1).set(width,height,1);
   vkCmdBlitImage(command,source.vkImage(),VK_IMAGE_LAYOUT_GENERAL,image,VK_IMAGE_LAYOUT_GENERAL,region,VK_FILTER_NEAREST);
   var release=VkImageMemoryBarrier.calloc(1,stack);release.sType$Default().srcAccessMask(VK_ACCESS_TRANSFER_WRITE_BIT).dstAccessMask(0).oldLayout(VK_IMAGE_LAYOUT_GENERAL).newLayout(VK_IMAGE_LAYOUT_GENERAL).srcQueueFamilyIndex(family).dstQueueFamilyIndex(VK_QUEUE_FAMILY_EXTERNAL).image(image);release.subresourceRange().set(barriers.get(1).subresourceRange());
   vkCmdPipelineBarrier(command,VK_PIPELINE_STAGE_TRANSFER_BIT,VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT,0,null,null,release);
   check(vkEndCommandBuffer(command),"copy command end");
   var mutex=CastleVulkanSync.handoff(stack,memory);
   var submit=VkSubmitInfo.calloc(stack).sType$Default().pNext(mutex.address()).pCommandBuffers(stack.pointers(command.address()));
   source.addViews();
   try{check(vkQueueSubmit(backend.graphicsQueue().vkQueue(),submit,fence),"external copy queue submit");inFlightSource=source;pending=true;first=false;submittedAt=System.nanoTime();}
   finally{if(inFlightSource!=source)source.removeViews();}
  }
 }
 private static void destroyImport(){
  if(device==null)return;if(image!=0)vkDestroyImage(device,image,null);if(memory!=0)vkFreeMemory(device,memory,null);image=memory=0;
 }
 public static void closeForDevice(VulkanDevice closing){
  if(backend!=closing||device==null)return;
  // Shutdown only: never waitIdle in the live capture path.
  vkDeviceWaitIdle(device);if(inFlightSource!=null){inFlightSource.removeViews();inFlightSource=null;}destroyImport();if(fence!=0)vkDestroyFence(device,fence,null);if(pool!=0)vkDestroyCommandPool(device,pool,null);
  try{if(nativeClose!=null)nativeClose.invokeExact();}catch(Throwable ignored){}
  image=memory=pool=fence=generation=0;command=null;pending=false;backend=null;device=null;disabled=false;reported=false;
 }
}
