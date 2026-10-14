package dev.skycraft.mixin;

import dev.skycraft.combat.SkyCombat;
import dev.skycraft.world.SkyCollision;
import net.minecraft.core.BlockPos;
import net.minecraft.world.level.BlockGetter;
import net.minecraft.world.level.pathfinder.WalkNodeEvaluator;
import net.minecraft.world.level.pathfinder.PathType;
import net.minecraft.world.level.pathfinder.PathfindingContext;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;
@Mixin(WalkNodeEvaluator.class)
public abstract class CastlePathfindingMixin {
 @Inject(method="getPathTypeStatic(Lnet/minecraft/world/level/pathfinder/PathfindingContext;Lnet/minecraft/core/BlockPos$MutableBlockPos;)Lnet/minecraft/world/level/pathfinder/PathType;",at=@At("RETURN"),cancellable=true)
 private static void castlecraft$ground(PathfindingContext context,BlockPos.MutableBlockPos pos,CallbackInfoReturnable<PathType> cir){
  if(!SkyCombat.isBridgeGeometry(context.level())||!SkyCollision.active())return;
  if(SkyCollision.hasGeometry(pos)&&SkyCollision.solidFraction(pos)>.1f)cir.setReturnValue(PathType.BLOCKED);
  else if(cir.getReturnValue()==PathType.OPEN&&SkyCollision.supportsFromBelow(pos))cir.setReturnValue(PathType.WALKABLE);
 }
 @Inject(method="getFloorLevel(Lnet/minecraft/world/level/BlockGetter;Lnet/minecraft/core/BlockPos;)D",at=@At("RETURN"),cancellable=true)
 private static void castlecraft$floor(BlockGetter level,BlockPos pos,CallbackInfoReturnable<Double> cir){if(!SkyCombat.isBridgeGeometry(level))return;var below=pos.below();var shape=SkyCollision.shapeAt(below);if(shape!=null&&!shape.isEmpty())cir.setReturnValue(Math.max(cir.getReturnValue(),below.getY()+shape.max(net.minecraft.core.Direction.Axis.Y)));}
}
