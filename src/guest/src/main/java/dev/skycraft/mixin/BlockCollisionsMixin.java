package dev.skycraft.mixin;

import dev.skycraft.combat.SkyCombat;

import com.llamalad7.mixinextras.injector.wrapoperation.Operation;
import com.llamalad7.mixinextras.injector.wrapoperation.WrapOperation;
import dev.skycraft.world.SkyCollision;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.BlockCollisions;
import net.minecraft.world.level.CollisionGetter;
import net.minecraft.world.level.block.state.BlockState;
import net.minecraft.world.phys.shapes.CollisionContext;
import net.minecraft.world.phys.AABB;
import java.util.Iterator;
import java.util.function.BiFunction;
import org.spongepowered.asm.mixin.Shadow;
import org.spongepowered.asm.mixin.Final;
import org.spongepowered.asm.mixin.Unique;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
import net.minecraft.world.phys.shapes.Shapes;
import net.minecraft.world.phys.shapes.VoxelShape;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;

/**
 * Adds Skyrim's geometry to every block-collision query. Vanilla movement, step-up, onGround
 * and fall-damage logic then run unchanged against it.
 */
@Mixin(BlockCollisions.class)
public abstract class BlockCollisionsMixin<T> {
    @Shadow @Final private AABB box;
    @Shadow @Final private CollisionGetter collisionGetter;
    @Shadow @Final private boolean onlySuffocatingBlocks;
    @Shadow @Final private BlockPos.MutableBlockPos pos;
    @Shadow @Final private BiFunction<BlockPos.MutableBlockPos, VoxelShape, T> resultProvider;
    @Unique private Iterator<SkyCollision.NativeCollision> castlecraft$native;

    // Vanilla skips null chunks and air before asking for a block shape. Yield native
    // geometry first, through the same resultProvider, for every collision context.
    // This includes pre-move queries and supporting-block lookups used by all mobs.
    @Inject(method="computeNext", at=@At("HEAD"), cancellable=true)
    private void castlecraft$nativeGeometry(CallbackInfoReturnable<T> cir) {
        if (onlySuffocatingBlocks || !SkyCombat.isBridgeGeometry(collisionGetter)) return;
        if (castlecraft$native == null) castlecraft$native = SkyCollision.collisions(box).iterator();
        if (castlecraft$native.hasNext()) {
            var collision = castlecraft$native.next();
            pos.set(collision.pos());
            cir.setReturnValue(resultProvider.apply(pos, collision.shape()));
        }
    }
	@WrapOperation(
		method = "computeNext",
		at = @At(
			value = "INVOKE",
			target = "Lnet/minecraft/world/phys/shapes/CollisionContext;getCollisionShape(Lnet/minecraft/world/level/block/state/BlockState;Lnet/minecraft/world/level/CollisionGetter;Lnet/minecraft/core/BlockPos;)Lnet/minecraft/world/phys/shapes/VoxelShape;"
		)
	)
	private VoxelShape skycraft$addSkyrimShape(
		CollisionContext context, BlockState state, CollisionGetter level, BlockPos pos, Operation<VoxelShape> original
	) {
		VoxelShape blockShape = original.call(context, state, level, pos);
        if (!SkyCombat.isBridgeGeometry(level)) return blockShape;
		// The walls of holes dug into Skyrim's ground: solid for everyone.
		if (state.isAir()) {
			VoxelShape wall = dev.skycraft.world.SkyDig.wallShape(level, pos);
			if (wall != null) {
				blockShape = blockShape.isEmpty() ? wall : Shapes.or(blockShape, wall);
			}
		}
        // Native room shapes are yielded before the vanilla cursor above.
        return blockShape;
    }
}
