package dev.skycraft.client.mixin;

import dev.skycraft.client.SkyDigClient;
import net.minecraft.client.multiplayer.ClientLevel;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.block.state.BlockState;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

/** A dug block breaking reveals what's around it (SkyDigClient). */
@Mixin(ClientLevel.class)
public abstract class ClientLevelDigMixin {
 // Sodium replaces LevelExtractor's block path without calling setSectionDirty.
 // Queue after the client state change; mesh generation still runs on the render thread.
 @Inject(method = "setBlocksDirty", at = @At("TAIL"))
 private void castlecraft$meshChanged(BlockPos pos, BlockState oldState, BlockState newState, CallbackInfo ci) {
  castlecraft$dirtyMesh(pos);
 }
 @Inject(method = "sendBlockUpdated", at = @At("TAIL"))
 private void castlecraft$meshUpdated(BlockPos pos, BlockState oldState, BlockState newState, int flags, CallbackInfo ci) {
  castlecraft$dirtyMesh(pos);
 }
 @org.spongepowered.asm.mixin.Unique
 private void castlecraft$dirtyMesh(BlockPos pos) {
  if (!dev.skycraft.client.SkyClient.linked() || (Object)this != net.minecraft.client.Minecraft.getInstance().level) return;
  dev.skycraft.world.CastleBlockUpdates.changed(pos.getX(),pos.getY(),pos.getZ(),dev.skycraft.client.render.WorldExporter::markDirtyNow);
 }
	@Inject(method = "setBlocksDirty", at = @At("HEAD"))
	private void skycraft$blockChanged(BlockPos pos, BlockState oldState, BlockState newState, CallbackInfo ci) {
		SkyDigClient.blockChanged((ClientLevel) (Object) this, pos, oldState, newState);
	}
}
