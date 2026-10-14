package dev.skycraft.combat;
import java.util.List;
import net.minecraft.core.Direction;
import net.minecraft.world.phys.AABB;
import net.minecraft.world.phys.Vec3;
import net.minecraft.world.phys.shapes.Shapes;
import net.minecraft.world.phys.shapes.VoxelShape;
/** Horizontal external forces only; native animation, jumping and AI keep their ownership. */
public final class CastleDisplacement {
 private CastleDisplacement() {}
 public static Vec3 limit(Vec3 v){
  if(!Double.isFinite(v.x)||!Double.isFinite(v.z))return Vec3.ZERO;
  double n=Math.sqrt(v.x*v.x+v.z*v.z),scale=n>.25?.25/n:1;
  return new Vec3(v.x*scale,0,v.z*scale);
 }
 public static Vec3 solve(AABB box,Vec3 request,List<VoxelShape> shapes){
  Vec3 v=limit(request);double dx=v.x,dz=v.z;
  if(Math.abs(dx)<Math.abs(dz)){
   dz=Shapes.collide(Direction.Axis.Z,box,shapes,dz);box=box.move(0,0,dz);
   dx=Shapes.collide(Direction.Axis.X,box,shapes,dx);
  }else{
   dx=Shapes.collide(Direction.Axis.X,box,shapes,dx);box=box.move(dx,0,0);
   dz=Shapes.collide(Direction.Axis.Z,box,shapes,dz);
  }
  return new Vec3(dx,0,dz);
 }
}
