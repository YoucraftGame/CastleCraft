package dev.skycraft.client;

import org.lwjgl.system.MemoryStack;
import org.lwjgl.vulkan.VkWin32KeyedMutexAcquireReleaseInfoKHR;

/** The same submission descriptor is used by production and the real-driver regression probe. */
public final class CastleVulkanSync {
 private CastleVulkanSync() {}
 public static VkWin32KeyedMutexAcquireReleaseInfoKHR handoff(MemoryStack stack, long memory) {
  // LWJGL's parallel-array setters do NOT infer these counts. A zero count silently
  // submits a copy without acquiring key 0 or releasing key 1 to the D3D consumer.
  return VkWin32KeyedMutexAcquireReleaseInfoKHR.calloc(stack).sType$Default()
   .acquireCount(1).pAcquireSyncs(stack.longs(memory)).pAcquireKeys(stack.longs(0)).pAcquireTimeouts(stack.ints(0))
   .releaseCount(1).pReleaseSyncs(stack.longs(memory)).pReleaseKeys(stack.longs(1));
 }
}
