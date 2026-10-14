package dev.skycraft.client.mixin;

import dev.skycraft.client.SkyClient;
import dev.skycraft.client.SkyCollider;
import dev.skycraft.client.CastleMotion;
import net.minecraft.client.Minecraft;
import net.minecraft.world.entity.MoverType;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.ModifyVariable;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

/**
 * After vanilla has collided the local player's movement with Minecraft blocks, collide it with
 * Skyrim's exact triangles (smooth slopes instead of voxel stair-steps).
 */
@Mixin(Entity.class)
public abstract class EntityCollideMixin {
    @ModifyVariable(method = "move", at = @At("HEAD"), argsOnly = true)
    private Vec3 castlecraft$nativeActionMotion(Vec3 movement, MoverType type, Vec3 original) {
        return (Object)this instanceof LocalPlayer player ? CastleMotion.apply(player, type, movement) : movement;
    }
	@ModifyVariable(method = "moveRelative", at = @At("HEAD"), argsOnly = true)
	private Vec3 castlecraft$fixedPlaneMovement(Vec3 input) {
		if ((Object)this instanceof LocalPlayer player && SkyClient.linked() && !Minecraft.getInstance().options.getCameraType().isFirstPerson()) {
			double angle = Math.toRadians(180.0 - player.getYRot());
			double c = Math.cos(angle), s = Math.sin(angle);
			return new Vec3(input.x*c-input.z*s,input.y,input.z*c+input.x*s);
		}
		return input;
	}
	@Inject(method = "collide", at = @At("RETURN"), cancellable = true)
	private void skycraft$smoothSkyrimCollision(Vec3 movement, CallbackInfoReturnable<Vec3> cir) {
		if ((Object) this instanceof LocalPlayer player && SkyClient.linked() && !player.noPhysics) {
			cir.setReturnValue(SkyCollider.collide(player, cir.getReturnValue()));
		}
	}
}
