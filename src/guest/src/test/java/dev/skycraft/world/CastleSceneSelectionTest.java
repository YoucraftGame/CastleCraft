package dev.skycraft.world;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class CastleSceneSelectionTest {
 record Entity(int id,double distance){}
 @Test void freshNearSpawnIsNotStarvedByOldFarEntities(){
  var e=List.of(new Entity(1,100),new Entity(2,90),new Entity(3,1));
  assertEquals(List.of(e.get(2),e.get(1)),CastleSceneSelection.nearest(e,2,Entity::distance,Entity::id));
 }
 @Test void equalDistanceStableAndInvalidPoseRejected(){
  var e=List.of(new Entity(4,1),new Entity(3,1),new Entity(2,Double.NaN));
  assertEquals(List.of(e.get(1),e.get(0)),CastleSceneSelection.nearest(e,3,Entity::distance,Entity::id));
 }
}
