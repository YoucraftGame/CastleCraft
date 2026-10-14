package dev.skycraft.world;
import java.util.*;
import java.util.function.*;
/** Select visible nearby entities deterministically, rather than starving fresh spawns by insertion order. */
public final class CastleSceneSelection {
 private CastleSceneSelection(){}
 public static <T> List<T> nearest(Collection<T> values,int limit,ToDoubleFunction<T> distance,ToIntFunction<T> id){
  if(limit<=0)return List.of();
  return values.stream().filter(v->Double.isFinite(distance.applyAsDouble(v)))
   .sorted(Comparator.<T>comparingDouble(distance).thenComparingInt(id)).limit(limit).toList();
 }
}
