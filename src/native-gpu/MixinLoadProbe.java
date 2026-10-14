import net.fabricmc.api.EnvType;
import net.fabricmc.loader.impl.launch.knot.Knot;

/** Transform retail classes only. Never invokes Minecraft.main or initializes a GPU. */
public final class MixinLoadProbe {
 public static void main(String[] args) throws Exception {
  Knot knot = new Knot(EnvType.CLIENT);
  ClassLoader loader = knot.init(new String[]{"--gameDir", args[0], "--version", "26.3"});
  for (String name : new String[]{
    "com.mojang.renderpearl.backend.vulkan.VulkanBackend",
    "com.mojang.renderpearl.backend.vulkan.VulkanGpuTexture",
    "com.mojang.renderpearl.backend.vulkan.VulkanDevice",
    "net.minecraft.client.sounds.SoundEngine",
    "net.minecraft.client.sounds.SoundManager",
    "net.minecraft.client.multiplayer.ClientLevel"}) {
   Class.forName(name, false, loader);
   System.out.println("MIXIN LOAD PASS: " + name);
  }
  System.out.println("MIXIN PROBE PASS: no game main, no render device, no world loaded");
 }
}
