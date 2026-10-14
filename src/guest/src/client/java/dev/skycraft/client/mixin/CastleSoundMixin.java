package dev.skycraft.client.mixin;
import dev.skycraft.client.SkyClient;
import net.minecraft.client.resources.sounds.SoundInstance;
import net.minecraft.client.resources.sounds.TickableSoundInstance;
import net.minecraft.client.sounds.SoundEngine;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(SoundEngine.class)
public abstract class CastleSoundMixin implements dev.skycraft.client.CastleAudio.ListenerAccess {
 @org.spongepowered.asm.mixin.Shadow private boolean loaded;
 @org.spongepowered.asm.mixin.Shadow @org.spongepowered.asm.mixin.Final private net.minecraft.client.sounds.SoundEngineExecutor executor;
 @org.spongepowered.asm.mixin.Shadow @org.spongepowered.asm.mixin.Final private com.mojang.blaze3d.audio.Listener listener;
 public void castlecraft$listener(com.mojang.blaze3d.audio.ListenerTransform transform){
  if(loaded)executor.execute(()->listener.setTransform(transform));
 }
 @Inject(method="updateSource(Lnet/minecraft/client/Camera;)V",at=@At("HEAD"),cancellable=true)
 private void castlecraft$liveListener(net.minecraft.client.Camera camera,CallbackInfo ci){
  var pose=dev.skycraft.client.CastleAudio.pose(net.minecraft.client.Minecraft.getInstance());
  if(pose!=null){castlecraft$listener(pose);ci.cancel();}
 }
 @Inject(method="play",at=@At("HEAD"),cancellable=true)
 private void castlecraft$inactiveMirror(SoundInstance sound,CallbackInfoReturnable<SoundEngine.PlayResult> ci){
  if(dev.skycraft.world.CastleMirrorLifecycle.worldAudio(SkyClient.mirrorSuspended(),sound.getSource().name()))ci.setReturnValue(SoundEngine.PlayResult.NOT_STARTED);
 }
 @Inject(method="playDelayed",at=@At("HEAD"),cancellable=true)
 private void castlecraft$inactiveDelayed(SoundInstance sound,int delay,CallbackInfo ci){
  if(dev.skycraft.world.CastleMirrorLifecycle.worldAudio(SkyClient.mirrorSuspended(),sound.getSource().name()))ci.cancel();
 }
 @Inject(method="queueTickingSound",at=@At("HEAD"),cancellable=true)
 private void castlecraft$inactiveLoop(TickableSoundInstance sound,CallbackInfo ci){
  if(dev.skycraft.world.CastleMirrorLifecycle.worldAudio(SkyClient.mirrorSuspended(),sound.getSource().name()))ci.cancel();
 }
}
