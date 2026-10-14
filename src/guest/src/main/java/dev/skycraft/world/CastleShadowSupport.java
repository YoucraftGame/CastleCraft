package dev.skycraft.world;
import java.util.OptionalDouble;
import net.minecraft.world.phys.shapes.VoxelShape;
/** Shadows belong to a support face, not to a jumping entity's current height. */
public final class CastleShadowSupport {
 private CastleShadowSupport() {}
 public static OptionalDouble height(double x,double feetY,double z,Iterable<VoxelShape> shapes){
  double best=Double.NEGATIVE_INFINITY;
  for(var shape:shapes)for(var b:shape.toAabbs())
   if(x>=b.minX&&x<=b.maxX&&z>=b.minZ&&z<=b.maxZ&&b.maxY<=feetY+.03&&b.maxY>=feetY-4)best=Math.max(best,b.maxY);
  return best==Double.NEGATIVE_INFINITY?OptionalDouble.empty():OptionalDouble.of(best);
 }
}
