package dev.skycraft.world;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.function.BiPredicate;
/** Search supported free space on the requested floor, never the top of a doorway or boss gate. */
public final class CastleArrivalPlan {
 public static final double MAX_CLEARANCE=2;
 public record Position(double x,double z) {}
 private static final java.util.List<Position> OFFSETS;
 static {
  var offsets=new ArrayList<Position>();
  for(int x=-8;x<=8;x++)for(int z=-8;z<=8;z++)if(x*x+z*z<=64)offsets.add(new Position(x*.25,z*.25));
  offsets.sort(Comparator.comparingDouble(p->p.x*p.x+p.z*p.z));OFFSETS=java.util.List.copyOf(offsets);
 }
 private CastleArrivalPlan() {}
 public static Position choose(double x,double z,BiPredicate<Double,Double> supportedFree){
  if(!Double.isFinite(x)||!Double.isFinite(z))return null;
  for(var offset:OFFSETS){double px=x+offset.x,pz=z+offset.z;if(supportedFree.test(px,pz))return new Position(px,pz);}
  return null;
 }
}
