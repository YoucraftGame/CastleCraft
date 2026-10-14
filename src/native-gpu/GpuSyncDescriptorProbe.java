import dev.skycraft.client.CastleVulkanSync;
import org.lwjgl.system.MemoryStack;
import org.lwjgl.vulkan.VkWin32KeyedMutexAcquireReleaseInfoKHR;

public final class GpuSyncDescriptorProbe {
 public static void main(String[] args) {
  try (var stack=MemoryStack.stackPush()) {
   long memory=42;
   var old=VkWin32KeyedMutexAcquireReleaseInfoKHR.calloc(stack).sType$Default()
    .pAcquireSyncs(stack.longs(memory)).pAcquireKeys(stack.longs(0)).pAcquireTimeouts(stack.ints(0))
    .pReleaseSyncs(stack.longs(memory)).pReleaseKeys(stack.longs(1));
   var fixed=CastleVulkanSync.handoff(stack,memory);
   if(old.acquireCount()!=0||old.releaseCount()!=0)throw new AssertionError("old defect no longer reproduced");
   if(fixed.acquireCount()!=1||fixed.releaseCount()!=1||fixed.pAcquireSyncs().get(0)!=memory
     ||fixed.pAcquireKeys().get(0)!=0||fixed.pReleaseKeys().get(0)!=1||fixed.pAcquireTimeouts().get(0)!=0)
    throw new AssertionError("production handoff descriptor invalid");
   System.out.println("{\"oldAcquire\":"+old.acquireCount()+",\"oldRelease\":"+old.releaseCount()
    +",\"acquire\":"+fixed.acquireCount()+",\"release\":"+fixed.releaseCount()+",\"acquireKey\":0,\"releaseKey\":1,\"timeout\":0}");
  }
 }
}
