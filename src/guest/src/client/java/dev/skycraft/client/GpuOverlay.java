package dev.skycraft.client;

import com.mojang.renderpearl.backend.opengl.GlTexture;
import com.mojang.renderpearl.api.textures.GpuTexture;
import dev.skycraft.SkyCraft;
import java.lang.foreign.*;
import java.lang.invoke.MethodHandle;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.HexFormat;
import net.fabricmc.loader.api.FabricLoader;

/** Optional OpenGL/Vulkan->D3D11 producer; CPU readback remains available if sharing cannot run. */
final class GpuOverlay {
 private static boolean tried,disabled,reported;
 private static MethodHandle produce,error;
 private static final Arena LIBRARY=Arena.ofShared();
 private static long nextDiagnostic;
 private static SymbolLookup nativeSymbols;
 static SymbolLookup library() throws Throwable {
   if(!tried){
    tried=true;
    if(!System.getProperty("os.name").startsWith("Windows")||!System.getProperty("os.arch").equals("amd64")){disabled=true;return null;}
    byte[] data;
    try(var input=GpuOverlay.class.getResourceAsStream("/native/windows/CastleCraft.GpuBridge.dll")){
     if(input==null)throw new IllegalStateException("GPU native DLL not packaged");data=input.readAllBytes();
    }
    String hash=HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(data));
    Path directory=FabricLoader.getInstance().getGameDir().resolve("skycraft-native").resolve(hash);Files.createDirectories(directory);
    Path library=directory.resolve("CastleCraft.GpuBridge.dll");
    if(!Files.exists(library))Files.write(library,data,StandardOpenOption.CREATE_NEW);
    if(!MessageDigest.isEqual(MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(library)),MessageDigest.getInstance("SHA-256").digest(data)))throw new IllegalStateException("GPU library hash mismatch");
    nativeSymbols=SymbolLookup.libraryLookup(library,LIBRARY);SymbolLookup symbols=nativeSymbols;Linker linker=Linker.nativeLinker();
    MethodHandle abi=linker.downcallHandle(symbols.find("CC_Abi").orElseThrow(),FunctionDescriptor.of(ValueLayout.JAVA_INT));
    if((int)abi.invokeExact()!=2)throw new IllegalStateException("GPU ABI mismatch");
    produce=linker.downcallHandle(symbols.find("CC_Produce").orElseThrow(),FunctionDescriptor.of(ValueLayout.JAVA_INT,ValueLayout.JAVA_INT,ValueLayout.JAVA_INT,ValueLayout.JAVA_INT));
    error=linker.downcallHandle(symbols.find("CC_Error").orElseThrow(),FunctionDescriptor.of(ValueLayout.JAVA_INT));
    SkyCraft.LOG.info("CastleCraft GPU overlay native producer loaded; OpenGL/Vulkan-to-D3D11 sharing awaits consumer; CPU fallback retained");
   }

 return disabled?null:nativeSymbols;
 }

 private GpuOverlay(){}
 static boolean capture(GpuTexture color,int width,int height){
  if(disabled||"0".equals(System.getenv("CASTLECRAFT_GPU_OVERLAY")))return false;
  if(color instanceof com.mojang.renderpearl.backend.vulkan.VulkanGpuTexture vk)return VulkanGpuOverlay.capture(vk,width,height);
  if(!(color instanceof GlTexture gl))return false;
  try{
   if(library()==null)return false;
   boolean ready=(int)produce.invokeExact(gl.glId(),width,height)!=0;
   if(ready&&!reported){reported=true;SkyCraft.LOG.info("CastleCraft GPU overlay ACTIVE: shared texture consumed by Unity, CPU overlay readback bypassed");}
   if(!ready&&System.currentTimeMillis()>=nextDiagnostic){nextDiagnostic=System.currentTimeMillis()+10000;int code=(int)error.invokeExact();if(code!=0)SkyCraft.LOG.warn("CastleCraft GPU overlay fallback, native reason {}",code);}
   return ready;
  }catch(Throwable e){disabled=true;SkyCraft.LOG.warn("CastleCraft GPU overlay unavailable; retaining CPU readback",e);return false;}
 }
}
